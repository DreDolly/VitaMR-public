using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using VitaMR.Models;

namespace VitaMR.Services;

public sealed class GeminiEncounterWikiRewriteService : IGeminiEncounterWikiRewriteService
{
    private const string RewritePromptFileName = "Gemini_Encounter_Wiki_Rewrite.md";
    private const string VerifyPromptFileName = "Gemini_Encounter_Wiki_Rewrite_Verify.md";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly JsonSerializerOptions ResponseJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new FlexibleStringListJsonConverter() }
    };

    private readonly HttpClient _httpClient;
    private readonly IGeminiApiKeyStore _apiKeyStore;
    private readonly IPromptCacheService _promptCacheService;

    public GeminiEncounterWikiRewriteService(
        IGeminiApiKeyStore apiKeyStore,
        IPromptCacheService promptCacheService)
    {
        _apiKeyStore = apiKeyStore;
        _promptCacheService = promptCacheService;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(120)
        };
    }

    public async Task<WikiRewriteApplyResult> RewriteEncounterFilesAsync(
        bool isEnabled,
        string modelName,
        string vaultRoot,
        ChartContext chartContext,
        EncounterExtractionResult extraction,
        EncounterNodeWriteResult writeResult,
        CancellationToken cancellationToken = default)
    {
        var result = new WikiRewriteApplyResult
        {
            WasAttempted = true,
            Status = "ENCOUNTER_WIKI_REWRITE_STARTED"
        };

        if (!isEnabled)
        {
            result.Status = "ENCOUNTER_WIKI_REWRITE_DISABLED";
            return result;
        }

        var apiKey = _apiKeyStore.Load().ThinkingApiKey;

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            result.Status = "ENCOUNTER_WIKI_REWRITE_KEY_MISSING";
            return result;
        }

        var targets = BuildTargets(vaultRoot, chartContext, writeResult);

        if (targets.Count == 0)
        {
            result.Status = "ENCOUNTER_WIKI_REWRITE_NO_TARGETS";
            return result;
        }

        var rewrite = await RequestRewriteAsync(modelName, apiKey, chartContext, extraction, targets, cancellationToken);
        result.Status = rewrite.Status;
        result.Summary = rewrite.Summary;

        if (!rewrite.WasAvailable)
        {
            result.Messages.Add($"Encounter wiki rewrite unavailable: {rewrite.Status}.");
            return result;
        }

        var acceptedFiles = FilterSafeRewrites(vaultRoot, chartContext, targets, rewrite.Files, result.Messages);

        if (acceptedFiles.Count == 0)
        {
            result.Status = "ENCOUNTER_WIKI_REWRITE_NO_SAFE_FILES";
            result.Messages.Add("Gemini returned no safe encounter wiki rewrite files.");
            return result;
        }

        var verify = await RequestVerificationAsync(modelName, apiKey, chartContext, extraction, targets, acceptedFiles, cancellationToken);
        result.VerificationPassed = verify.Approved;

        if (!verify.Approved)
        {
            result.Status = verify.WasAvailable
                ? "ENCOUNTER_WIKI_REWRITE_VERIFICATION_REJECTED"
                : verify.Status;
            result.Messages.Add(string.IsNullOrWhiteSpace(verify.Reason)
                ? "The encounter wiki rewrite did not pass verification."
                : verify.Reason);
            result.Messages.AddRange(verify.Issues.Where(issue => !string.IsNullOrWhiteSpace(issue)));
            return result;
        }

        foreach (var file in acceptedFiles)
        {
            var absolutePath = GetSafeWikiPath(vaultRoot, chartContext, file.RelativePath);

            if (absolutePath is null)
            {
                result.Messages.Add($"Skipped unsafe encounter rewrite path: {file.RelativePath}");
                continue;
            }

            File.WriteAllText(absolutePath, NormalizeMarkdown(file.Content), Encoding.UTF8);
            result.UpdatedFiles.Add(absolutePath);
        }

        result.WasApplied = result.UpdatedFiles.Count > 0;
        result.Status = result.WasApplied
            ? "ENCOUNTER_WIKI_REWRITE_APPLIED"
            : "ENCOUNTER_WIKI_REWRITE_NOT_APPLIED";
        result.Messages.Add($"Encounter wiki rewrite verified: {verify.Reason}");
        return result;
    }

    private async Task<GeminiWikiRewriteResult> RequestRewriteAsync(
        string modelName,
        string apiKey,
        ChartContext chartContext,
        EncounterExtractionResult extraction,
        IReadOnlyList<EncounterRewriteTarget> targets,
        CancellationToken cancellationToken)
    {
        var result = new GeminiWikiRewriteResult
        {
            WasAttempted = true,
            Status = "ENCOUNTER_WIKI_REWRITE_REQUEST_STARTED"
        };

        var prompt = BuildRewritePrompt(chartContext, extraction, targets);
        var requestBody = BuildJsonRequest(prompt);

        try
        {
            using var request = new StringContent(JsonSerializer.Serialize(requestBody, JsonOptions), Encoding.UTF8, "application/json");
            using var response = await _httpClient.PostAsync(BuildGenerateUri(modelName, apiKey), request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            result.RawResponse = body;

            if (!response.IsSuccessStatusCode)
            {
                result.Status = $"ENCOUNTER_WIKI_REWRITE_HTTP_{(int)response.StatusCode}";
                return result;
            }

            var json = ExtractText(body);

            if (string.IsNullOrWhiteSpace(json))
            {
                result.Status = "ENCOUNTER_WIKI_REWRITE_EMPTY";
                return result;
            }

            var parsed = JsonSerializer.Deserialize<GeminiWikiRewriteResult>(json, ResponseJsonOptions);

            if (parsed is null)
            {
                result.Status = "ENCOUNTER_WIKI_REWRITE_INVALID_JSON";
                return result;
            }

            parsed.WasAttempted = true;
            parsed.WasAvailable = true;
            parsed.Status = string.IsNullOrWhiteSpace(parsed.Status) ? "ENCOUNTER_WIKI_REWRITE_READY" : parsed.Status;
            parsed.RawResponse = body;
            return parsed;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or UriFormatException or JsonException)
        {
            result.Status = exception is TaskCanceledException
                ? "ENCOUNTER_WIKI_REWRITE_TIMEOUT"
                : "ENCOUNTER_WIKI_REWRITE_UNAVAILABLE";
            result.RawResponse = exception.Message;
            return result;
        }
    }

    private async Task<GeminiWikiRewriteVerificationResult> RequestVerificationAsync(
        string modelName,
        string apiKey,
        ChartContext chartContext,
        EncounterExtractionResult extraction,
        IReadOnlyList<EncounterRewriteTarget> originalTargets,
        IReadOnlyList<WikiFileRewrite> rewrittenFiles,
        CancellationToken cancellationToken)
    {
        var result = new GeminiWikiRewriteVerificationResult
        {
            WasAttempted = true,
            Status = "ENCOUNTER_WIKI_REWRITE_VERIFY_STARTED"
        };

        var prompt = BuildVerifyPrompt(chartContext, extraction, originalTargets, rewrittenFiles);
        var requestBody = BuildJsonRequest(prompt);

        try
        {
            using var request = new StringContent(JsonSerializer.Serialize(requestBody, JsonOptions), Encoding.UTF8, "application/json");
            using var response = await _httpClient.PostAsync(BuildGenerateUri(modelName, apiKey), request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            result.RawResponse = body;

            if (!response.IsSuccessStatusCode)
            {
                result.Status = $"ENCOUNTER_WIKI_REWRITE_VERIFY_HTTP_{(int)response.StatusCode}";
                return result;
            }

            var json = ExtractText(body);

            if (string.IsNullOrWhiteSpace(json))
            {
                result.Status = "ENCOUNTER_WIKI_REWRITE_VERIFY_EMPTY";
                return result;
            }

            var parsed = JsonSerializer.Deserialize<GeminiWikiRewriteVerificationResult>(json, ResponseJsonOptions);

            if (parsed is null)
            {
                result.Status = "ENCOUNTER_WIKI_REWRITE_VERIFY_INVALID_JSON";
                return result;
            }

            parsed.WasAttempted = true;
            parsed.WasAvailable = true;
            parsed.Status = string.IsNullOrWhiteSpace(parsed.Status) ? "ENCOUNTER_WIKI_REWRITE_VERIFY_READY" : parsed.Status;
            parsed.RawResponse = body;
            return parsed;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or UriFormatException or JsonException)
        {
            result.Status = exception is TaskCanceledException
                ? "ENCOUNTER_WIKI_REWRITE_VERIFY_TIMEOUT"
                : "ENCOUNTER_WIKI_REWRITE_VERIFY_UNAVAILABLE";
            result.RawResponse = exception.Message;
            return result;
        }
    }

    private string BuildRewritePrompt(
        ChartContext chartContext,
        EncounterExtractionResult extraction,
        IReadOnlyList<EncounterRewriteTarget> targets)
    {
        var instructions = _promptCacheService.GetPrompt(
            RewritePromptFileName,
            "Rewrite the supplied encounter wiki files into clean markdown while preserving all clinical facts. Return JSON only.");

        return
            $"{instructions}\n\n" +
            $"Chart id: {chartContext.ChartId}\n" +
            $"Encounter document type: {extraction.DocumentType}\n" +
            $"Encounter date: {extraction.DateOfService}\n\n" +
            $"Structured extraction JSON:\n{JsonSerializer.Serialize(extraction, JsonOptions)}\n\n" +
            $"Current wiki files:\n{FormatTargets(targets)}\n\n" +
            "Return complete replacement markdown for each file that must change.";
    }

    private string BuildVerifyPrompt(
        ChartContext chartContext,
        EncounterExtractionResult extraction,
        IReadOnlyList<EncounterRewriteTarget> originalTargets,
        IReadOnlyList<WikiFileRewrite> rewrittenFiles)
    {
        var instructions = _promptCacheService.GetPrompt(
            VerifyPromptFileName,
            "Verify that rewritten encounter wiki files preserve all facts while improving markdown readability. Return JSON only.");

        return
            $"{instructions}\n\n" +
            $"Chart id: {chartContext.ChartId}\n" +
            $"Encounter document type: {extraction.DocumentType}\n" +
            $"Encounter date: {extraction.DateOfService}\n\n" +
            $"Structured extraction JSON:\n{JsonSerializer.Serialize(extraction, JsonOptions)}\n\n" +
            $"Original files:\n{FormatTargets(originalTargets)}\n\n" +
            $"Rewritten files:\n{FormatRewrites(rewrittenFiles)}\n\n" +
            "Return verification JSON now.";
    }

    private static IReadOnlyList<EncounterRewriteTarget> BuildTargets(
        string vaultRoot,
        ChartContext chartContext,
        EncounterNodeWriteResult writeResult)
    {
        var targets = new List<EncounterRewriteTarget>();

        foreach (var path in EnumeratePaths(writeResult))
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                continue;
            }

            var relativePath = GetRelativeWikiPath(vaultRoot, chartContext, path);

            if (relativePath is null)
            {
                continue;
            }

            targets.Add(new EncounterRewriteTarget(relativePath, File.ReadAllText(path)));
        }

        return targets
            .GroupBy(target => target.RelativePath, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }

    private static IEnumerable<string> EnumeratePaths(EncounterNodeWriteResult writeResult)
    {
        yield return writeResult.EncounterNodePath;
        yield return writeResult.IndexPath;
        yield return writeResult.TimelinePath;
        yield return writeResult.CareGapsPath;
        yield return writeResult.ConflictsPath;
        yield return writeResult.EmergencyCardPath;

        foreach (var topicPath in writeResult.TopicPagePaths)
        {
            yield return topicPath;
        }
    }

    private static IReadOnlyList<WikiFileRewrite> FilterSafeRewrites(
        string vaultRoot,
        ChartContext chartContext,
        IReadOnlyList<EncounterRewriteTarget> targets,
        IEnumerable<WikiFileRewrite> rewrites,
        ICollection<string> messages)
    {
        var allowedPaths = targets.Select(target => target.RelativePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var acceptedFiles = new List<WikiFileRewrite>();

        foreach (var rewrite in rewrites)
        {
            var relativePath = NormalizeRelativePath(rewrite.RelativePath);

            if (string.IsNullOrWhiteSpace(relativePath) ||
                !allowedPaths.Contains(relativePath) ||
                GetSafeWikiPath(vaultRoot, chartContext, relativePath) is null)
            {
                messages.Add($"Rejected unexpected encounter rewrite path: {rewrite.RelativePath}");
                continue;
            }

            if (string.IsNullOrWhiteSpace(rewrite.Content))
            {
                messages.Add($"Rejected empty encounter rewrite content: {relativePath}");
                continue;
            }

            acceptedFiles.Add(new WikiFileRewrite
            {
                RelativePath = relativePath,
                Content = rewrite.Content
            });
        }

        return acceptedFiles;
    }

    private static string? GetRelativeWikiPath(string vaultRoot, ChartContext chartContext, string absolutePath)
    {
        var chartRoot = Path.GetFullPath(Path.Combine(vaultRoot, chartContext.ChartFolderName));
        var safePath = Path.GetFullPath(absolutePath);

        if (!safePath.StartsWith(chartRoot, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return NormalizeRelativePath(Path.GetRelativePath(chartRoot, safePath));
    }

    private static string? GetSafeWikiPath(string vaultRoot, ChartContext chartContext, string relativePath)
    {
        var normalizedRelativePath = NormalizeRelativePath(relativePath);

        if (string.IsNullOrWhiteSpace(normalizedRelativePath) ||
            normalizedRelativePath.Contains("..", StringComparison.Ordinal) ||
            !normalizedRelativePath.StartsWith("wiki/", StringComparison.OrdinalIgnoreCase) ||
            !normalizedRelativePath.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var chartRoot = Path.GetFullPath(Path.Combine(vaultRoot, chartContext.ChartFolderName));
        var wikiRoot = Path.GetFullPath(Path.Combine(chartRoot, "wiki"));
        var safeWikiRoot = wikiRoot.EndsWith(Path.DirectorySeparatorChar) ? wikiRoot : wikiRoot + Path.DirectorySeparatorChar;
        var absolutePath = Path.GetFullPath(Path.Combine(chartRoot, normalizedRelativePath.Replace('/', Path.DirectorySeparatorChar)));

        return absolutePath.StartsWith(safeWikiRoot, StringComparison.OrdinalIgnoreCase)
            ? absolutePath
            : null;
    }

    private static string FormatTargets(IEnumerable<EncounterRewriteTarget> targets)
    {
        return string.Join(
            "\n\n",
            targets.Select(target =>
                $"--- FILE: {target.RelativePath} ---\n```markdown\n{target.OriginalContent}\n```"));
    }

    private static string FormatRewrites(IEnumerable<WikiFileRewrite> rewrites)
    {
        return string.Join(
            "\n\n",
            rewrites.Select(rewrite =>
                $"--- FILE: {NormalizeRelativePath(rewrite.RelativePath)} ---\n```markdown\n{rewrite.Content}\n```"));
    }

    private static string NormalizeRelativePath(string relativePath)
    {
        return relativePath.Replace('\\', '/').Trim().TrimStart('/');
    }

    private static string NormalizeMarkdown(string markdown)
    {
        return markdown.Replace("\r\n", "\n").TrimEnd() + Environment.NewLine;
    }

    private static object BuildJsonRequest(string prompt)
    {
        return new
        {
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[]
                    {
                        new { text = prompt }
                    }
                }
            },
            generationConfig = new
            {
                temperature = 0.05,
                responseMimeType = "application/json"
            }
        };
    }

    private static Uri BuildGenerateUri(string modelName, string apiKey)
    {
        var safeModelName = string.IsNullOrWhiteSpace(modelName)
            ? "gemini-3-flash-preview"
            : modelName.Trim();

        return new Uri($"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(safeModelName)}:generateContent?key={Uri.EscapeDataString(apiKey)}");
    }

    private static string ExtractText(string body)
    {
        using var document = JsonDocument.Parse(body);

        if (!document.RootElement.TryGetProperty("candidates", out var candidates) ||
            candidates.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        foreach (var candidate in candidates.EnumerateArray())
        {
            if (!candidate.TryGetProperty("content", out var content) ||
                !content.TryGetProperty("parts", out var parts) ||
                parts.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var part in parts.EnumerateArray())
            {
                if (part.TryGetProperty("text", out var text) &&
                    text.ValueKind == JsonValueKind.String)
                {
                    return text.GetString() ?? string.Empty;
                }
            }
        }

        return string.Empty;
    }

    private sealed record EncounterRewriteTarget(string RelativePath, string OriginalContent);
}
