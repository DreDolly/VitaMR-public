using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using VitaMR.Models;

namespace VitaMR.Services;

public sealed class OllamaLocalModelWarmupService : ILocalModelWarmupService
{
    private static readonly string[] PromptFiles =
    [
        "Local_Model_Privacy_Reviewer.md",
        "Local_Image_Document_Classifier.md",
        "Local_Omnibox_Preflight.md",
        "Local_Dolly_Vault_Chat.md"
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly HttpClient _httpClient;
    private readonly IPromptCacheService _promptCacheService;

    public OllamaLocalModelWarmupService(IPromptCacheService promptCacheService)
    {
        _promptCacheService = promptCacheService;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(45)
        };
    }

    public async Task<LocalModelWarmupResult> WarmAsync(
        string endpoint,
        string modelName,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var attemptedStartup = false;

        try
        {
            _promptCacheService.PreloadPrompts(PromptFiles);
            await EnsureLocalOllamaIsRunningAsync(endpoint, cancellationToken);

            var requestBody = new
            {
                model = string.IsNullOrWhiteSpace(modelName) ? "gemma4:latest" : modelName.Trim(),
                prompt = "VitaMR startup warmup. Reply with READY.",
                stream = false,
                keep_alive = "30s",
                options = new
                {
                    temperature = 0,
                    num_predict = 1
                }
            };

            using var request = new StringContent(
                JsonSerializer.Serialize(requestBody, JsonOptions),
                Encoding.UTF8,
                "application/json");

            using var response = await _httpClient.PostAsync(LaptopWorkerProtocol.BuildWarmupUri(endpoint), request, cancellationToken);
            stopwatch.Stop();

            if (!response.IsSuccessStatusCode)
            {
                return new LocalModelWarmupResult
                {
                    IsAvailable = false,
                    Status = "LOCAL_MODEL_WARMUP_UNAVAILABLE",
                    Message = $"Gemma warmup returned {(int)response.StatusCode}.",
                    Elapsed = stopwatch.Elapsed
                };
            }

            return new LocalModelWarmupResult
            {
                IsAvailable = true,
                Status = "LOCAL_MODEL_READY",
                Message = $"Gemma ready in {stopwatch.Elapsed.TotalSeconds:0.0}s.",
                Elapsed = stopwatch.Elapsed
            };
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or UriFormatException)
        {
            if (!attemptedStartup && IsLocalOllamaEndpoint(endpoint))
            {
                attemptedStartup = true;
                var startupSucceeded = await TryStartLocalOllamaAsync(cancellationToken);
                if (startupSucceeded)
                {
                    stopwatch.Restart();
                    return await WarmAsync(endpoint, modelName, cancellationToken);
                }
            }

            stopwatch.Stop();

            return new LocalModelWarmupResult
            {
                IsAvailable = false,
                Status = "LOCAL_MODEL_WARMUP_UNAVAILABLE",
                Message = exception is TaskCanceledException
                    ? "Gemma warmup timed out."
                    : "Gemma warmup could not connect.",
                Elapsed = stopwatch.Elapsed
            };
        }
    }

    private async Task EnsureLocalOllamaIsRunningAsync(string endpoint, CancellationToken cancellationToken)
    {
        if (!IsLocalOllamaEndpoint(endpoint))
        {
            return;
        }

        if (await CanReachOllamaAsync(endpoint, cancellationToken))
        {
            return;
        }

        await TryStartLocalOllamaAsync(cancellationToken);
    }

    private async Task<bool> CanReachOllamaAsync(string endpoint, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.GetAsync(new Uri(new Uri(endpoint.TrimEnd('/') + "/"), "api/tags"), cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or UriFormatException)
        {
            return false;
        }
    }

    private async Task<bool> TryStartLocalOllamaAsync(CancellationToken cancellationToken)
    {
        var executablePath = FindOllamaExecutable();
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return false;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = "serve",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            });
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return false;
        }

        var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
        while (DateTimeOffset.UtcNow < deadline && !cancellationToken.IsCancellationRequested)
        {
            if (await CanReachOllamaAsync("http://localhost:11434", cancellationToken))
            {
                return true;
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }
            catch (TaskCanceledException)
            {
                return false;
            }
        }

        return false;
    }

    private static string FindOllamaExecutable()
    {
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Ollama", "ollama.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Ollama", "ollama.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Ollama", "ollama.exe")
        };

        return candidates.FirstOrDefault(File.Exists) ?? string.Empty;
    }

    private static bool IsLocalOllamaEndpoint(string endpoint)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return uri.Port == 11434 &&
            (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
             uri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
             uri.Host.Equals("::1", StringComparison.OrdinalIgnoreCase));
    }
}
