using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using VitaMR.Models;

namespace VitaMR.Services;

public sealed class GeminiWikiRewriteService : IGeminiWikiRewriteService
{
    private const string RewritePromptFileName = "Gemini_Wiki_Rewrite.md";
    private const string VerifyPromptFileName = "Gemini_Wiki_Rewrite_Verify.md";

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

    public GeminiWikiRewriteService(
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

    public async Task<WikiRewriteApplyResult> RewriteAndApplyAsync(
        bool isEnabled,
        string modelName,
        string vaultRoot,
        ChartContext chartContext,
        GeminiWikiPatchResult patch,
        IngestResult rawManualSource,
        string scrubbedUserText,
        string conversationContext,
        CancellationToken cancellationToken = default)
    {
        var result = new WikiRewriteApplyResult
        {
            WasAttempted = true,
            Status = "WIKI_REWRITE_STARTED"
        };

        var targets = BuildTargets(vaultRoot, chartContext, patch, rawManualSource);

        if (targets.Count == 0)
        {
            result.Status = "WIKI_REWRITE_NO_TARGETS";
            result.Messages.Add("No safe wiki files were selected for the LLM rewrite.");
            return result;
        }

        var rewrite = await RequestRewriteAsync(
            isEnabled,
            modelName,
            chartContext,
            patch,
            rawManualSource,
            scrubbedUserText,
            conversationContext,
            targets,
            cancellationToken);

        result.Status = rewrite.Status;
        result.Summary = rewrite.Summary;

        if (!rewrite.WasAvailable)
        {
            result.Messages.Add($"Gemini rewrite was not available: {rewrite.Status}.");
            return result;
        }

        var acceptedFiles = FilterSafeRewrites(vaultRoot, chartContext, targets, rewrite.Files, result.Messages);

        if (acceptedFiles.Count == 0)
        {
            result.Status = "WIKI_REWRITE_NO_SAFE_FILES";
            result.Messages.Add("Gemini returned no safe wiki rewrite files.");
            return result;
        }

        var verify = await RequestVerificationAsync(
            isEnabled,
            modelName,
            chartContext,
            patch,
            rawManualSource,
            scrubbedUserText,
            targets,
            acceptedFiles,
            cancellationToken);

        result.VerificationPassed = verify.Approved;

        if (!verify.Approved)
        {
            result.Status = verify.WasAvailable
                ? "WIKI_REWRITE_VERIFICATION_REJECTED"
                : verify.Status;
            result.Messages.Add(string.IsNullOrWhiteSpace(verify.Reason)
                ? "The second LLM verification did not approve the rewrite."
                : verify.Reason);
            result.Messages.AddRange(verify.Issues.Where(issue => !string.IsNullOrWhiteSpace(issue)));
            return result;
        }

        foreach (var file in acceptedFiles)
        {
            var absolutePath = GetSafeWikiPath(vaultRoot, chartContext, file.RelativePath);

            if (absolutePath is null)
            {
                result.Messages.Add($"Skipped unsafe rewrite path: {file.RelativePath}");
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);
            File.WriteAllText(absolutePath, NormalizeMarkdown(file.Content), Encoding.UTF8);
            result.UpdatedFiles.Add(absolutePath);
        }

        result.WasApplied = result.UpdatedFiles.Count > 0;
        result.Status = result.WasApplied
            ? "WIKI_REWRITE_APPLIED"
            : "WIKI_REWRITE_NOT_APPLIED";
        result.Messages.Add($"LLM rewrite verification approved: {verify.Reason}");
        return result;
    }

    private async Task<GeminiWikiRewriteResult> RequestRewriteAsync(
        bool isEnabled,
        string modelName,
        ChartContext chartContext,
        GeminiWikiPatchResult patch,
        IngestResult rawManualSource,
        string scrubbedUserText,
        string conversationContext,
        IReadOnlyList<WikiRewriteTarget> targets,
        CancellationToken cancellationToken)
    {
        var result = new GeminiWikiRewriteResult
        {
            WasAttempted = true,
            Status = "GEMINI_WIKI_REWRITE_STARTED"
        };

        if (!isEnabled)
        {
            result.Status = "GEMINI_WIKI_REWRITE_DISABLED";
            return result;
        }

        var apiKey = _apiKeyStore.Load().ThinkingApiKey;

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            result.Status = "GEMINI_WIKI_REWRITE_KEY_MISSING";
            return result;
        }

        var requestBody = BuildJsonRequest(BuildRewritePrompt(chartContext, patch, rawManualSource, scrubbedUserText, conversationContext, targets));

        try
        {
            using var request = new StringContent(
                JsonSerializer.Serialize(requestBody, JsonOptions),
                Encoding.UTF8,
                "application/json");
            using var response = await _httpClient.PostAsync(BuildGenerateUri(modelName, apiKey), request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            result.RawResponse = body;

            if (!response.IsSuccessStatusCode)
            {
                result.Status = $"GEMINI_WIKI_REWRITE_HTTP_{(int)response.StatusCode}";
                return result;
            }

            var json = ExtractText(body);

            if (string.IsNullOrWhiteSpace(json))
            {
                result.Status = "GEMINI_WIKI_REWRITE_EMPTY";
                return result;
            }

            var parsed = JsonSerializer.Deserialize<GeminiWikiRewriteResult>(json, ResponseJsonOptions);

            if (parsed is null)
            {
                result.Status = "GEMINI_WIKI_REWRITE_INVALID_JSON";
                return result;
            }

            parsed.WasAttempted = true;
            parsed.WasAvailable = true;
            parsed.Status = string.IsNullOrWhiteSpace(parsed.Status)
                ? "GEMINI_WIKI_REWRITE_READY"
                : parsed.Status;
            parsed.RawResponse = body;
            return parsed;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or UriFormatException or JsonException)
        {
            result.Status = exception is TaskCanceledException
                ? "GEMINI_WIKI_REWRITE_TIMEOUT"
                : "GEMINI_WIKI_REWRITE_UNAVAILABLE";
            result.RawResponse = exception.Message;
            return result;
        }
    }

    private async Task<GeminiWikiRewriteVerificationResult> RequestVerificationAsync(
        bool isEnabled,
        string modelName,
        ChartContext chartContext,
        GeminiWikiPatchResult patch,
        IngestResult rawManualSource,
        string scrubbedUserText,
        IReadOnlyList<WikiRewriteTarget> originalTargets,
        IReadOnlyList<WikiFileRewrite> rewrittenFiles,
        CancellationToken cancellationToken)
    {
        var result = new GeminiWikiRewriteVerificationResult
        {
            WasAttempted = true,
            Status = "GEMINI_WIKI_REWRITE_VERIFY_STARTED"
        };

        if (!isEnabled)
        {
            result.Status = "GEMINI_WIKI_REWRITE_VERIFY_DISABLED";
            return result;
        }

        var apiKey = _apiKeyStore.Load().ThinkingApiKey;

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            result.Status = "GEMINI_WIKI_REWRITE_VERIFY_KEY_MISSING";
            return result;
        }

        var requestBody = BuildJsonRequest(BuildVerifyPrompt(chartContext, patch, rawManualSource, scrubbedUserText, originalTargets, rewrittenFiles));

        try
        {
            using var request = new StringContent(
                JsonSerializer.Serialize(requestBody, JsonOptions),
                Encoding.UTF8,
                "application/json");
            using var response = await _httpClient.PostAsync(BuildGenerateUri(modelName, apiKey), request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            result.RawResponse = body;

            if (!response.IsSuccessStatusCode)
            {
                result.Status = $"GEMINI_WIKI_REWRITE_VERIFY_HTTP_{(int)response.StatusCode}";
                return result;
            }

            var json = ExtractText(body);

            if (string.IsNullOrWhiteSpace(json))
            {
                result.Status = "GEMINI_WIKI_REWRITE_VERIFY_EMPTY";
                return result;
            }

            var parsed = JsonSerializer.Deserialize<GeminiWikiRewriteVerificationResult>(json, ResponseJsonOptions);

            if (parsed is null)
            {
                result.Status = "GEMINI_WIKI_REWRITE_VERIFY_INVALID_JSON";
                return result;
            }

            parsed.WasAttempted = true;
            parsed.WasAvailable = true;
            parsed.Status = string.IsNullOrWhiteSpace(parsed.Status)
                ? "GEMINI_WIKI_REWRITE_VERIFY_READY"
                : parsed.Status;
            parsed.RawResponse = body;
            return parsed;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or UriFormatException or JsonException)
        {
            result.Status = exception is TaskCanceledException
                ? "GEMINI_WIKI_REWRITE_VERIFY_TIMEOUT"
                : "GEMINI_WIKI_REWRITE_VERIFY_UNAVAILABLE";
            result.RawResponse = exception.Message;
            return result;
        }
    }

    private string BuildRewritePrompt(
        ChartContext chartContext,
        GeminiWikiPatchResult patch,
        IngestResult rawManualSource,
        string scrubbedUserText,
        string conversationContext,
        IReadOnlyList<WikiRewriteTarget> targets)
    {
        var instructions = _promptCacheService.GetPrompt(
            RewritePromptFileName,
            "Rewrite the supplied sterile wiki markdown files with the requested update. Return JSON only.");

        return
            $"{instructions}\n\n" +
            $"Chart id: {chartContext.ChartId}\n" +
            "Resolved display placeholder: [PATIENT]\n" +
            $"Raw source wiki link: {BuildRawSourceLink(rawManualSource)}\n\n" +
            $"Recent scrubbed conversation:\n{conversationContext}\n\n" +
            $"Scrubbed user request:\n{scrubbedUserText}\n\n" +
            $"Patch summary:\n{patch.PatchSummary}\n\n" +
            $"Structured update facts:\n{FormatUpdates(patch.Updates)}\n\n" +
            $"Current wiki files to rewrite:\n{FormatTargets(targets)}\n\n" +
            "Return the complete replacement markdown for each file that must change.";
    }

    private string BuildVerifyPrompt(
        ChartContext chartContext,
        GeminiWikiPatchResult patch,
        IngestResult rawManualSource,
        string scrubbedUserText,
        IReadOnlyList<WikiRewriteTarget> originalTargets,
        IReadOnlyList<WikiFileRewrite> rewrittenFiles)
    {
        var instructions = _promptCacheService.GetPrompt(
            VerifyPromptFileName,
            "Verify that rewritten sterile wiki markdown preserves old facts and adds only the requested update. Return JSON only.");

        return
            $"{instructions}\n\n" +
            $"Chart id: {chartContext.ChartId}\n" +
            $"Raw source wiki link: {BuildRawSourceLink(rawManualSource)}\n\n" +
            $"Scrubbed user request:\n{scrubbedUserText}\n\n" +
            $"Patch summary:\n{patch.PatchSummary}\n\n" +
            $"Structured update facts:\n{FormatUpdates(patch.Updates)}\n\n" +
            $"Original files:\n{FormatTargets(originalTargets)}\n\n" +
            $"Rewritten files:\n{FormatRewrites(rewrittenFiles)}\n\n" +
            "Return verification JSON now.";
    }

    private static IReadOnlyList<WikiRewriteTarget> BuildTargets(
        string vaultRoot,
        ChartContext chartContext,
        GeminiWikiPatchResult patch,
        IngestResult rawManualSource)
    {
        var targets = new Dictionary<string, WikiRewriteTarget>(StringComparer.OrdinalIgnoreCase);

        AddTarget(targets, vaultRoot, chartContext, "wiki/Index.md", BuildIndexSeed(chartContext));
        AddTarget(targets, vaultRoot, chartContext, "wiki/Timeline.md", BuildTimelineSeed(chartContext));

        foreach (var update in patch.Updates)
        {
            var topic = string.IsNullOrWhiteSpace(update.Topic)
                ? "Manual Updates"
                : update.Topic;
            AddTarget(targets, vaultRoot, chartContext, $"wiki/{BuildSafeSlug(topic)}.md", BuildTopicSeed(topic, chartContext));
        }

        AddTarget(
            targets,
            vaultRoot,
            chartContext,
            $"wiki/manual_updates/{rawManualSource.IngestedAt:yyyy-MM-dd_HHmmss}_Manual_Update.md",
            BuildManualUpdateSeed(chartContext, patch, rawManualSource));

        return targets.Values.ToList();
    }

    private static void AddTarget(
        IDictionary<string, WikiRewriteTarget> targets,
        string vaultRoot,
        ChartContext chartContext,
        string relativePath,
        string seedContent)
    {
        var safePath = GetSafeWikiPath(vaultRoot, chartContext, relativePath);

        if (safePath is null)
        {
            return;
        }

        var normalizedRelativePath = NormalizeRelativePath(relativePath);
        var content = File.Exists(safePath)
            ? File.ReadAllText(safePath)
            : seedContent;

        targets[normalizedRelativePath] = new WikiRewriteTarget(normalizedRelativePath, content);
    }

    private static IReadOnlyList<WikiFileRewrite> FilterSafeRewrites(
        string vaultRoot,
        ChartContext chartContext,
        IReadOnlyList<WikiRewriteTarget> targets,
        IEnumerable<WikiFileRewrite> rewrites,
        ICollection<string> messages)
    {
        var allowedPaths = targets
            .Select(target => NormalizeRelativePath(target.RelativePath))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var acceptedFiles = new List<WikiFileRewrite>();

        foreach (var rewrite in rewrites)
        {
            var relativePath = NormalizeRelativePath(rewrite.RelativePath);

            if (string.IsNullOrWhiteSpace(relativePath) ||
                !allowedPaths.Contains(relativePath) ||
                GetSafeWikiPath(vaultRoot, chartContext, relativePath) is null)
            {
                messages.Add($"Rejected unsafe or unexpected rewrite path: {rewrite.RelativePath}");
                continue;
            }

            if (string.IsNullOrWhiteSpace(rewrite.Content))
            {
                messages.Add($"Rejected empty rewrite content: {relativePath}");
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
        var absolutePath = Path.GetFullPath(Path.Combine(chartRoot, normalizedRelativePath.Replace('/', Path.DirectorySeparatorChar)));
        var safeWikiRoot = wikiRoot.EndsWith(Path.DirectorySeparatorChar)
            ? wikiRoot
            : wikiRoot + Path.DirectorySeparatorChar;

        return absolutePath.StartsWith(safeWikiRoot, StringComparison.OrdinalIgnoreCase)
            ? absolutePath
            : null;
    }

    private static string BuildIndexSeed(ChartContext chartContext)
    {
        return
            $"# {chartContext.ChartId} Index\n\n" +
            "| Topic | Current Summary | Last Updated | Source | Status |\n" +
            "| --- | --- | --- | --- | --- |\n";
    }

    private static string BuildTimelineSeed(ChartContext chartContext)
    {
        return $"# {chartContext.ChartId} Timeline\n\n";
    }

    private static string BuildTopicSeed(string topic, ChartContext chartContext)
    {
        return
            $"# {topic.Trim()}\n\n" +
            $"Chart: {chartContext.ChartId}\n\n" +
            "## Known Facts\n\n" +
            "- No facts recorded yet.\n\n" +
            "## Sources\n\n" +
            "- none\n";
    }

    private static string BuildManualUpdateSeed(
        ChartContext chartContext,
        GeminiWikiPatchResult patch,
        IngestResult rawManualSource)
    {
        return
            $"# Manual Update - {rawManualSource.IngestedAt:yyyy-MM-dd HH:mm:ss}\n\n" +
            $"Chart: {chartContext.ChartId}\n" +
            $"Source: {BuildRawSourceLink(rawManualSource)}\n" +
            "Verification status: user_reported_unverified\n\n" +
            "## Backend Patch Summary\n\n" +
            $"{patch.PatchSummary}\n\n" +
            "## Extracted Updates\n\n" +
            $"{FormatUpdates(patch.Updates)}\n";
    }

    private static string BuildSafeSlug(string value)
    {
        var normalized = Regex.Replace(value.Trim(), @"[^\w\s-]", string.Empty);
        normalized = Regex.Replace(normalized, @"\s+", "_").Trim('_');
        return string.IsNullOrWhiteSpace(normalized)
            ? "Manual_Updates"
            : normalized;
    }

    private static string BuildRawSourceLink(IngestResult rawManualSource)
    {
        var name = Path.GetFileNameWithoutExtension(rawManualSource.RawVaultPath);
        return string.IsNullOrWhiteSpace(name)
            ? "raw source saved"
            : $"[[raw/{name}]]";
    }

    private static string FormatUpdates(IEnumerable<WikiFactUpdate> updates)
    {
        var lines = updates
            .Where(update => !string.IsNullOrWhiteSpace(update.Topic) || !string.IsNullOrWhiteSpace(update.Summary))
            .Select(update =>
                $"- topic: {update.Topic}; category: {update.Category}; value: {update.NewValue}; unit: {update.Unit}; date: {update.Date}; status: {update.Status}; verification: {update.VerificationStatus}; summary: {update.Summary}; replaces_pending: {update.ReplacesPendingText}")
            .ToList();

        return lines.Count == 0
            ? "- no structured update facts returned"
            : string.Join("\n", lines);
    }

    private static string FormatTargets(IEnumerable<WikiRewriteTarget> targets)
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
        return relativePath
            .Replace('\\', '/')
            .Trim()
            .TrimStart('/');
    }

    private static string NormalizeMarkdown(string markdown)
    {
        var normalized = markdown.Replace("\r\n", "\n").TrimEnd();
        return normalized + Environment.NewLine;
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

    private sealed record WikiRewriteTarget(string RelativePath, string OriginalContent);
}
