using System.Buffers;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using VitaMR.Models;

namespace VitaMR.Services;

public sealed class MobileApiHost : IAsyncDisposable
{
    private const int DefaultPort = 5057;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly AppSettings _settings;
    private readonly IPatientRegistryService _patientRegistryService;
    private readonly CancellationTokenSource _stopSource = new();
    private readonly object _pairingLock = new();
    private readonly string _deviceStorePath;
    private readonly string _packetQueuePath;
    private readonly string _captureQueuePath;
    private readonly string _captureFolder;
    private TcpListener? _listener;
    private Task? _acceptLoop;
    private PairingSession? _pairingSession;

    public MobileApiHost(AppSettings settings, IPatientRegistryService patientRegistryService)
    {
        _settings = settings;
        _patientRegistryService = patientRegistryService;

        var appFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VitaMR");
        _deviceStorePath = Path.Combine(appFolder, "Mobile_Trusted_Devices.json");
        _packetQueuePath = Path.Combine(appFolder, "Mobile_Packet_Requests.jsonl");
        _captureQueuePath = Path.Combine(appFolder, "Mobile_Capture_Requests.jsonl");
        _captureFolder = Path.Combine(appFolder, "Mobile_Captures");
    }

    public int Port { get; private set; } = DefaultPort;

    public bool IsRunning => _listener is not null;

    public Func<MobileChatRequest, MobileTrustedDevice, CancellationToken, Task<MobileChatResponse>>? ChatHandler { get; set; }

    public Func<MobileChartPacketRequest, MobileTrustedDevice, CancellationToken, Task<MobileChartPacketResponse>>? ChartPacketHandler { get; set; }

    public Func<MobileVitaMasteryRequest, MobileTrustedDevice, CancellationToken, Task<MobileVitaMasteryResponse>>? VitaMasteryHandler { get; set; }

    public Func<MobileDataHunterQuestRequest, MobileTrustedDevice, CancellationToken, Task<MobileChatResponse>>? DataHunterQuestHandler { get; set; }

    public Func<MobileOfflineItemRequest, MobileTrustedDevice, CancellationToken, Task<MobileOfflineItemResponse>>? OfflineItemHandler { get; set; }

    public Func<MobileCaptureRequest, MobileTrustedDevice, string, int, string, CancellationToken, Task>? CaptureHandler { get; set; }

    public Func<MobileChartPhotoRequest, MobileTrustedDevice, CancellationToken, Task<MobileChartPhotoResponse>>? ChartPhotoHandler { get; set; }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_listener is not null)
        {
            return Task.CompletedTask;
        }

        _listener = new TcpListener(IPAddress.Any, DefaultPort);
        _listener.Start();
        Port = DefaultPort;
        _acceptLoop = Task.Run(() => AcceptLoopAsync(_stopSource.Token), cancellationToken);
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        _stopSource.Cancel();

        try
        {
            _listener?.Stop();
        }
        catch (SocketException)
        {
        }

        if (_acceptLoop is not null)
        {
            try
            {
                await _acceptLoop;
            }
            catch (OperationCanceledException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
        }

        _stopSource.Dispose();
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _listener is not null)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }

            _ = Task.Run(() => HandleClientAsync(client, cancellationToken), cancellationToken);
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using var _ = client;

        try
        {
            using var stream = client.GetStream();
            var request = await ReadRequestAsync(stream, cancellationToken);
            var response = await HandleRequestAsync(request, cancellationToken);
            await WriteResponseAsync(stream, response, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or SocketException or InvalidDataException or JsonException)
        {
            try
            {
                using var stream = client.GetStream();
                await WriteResponseAsync(
                    stream,
                    ApiResponse.Json(HttpStatusCode.BadRequest, new { status = "bad_request", message = exception.Message }),
                    cancellationToken);
            }
            catch
            {
            }
        }
    }

    private async Task<ApiResponse> HandleRequestAsync(ApiRequest request, CancellationToken cancellationToken)
    {
        if (request.Method.Equals("GET", StringComparison.OrdinalIgnoreCase) &&
            request.Path.Equals("/mobile/health", StringComparison.OrdinalIgnoreCase))
        {
            return ApiResponse.Json(HttpStatusCode.OK, new
            {
                status = "ready",
                apiVersion = "mobile-v0.1",
                serverTime = DateTimeOffset.Now,
                port = Port,
                vaultReady = !string.IsNullOrWhiteSpace(_settings.VaultRootPath),
                activeChartId = _settings.ActiveChartId,
                activePatientDisplayName = _settings.ActivePatientDisplayName,
                pairing = "available"
            });
        }

        if (request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase) &&
            request.Path.Equals("/mobile/pair/start", StringComparison.OrdinalIgnoreCase))
        {
            return StartPairing();
        }

        if (request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase) &&
            request.Path.Equals("/mobile/pair/complete", StringComparison.OrdinalIgnoreCase))
        {
            var pairRequest = DeserializeBody<MobilePairCompleteRequest>(request);
            return CompletePairing(pairRequest);
        }

        var trustedDevice = Authenticate(request);
        if (trustedDevice is null)
        {
            return ApiResponse.Json(HttpStatusCode.Unauthorized, new
            {
                status = "unauthorized",
                message = "Pair this phone with VitaMR before using mobile chart endpoints."
            });
        }

        trustedDevice.LastSeenAt = DateTimeOffset.Now;
        SaveTrustedDevices(UpdateTrustedDevice(LoadTrustedDevices(), trustedDevice));

        if (request.Method.Equals("GET", StringComparison.OrdinalIgnoreCase) &&
            request.Path.Equals("/mobile/charts", StringComparison.OrdinalIgnoreCase))
        {
            return ApiResponse.Json(HttpStatusCode.OK, new
            {
                status = "ready",
                activeChartId = _settings.ActiveChartId,
                charts = _patientRegistryService.GetAllIdentities()
                    .OrderBy(patient => patient.PatientDisplayName, StringComparer.OrdinalIgnoreCase)
                    .Select(patient => new
                    {
                        patient.ChartId,
                        patient.PatientDisplayName,
                        patient.DateOfBirth,
                        patient.RelationshipNotes,
                        isActive = patient.ChartId.Equals(_settings.ActiveChartId, StringComparison.OrdinalIgnoreCase)
                    })
                    .ToList()
            });
        }

        if (request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase) &&
            request.Path.Equals("/mobile/chat", StringComparison.OrdinalIgnoreCase))
        {
            var chatRequest = DeserializeBody<MobileChatRequest>(request);
            return await AcceptMobileChatAsync(chatRequest, trustedDevice, cancellationToken);
        }

        if (request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase) &&
            request.Path.Equals("/mobile/chart-packet", StringComparison.OrdinalIgnoreCase))
        {
            var packetRequest = DeserializeBody<MobileChartPacketRequest>(request);
            return await BuildChartPacketAsync(packetRequest, trustedDevice, cancellationToken);
        }

        if (request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase) &&
            request.Path.Equals("/mobile/vita-mastery", StringComparison.OrdinalIgnoreCase))
        {
            var masteryRequest = DeserializeBody<MobileVitaMasteryRequest>(request);
            return await BuildVitaMasteryAsync(masteryRequest, trustedDevice, cancellationToken);
        }

        if (request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase) &&
            request.Path.Equals("/mobile/data-hunter", StringComparison.OrdinalIgnoreCase))
        {
            var dataHunterRequest = DeserializeBody<MobileDataHunterQuestRequest>(request);
            return await BuildDataHunterQuestAsync(dataHunterRequest, trustedDevice, cancellationToken);
        }

        if (request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase) &&
            request.Path.Equals("/mobile/packet", StringComparison.OrdinalIgnoreCase))
        {
            var packetRequest = DeserializeBody<MobilePacketRequest>(request);
            return QueuePacketRequest(packetRequest, trustedDevice);
        }

        if (request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase) &&
            request.Path.Equals("/mobile/capture", StringComparison.OrdinalIgnoreCase))
        {
            var captureRequest = DeserializeBody<MobileCaptureRequest>(request);
            return await SaveCaptureRequestAsync(captureRequest, trustedDevice, cancellationToken);
        }

        if (request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase) &&
            request.Path.Equals("/mobile/chart-photo", StringComparison.OrdinalIgnoreCase))
        {
            var photoRequest = DeserializeBody<MobileChartPhotoRequest>(request);
            return await GetOrSetChartPhotoAsync(photoRequest, trustedDevice, cancellationToken);
        }

        if (request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase) &&
            request.Path.Equals("/mobile/offline-item", StringComparison.OrdinalIgnoreCase))
        {
            var offlineRequest = DeserializeBody<MobileOfflineItemRequest>(request);
            return await AcceptOfflineItemAsync(offlineRequest, trustedDevice, cancellationToken);
        }

        return ApiResponse.Json(HttpStatusCode.NotFound, new
        {
            status = "not_found",
            message = "Unknown VitaMR mobile endpoint."
        });
    }

    private ApiResponse StartPairing()
    {
        var code = RandomNumberGenerator.GetInt32(100000, 999999).ToString("000000");
        var session = new PairingSession(code, DateTimeOffset.Now.AddMinutes(10));

        lock (_pairingLock)
        {
            _pairingSession = session;
        }

        return ApiResponse.Json(HttpStatusCode.OK, new MobilePairStartResponse
        {
            PairingCode = code,
            ExpiresAt = session.ExpiresAt
        });
    }

    private ApiResponse CompletePairing(MobilePairCompleteRequest request)
    {
        PairingSession? session;
        lock (_pairingLock)
        {
            session = _pairingSession;
        }

        if (session is null ||
            session.ExpiresAt < DateTimeOffset.Now ||
            !session.Code.Equals(request.PairingCode.Trim(), StringComparison.Ordinal))
        {
            return ApiResponse.Json(HttpStatusCode.Forbidden, new
            {
                status = "pairing_failed",
                message = "Pairing code is missing, expired, or incorrect."
            });
        }

        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var device = new MobileTrustedDevice
        {
            DeviceId = Guid.NewGuid().ToString("N"),
            DeviceName = string.IsNullOrWhiteSpace(request.DeviceName)
                ? "Android phone"
                : request.DeviceName.Trim(),
            TokenHash = HashToken(token),
            PairedAt = DateTimeOffset.Now,
            LastSeenAt = DateTimeOffset.Now
        };

        var store = LoadTrustedDevices();
        store.Devices.RemoveAll(existing => existing.DeviceName.Equals(device.DeviceName, StringComparison.OrdinalIgnoreCase));
        store.Devices.Add(device);
        SaveTrustedDevices(store);

        lock (_pairingLock)
        {
            _pairingSession = null;
        }

        return ApiResponse.Json(HttpStatusCode.OK, new MobilePairCompleteResponse
        {
            DeviceId = device.DeviceId,
            DeviceName = device.DeviceName,
            Token = token
        });
    }

    private async Task<ApiResponse> AcceptMobileChatAsync(
        MobileChatRequest request,
        MobileTrustedDevice device,
        CancellationToken cancellationToken)
    {
        var requestId = $"MOB-{DateTimeOffset.Now:yyyyMMddHHmmss}-{RandomNumberGenerator.GetInt32(1000, 9999)}";
        var message = string.IsNullOrWhiteSpace(request.Message)
            ? string.Empty
            : request.Message.Trim();

        AppendMobileQueue("chat", requestId, device, request.ChartId, message);

        if (ChatHandler is not null)
        {
            var response = await ChatHandler(request, device, cancellationToken);
            if (string.IsNullOrWhiteSpace(response.RequestId))
            {
                response.RequestId = requestId;
            }

            return ApiResponse.Json(HttpStatusCode.OK, response);
        }

        return ApiResponse.Json(HttpStatusCode.Accepted, new MobileChatResponse
        {
            RequestId = requestId,
            Status = "accepted",
            Route = "desktop_orchestrator_pending",
            Reply = "VitaMR desktop received this mobile chat request. Full Dolly response bridging is the next desktop API slice; no chart write was made from the phone."
        });
    }

    private ApiResponse QueuePacketRequest(MobilePacketRequest request, MobileTrustedDevice device)
    {
        var packetId = $"PKT-{DateTimeOffset.Now:yyyyMMddHHmmss}-{RandomNumberGenerator.GetInt32(1000, 9999)}";
        var packetRequest = string.IsNullOrWhiteSpace(request.Request)
            ? "Mobile packet request"
            : request.Request.Trim();

        AppendMobileQueue("packet", packetId, device, request.ChartId, packetRequest);

        return ApiResponse.Json(HttpStatusCode.Accepted, new MobilePacketResponse
        {
            PacketId = packetId,
            Status = "queued",
            Message = "VitaMR desktop queued this offline packet request. Packet building will stay desktop-owned and sterile."
        });
    }

    private async Task<ApiResponse> BuildChartPacketAsync(
        MobileChartPacketRequest request,
        MobileTrustedDevice device,
        CancellationToken cancellationToken)
    {
        AppendMobileQueue("chart_packet", $"PKT-{DateTimeOffset.Now:yyyyMMddHHmmss}", device, request.ChartId, request.PacketType);

        if (ChartPacketHandler is null)
        {
            return ApiResponse.Json(HttpStatusCode.Accepted, new MobileChartPacketResponse
            {
                Status = "queued",
                ChartId = request.ChartId,
                PacketType = request.PacketType,
                Mode = request.Mode,
                Message = "VitaMR desktop queued this chart packet request."
            });
        }

        var response = await ChartPacketHandler(request, device, cancellationToken);
        return ApiResponse.Json(
            response.Status.Equals("ready", StringComparison.OrdinalIgnoreCase)
                ? HttpStatusCode.OK
                : HttpStatusCode.BadRequest,
            response);
    }

    private async Task<ApiResponse> BuildVitaMasteryAsync(
        MobileVitaMasteryRequest request,
        MobileTrustedDevice device,
        CancellationToken cancellationToken)
    {
        if (VitaMasteryHandler is null)
        {
            return ApiResponse.Json(HttpStatusCode.Accepted, new MobileVitaMasteryResponse
            {
                Status = "queued",
                ChartId = string.IsNullOrWhiteSpace(request.ChartId) ? _settings.ActiveChartId : request.ChartId,
                PatientDisplayName = _settings.ActivePatientDisplayName,
                SummaryLine = "Vita Mastery is not available from desktop yet."
            });
        }

        var response = await VitaMasteryHandler(request, device, cancellationToken);
        return ApiResponse.Json(
            response.Status.Equals("ready", StringComparison.OrdinalIgnoreCase)
                ? HttpStatusCode.OK
                : HttpStatusCode.BadRequest,
            response);
    }

    private async Task<ApiResponse> BuildDataHunterQuestAsync(
        MobileDataHunterQuestRequest request,
        MobileTrustedDevice device,
        CancellationToken cancellationToken)
    {
        if (DataHunterQuestHandler is null)
        {
            return ApiResponse.Json(HttpStatusCode.Accepted, new MobileChatResponse
            {
                Status = "queued",
                ActiveChartId = string.IsNullOrWhiteSpace(request.ChartId) ? _settings.ActiveChartId : request.ChartId,
                ActivePatientDisplayName = _settings.ActivePatientDisplayName,
                Route = "desktop_data_hunter_pending",
                Reply = "Data Hunter is not available from desktop yet."
            });
        }

        var response = await DataHunterQuestHandler(request, device, cancellationToken);
        return ApiResponse.Json(
            response.Status.Equals("answered", StringComparison.OrdinalIgnoreCase)
                ? HttpStatusCode.OK
                : HttpStatusCode.Accepted,
            response);
    }

    private async Task<ApiResponse> SaveCaptureRequestAsync(
        MobileCaptureRequest request,
        MobileTrustedDevice device,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Base64Data))
        {
            return ApiResponse.Json(HttpStatusCode.BadRequest, new
            {
                status = "missing_file",
                message = "Capture request did not include file data."
            });
        }

        byte[] fileBytes;
        try
        {
            fileBytes = Convert.FromBase64String(request.Base64Data);
        }
        catch (FormatException)
        {
            return ApiResponse.Json(HttpStatusCode.BadRequest, new
            {
                status = "invalid_file",
                message = "Capture file data was not valid base64."
            });
        }

        var captureId = $"CAP-{DateTimeOffset.Now:yyyyMMddHHmmss}-{RandomNumberGenerator.GetInt32(1000, 9999)}";
        var safeFileName = MakeSafeFileName(string.IsNullOrWhiteSpace(request.FileName)
            ? $"{captureId}.bin"
            : request.FileName.Trim());
        var captureDirectory = Path.Combine(_captureFolder, captureId);
        Directory.CreateDirectory(captureDirectory);
        var savedPath = Path.Combine(captureDirectory, safeFileName);
        File.WriteAllBytes(savedPath, fileBytes);

        AppendCaptureQueue(captureId, device, request, savedPath, fileBytes.Length);

        if (CaptureHandler is not null)
        {
            try
            {
                await CaptureHandler(request, device, savedPath, fileBytes.Length, captureId, cancellationToken);
            }
            catch
            {
            }
        }

        return ApiResponse.Json(HttpStatusCode.Accepted, new MobileCaptureResponse
        {
            CaptureId = captureId,
            Status = "queued",
            Message = string.IsNullOrWhiteSpace(request.Note)
                ? "Attachment received. Add a short message if you want Dolly to do something specific with it."
                : "Attachment received with your message."
        });
    }

    private async Task<ApiResponse> GetOrSetChartPhotoAsync(
        MobileChartPhotoRequest request,
        MobileTrustedDevice device,
        CancellationToken cancellationToken)
    {
        if (ChartPhotoHandler is null)
        {
            return ApiResponse.Json(HttpStatusCode.Accepted, new MobileChartPhotoResponse
            {
                Status = "queued",
                ChartId = request.ChartId,
                Message = "VitaMR desktop has not connected chart photo handling yet."
            });
        }

        var response = await ChartPhotoHandler(request, device, cancellationToken);
        return ApiResponse.Json(HttpStatusCode.OK, response);
    }

    private async Task<ApiResponse> AcceptOfflineItemAsync(
        MobileOfflineItemRequest request,
        MobileTrustedDevice device,
        CancellationToken cancellationToken)
    {
        AppendMobileQueue("offline_item", request.LocalId, device, request.ChartId, request.Note);

        if (OfflineItemHandler is not null)
        {
            var response = await OfflineItemHandler(request, device, cancellationToken);
            return ApiResponse.Json(HttpStatusCode.OK, response);
        }

        return ApiResponse.Json(HttpStatusCode.Accepted, new MobileOfflineItemResponse
        {
            Status = "received",
            Message = "VitaMR desktop received this saved phone item.",
            NeedsContext = true,
            ContextQuestion = "Should Dolly save only, review, summarize, or add this to the chart after confirmation?"
        });
    }

    private void AppendMobileQueue(string kind, string id, MobileTrustedDevice device, string chartId, string text)
    {
        var folder = Path.GetDirectoryName(_packetQueuePath);
        if (!string.IsNullOrWhiteSpace(folder))
        {
            Directory.CreateDirectory(folder);
        }

        var line = JsonSerializer.Serialize(new
        {
            kind,
            id,
            createdAt = DateTimeOffset.Now,
            deviceId = device.DeviceId,
            deviceName = device.DeviceName,
            chartId = string.IsNullOrWhiteSpace(chartId) ? _settings.ActiveChartId : chartId.Trim(),
            text
        }, JsonOptions);

        File.AppendAllLines(_packetQueuePath, [line]);
    }

    private void AppendCaptureQueue(
        string captureId,
        MobileTrustedDevice device,
        MobileCaptureRequest request,
        string savedPath,
        int byteCount)
    {
        var folder = Path.GetDirectoryName(_captureQueuePath);
        if (!string.IsNullOrWhiteSpace(folder))
        {
            Directory.CreateDirectory(folder);
        }

        var line = JsonSerializer.Serialize(new
        {
            kind = "capture",
            captureId,
            createdAt = DateTimeOffset.Now,
            deviceId = device.DeviceId,
            deviceName = device.DeviceName,
            chartId = string.IsNullOrWhiteSpace(request.ChartId) ? _settings.ActiveChartId : request.ChartId.Trim(),
            note = request.Note?.Trim() ?? string.Empty,
            captureType = request.CaptureType?.Trim() ?? "photo",
            fileName = Path.GetFileName(savedPath),
            contentType = request.ContentType?.Trim() ?? "application/octet-stream",
            savedPath,
            byteCount
        }, JsonOptions);

        File.AppendAllLines(_captureQueuePath, [line]);
    }

    private static string MakeSafeFileName(string fileName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var safe = new string(fileName.Select(character => invalid.Contains(character) ? '_' : character).ToArray());
        return string.IsNullOrWhiteSpace(safe) ? "capture.bin" : safe;
    }

    private MobileTrustedDevice? Authenticate(ApiRequest request)
    {
        if (!request.Headers.TryGetValue("authorization", out var authorization) ||
            !authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var token = authorization["Bearer ".Length..].Trim();
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var tokenHash = HashToken(token);
        return LoadTrustedDevices().Devices
            .FirstOrDefault(device => device.TokenHash.Equals(tokenHash, StringComparison.Ordinal));
    }

    private MobileTrustedDeviceStore LoadTrustedDevices()
    {
        try
        {
            if (!File.Exists(_deviceStorePath))
            {
                return new MobileTrustedDeviceStore();
            }

            var json = File.ReadAllText(_deviceStorePath);
            return JsonSerializer.Deserialize<MobileTrustedDeviceStore>(json, JsonOptions) ?? new MobileTrustedDeviceStore();
        }
        catch
        {
            return new MobileTrustedDeviceStore();
        }
    }

    private void SaveTrustedDevices(MobileTrustedDeviceStore store)
    {
        var folder = Path.GetDirectoryName(_deviceStorePath);
        if (!string.IsNullOrWhiteSpace(folder))
        {
            Directory.CreateDirectory(folder);
        }

        File.WriteAllText(_deviceStorePath, JsonSerializer.Serialize(store, JsonOptions));
    }

    private static MobileTrustedDeviceStore UpdateTrustedDevice(MobileTrustedDeviceStore store, MobileTrustedDevice updated)
    {
        var index = store.Devices.FindIndex(device => device.DeviceId.Equals(updated.DeviceId, StringComparison.Ordinal));
        if (index >= 0)
        {
            store.Devices[index] = updated;
        }

        return store;
    }

    private static string HashToken(string token)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(hash);
    }

    private static T DeserializeBody<T>(ApiRequest request) where T : new()
    {
        if (string.IsNullOrWhiteSpace(request.Body))
        {
            return new T();
        }

        return JsonSerializer.Deserialize<T>(request.Body, JsonOptions) ?? new T();
    }

    private static async Task<ApiRequest> ReadRequestAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(8192);
        try
        {
            using var memory = new MemoryStream();
            int headerEnd = -1;
            while (headerEnd < 0)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
                if (read == 0)
                {
                    throw new InvalidDataException("Request ended before headers were complete.");
                }

                memory.Write(buffer, 0, read);
                headerEnd = FindHeaderEnd(memory.GetBuffer(), (int)memory.Length);
                if (memory.Length > 1024 * 1024)
                {
                    throw new InvalidDataException("Request headers are too large.");
                }
            }

            var bytes = memory.ToArray();
            var headerText = Encoding.UTF8.GetString(bytes, 0, headerEnd);
            var lines = headerText.Split(["\r\n"], StringSplitOptions.None);
            var requestLine = lines[0].Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
            if (requestLine.Length < 2)
            {
                throw new InvalidDataException("Invalid request line.");
            }

            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in lines.Skip(1))
            {
                var separator = line.IndexOf(':', StringComparison.Ordinal);
                if (separator <= 0)
                {
                    continue;
                }

                headers[line[..separator].Trim().ToLowerInvariant()] = line[(separator + 1)..].Trim();
            }

            var bodyStart = headerEnd + 4;
            var contentLength = headers.TryGetValue("content-length", out var lengthValue) &&
                                int.TryParse(lengthValue, out var parsedLength)
                ? parsedLength
                : 0;

            using var body = new MemoryStream();
            if (bytes.Length > bodyStart)
            {
                body.Write(bytes, bodyStart, Math.Min(bytes.Length - bodyStart, contentLength));
            }

            while (body.Length < contentLength)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, contentLength - (int)body.Length)), cancellationToken);
                if (read == 0)
                {
                    break;
                }

                body.Write(buffer, 0, read);
            }

            var path = requestLine[1].Split('?', 2)[0];
            return new ApiRequest(requestLine[0], path, headers, Encoding.UTF8.GetString(body.ToArray()));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static int FindHeaderEnd(byte[] buffer, int length)
    {
        for (var index = 0; index <= length - 4; index++)
        {
            if (buffer[index] == '\r' &&
                buffer[index + 1] == '\n' &&
                buffer[index + 2] == '\r' &&
                buffer[index + 3] == '\n')
            {
                return index;
            }
        }

        return -1;
    }

    private static async Task WriteResponseAsync(NetworkStream stream, ApiResponse response, CancellationToken cancellationToken)
    {
        var bodyBytes = Encoding.UTF8.GetBytes(response.Body);
        var header = $"HTTP/1.1 {(int)response.StatusCode} {response.StatusCode}\r\n" +
                     "Content-Type: application/json; charset=utf-8\r\n" +
                     $"Content-Length: {bodyBytes.Length}\r\n" +
                     "Connection: close\r\n" +
                     "\r\n";

        var headerBytes = Encoding.UTF8.GetBytes(header);
        await stream.WriteAsync(headerBytes, cancellationToken);
        await stream.WriteAsync(bodyBytes, cancellationToken);
    }

    private sealed record PairingSession(string Code, DateTimeOffset ExpiresAt);

    private sealed record ApiRequest(
        string Method,
        string Path,
        IReadOnlyDictionary<string, string> Headers,
        string Body);

    private sealed record ApiResponse(HttpStatusCode StatusCode, string Body)
    {
        public static ApiResponse Json(HttpStatusCode statusCode, object body)
        {
            return new ApiResponse(statusCode, JsonSerializer.Serialize(body, JsonOptions));
        }
    }
}
