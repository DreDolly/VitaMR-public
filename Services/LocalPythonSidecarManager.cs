using System.Diagnostics;
using System.IO;
using System.Net.Http;

namespace VitaMR.Services;

public sealed class LocalPythonSidecarManager : IDisposable
{
    private readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(2)
    };
    private readonly List<Process> _ownedProcesses = [];

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        foreach (var sidecar in BuildSidecars())
        {
            if (await IsRespondingAsync(sidecar.HealthUri, cancellationToken))
            {
                continue;
            }

            var root = FindSidecarRoot(sidecar.RelativePath);
            if (string.IsNullOrWhiteSpace(root))
            {
                continue;
            }

            await EnsurePythonEnvironmentAsync(root, sidecar, cancellationToken);
            StartSidecarProcess(root, sidecar);
            await WaitForReadinessAsync(sidecar.HealthUri, cancellationToken);
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();

        foreach (var process in _ownedProcesses)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch (InvalidOperationException)
            {
            }
            catch (System.ComponentModel.Win32Exception)
            {
            }

            process.Dispose();
        }
    }

    private static IReadOnlyList<SidecarDefinition> BuildSidecars()
    {
        return
        [
            new(
                "local_privacy",
                "Services\\local_privacy",
                8001,
                new Uri("http://127.0.0.1:8001/health"),
                "import fastapi, uvicorn, spacy, presidio_analyzer, presidio_anonymizer"),
            new(
                "local_ocr",
                "Services\\local_ocr",
                8000,
                new Uri("http://127.0.0.1:8000/health"),
                "import fastapi, uvicorn, fitz, paddleocr, multipart")
        ];
    }

    private async Task<bool> IsRespondingAsync(Uri healthUri, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.GetAsync(healthUri, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (TaskCanceledException)
        {
            return false;
        }
    }

    private static string FindSidecarRoot(string relativePath)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, relativePath);
            if (Directory.Exists(candidate) &&
                File.Exists(Path.Combine(candidate, "app.py")) &&
                File.Exists(Path.Combine(candidate, "requirements.txt")))
            {
                return candidate;
            }

            current = current.Parent;
        }

        return string.Empty;
    }

    private async Task WaitForReadinessAsync(Uri healthUri, CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(90);
        while (DateTimeOffset.UtcNow < deadline && !cancellationToken.IsCancellationRequested)
        {
            if (await IsRespondingAsync(healthUri, cancellationToken))
            {
                return;
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }
            catch (TaskCanceledException)
            {
                return;
            }
        }
    }

    private void StartSidecarProcess(string workingDirectory, SidecarDefinition sidecar)
    {
        AppendLog(sidecar, $"Starting uvicorn on port {sidecar.Port}.");

        var startInfo = new ProcessStartInfo
        {
            FileName = GetVenvPythonPath(workingDirectory),
            Arguments = $"-m uvicorn app:app --host 127.0.0.1 --port {sidecar.Port}",
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        var process = Process.Start(startInfo);
        if (process is not null)
        {
            process.OutputDataReceived += (_, args) => AppendLog(sidecar, args.Data);
            process.ErrorDataReceived += (_, args) => AppendLog(sidecar, args.Data);
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            _ownedProcesses.Add(process);
        }
    }

    private static async Task EnsurePythonEnvironmentAsync(
        string workingDirectory,
        SidecarDefinition sidecar,
        CancellationToken cancellationToken)
    {
        AppendLog(sidecar, "Preparing Python environment.");

        var pythonPath = GetVenvPythonPath(workingDirectory);
        if (!File.Exists(pythonPath))
        {
            await RunProcessAsync(
                "py",
                "-3 -m venv .venv",
                workingDirectory,
                sidecar,
                TimeSpan.FromMinutes(2),
                cancellationToken);
        }

        await RunProcessAsync(
            pythonPath,
            "-m ensurepip --upgrade",
            workingDirectory,
            sidecar,
            TimeSpan.FromMinutes(2),
            cancellationToken);

        var probe = await RunProcessAsync(
            pythonPath,
            $"-c \"{sidecar.DependencyProbe}\"",
            workingDirectory,
            sidecar,
            TimeSpan.FromSeconds(60),
            cancellationToken);

        if (probe == 0)
        {
            AppendLog(sidecar, "Python dependencies ready.");
            return;
        }

        AppendLog(sidecar, "Installing missing Python dependencies.");
        await RunProcessAsync(
            pythonPath,
            "-m pip install --upgrade pip",
            workingDirectory,
            sidecar,
            TimeSpan.FromMinutes(5),
            cancellationToken);
        await RunProcessAsync(
            pythonPath,
            "-m pip install -r requirements.txt",
            workingDirectory,
            sidecar,
            TimeSpan.FromMinutes(15),
            cancellationToken);
    }

    private static async Task<int> RunProcessAsync(
        string fileName,
        string arguments,
        string workingDirectory,
        SidecarDefinition sidecar,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        try
        {
            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(timeout);

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    WorkingDirectory = workingDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                },
                EnableRaisingEvents = true
            };

            process.OutputDataReceived += (_, args) => AppendLog(sidecar, args.Data);
            process.ErrorDataReceived += (_, args) => AppendLog(sidecar, args.Data);

            AppendLog(sidecar, $"> {fileName} {arguments}");
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync(timeoutSource.Token);
            AppendLog(sidecar, $"Exit code: {process.ExitCode}");
            return process.ExitCode;
        }
        catch (OperationCanceledException)
        {
            AppendLog(sidecar, $"Timed out after {timeout.TotalSeconds:0}s: {fileName} {arguments}");
            return -1;
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            AppendLog(sidecar, $"Process failed: {exception.Message}");
            return -1;
        }
    }

    private static string GetVenvPythonPath(string workingDirectory)
    {
        return Path.Combine(workingDirectory, ".venv", "Scripts", "python.exe");
    }

    private static void AppendLog(SidecarDefinition sidecar, string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        try
        {
            var logDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "VitaMR",
                "Logs");
            Directory.CreateDirectory(logDirectory);
            var logPath = Path.Combine(logDirectory, $"{sidecar.Name}.log");
            File.AppendAllText(logPath, $"[{DateTimeOffset.Now:O}] {message}{Environment.NewLine}");
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private sealed record SidecarDefinition(
        string Name,
        string RelativePath,
        int Port,
        Uri HealthUri,
        string DependencyProbe);
}
