using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using VitaMR.Models;

namespace VitaMR.Services;

public sealed class OllamaLocalModelReviewService : ILocalModelReviewService
{
    private const string EntitySweepPromptFileName = "Local_Privacy_Pass1_Entity_Sweep.md";
    private const string ChronologySweepPromptFileName = "Local_Privacy_Pass2_Chronology_Digits.md";
    private const string ContextualSweepPromptFileName = "Local_Privacy_Pass3_Contextual_Sweep.md";
    private const string IntegrityAuditPromptFileName = "Local_Privacy_Pass4_Clinical_Integrity_Audit.md";
    private const int MaxChunkLength = 1200;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly HttpClient _httpClient;
    private readonly IPromptCacheService _promptCacheService;

    public OllamaLocalModelReviewService(IPromptCacheService promptCacheService)
    {
        _promptCacheService = promptCacheService;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(90)
        };
    }

    public async Task<LocalModelReviewResult> ReviewScrubbedPayloadAsync(
        string endpoint,
        string modelName,
        IReadOnlyList<ScrubResult> scrubResults,
        CancellationToken cancellationToken = default)
    {
        var result = new LocalModelReviewResult
        {
            WasAttempted = true,
            Status = "LOCAL_MODEL_MULTI_PASS_STARTED"
        };

        try
        {
            var uri = LaptopWorkerProtocol.BuildInferenceUri(endpoint);
            var reviewedPayloads = new List<LocalModelReviewedPayload>();
            var rawResponses = new List<string>();

            foreach (var scrubResult in scrubResults.Where(resultItem => !string.IsNullOrWhiteSpace(resultItem.FullText)))
            {
                var payloadReview = await ReviewSinglePayloadAsync(
                    uri,
                    modelName,
                    scrubResult,
                    rawResponses,
                    cancellationToken);

                reviewedPayloads.Add(payloadReview);
            }

            result.IsAvailable = reviewedPayloads.Count > 0;
            result.ReviewedPayloads = reviewedPayloads;
            result.StructuredFindings = PrivacyFindingGuardrailService.FilterAndOrderFindings(
                    reviewedPayloads.SelectMany(payload => payload.Findings))
                .ToList();
            result.Findings = result.StructuredFindings
                .Select(FormatFinding)
                .Where(finding => !string.IsNullOrWhiteSpace(finding))
                .Cast<string>()
                .ToList();
            result.RemainingPhiRisk = DetermineRemainingRisk(result.StructuredFindings);
            result.Recommendation = BuildRecommendation(reviewedPayloads, result.StructuredFindings.Count);
            result.Status = DetermineStatus(reviewedPayloads);
            result.RawResponse = string.Join("\n\n====\n\n", rawResponses);
            return result;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or UriFormatException or JsonException)
        {
            result.Status = "LOCAL_MODEL_UNAVAILABLE";
            result.Recommendation = exception switch
            {
                TaskCanceledException => "Local Gemma review timed out while waiting for Ollama. Keep Ollama running and try again after the model has warmed up.",
                JsonException => "Local Gemma returned malformed JSON during multi-pass privacy review. The scrubber was stopped before API processing.",
                _ => "Local Gemma review could not connect. Start Ollama or verify the endpoint/model settings."
            };
            result.RawResponse = exception.Message;
            return result;
        }
    }

    private async Task<LocalModelReviewedPayload> ReviewSinglePayloadAsync(
        Uri uri,
        string modelName,
        ScrubResult scrubResult,
        ICollection<string> rawResponses,
        CancellationToken cancellationToken)
    {
        var payloadReview = new LocalModelReviewedPayload
        {
            SourcePath = scrubResult.SourcePath,
            DisplayName = scrubResult.DisplayName
        };

        var chunks = BuildChunks(scrubResult.FullText);
        var collectedFindings = new List<LocalModelFinding>();

        for (var index = 0; index < chunks.Count; index++)
        {
            var chunk = chunks[index];
            collectedFindings.AddRange(await RunFindingPassAsync(
                uri,
                modelName,
                scrubResult,
                chunk,
                index + 1,
                chunks.Count,
                "PASS_1_ENTITY_SWEEP",
                EntitySweepPromptFileName,
                rawResponses,
                cancellationToken));
            collectedFindings.AddRange(await RunFindingPassAsync(
                uri,
                modelName,
                scrubResult,
                chunk,
                index + 1,
                chunks.Count,
                "PASS_2_CHRONOLOGY_DIGITS",
                ChronologySweepPromptFileName,
                rawResponses,
                cancellationToken));
            collectedFindings.AddRange(await RunFindingPassAsync(
                uri,
                modelName,
                scrubResult,
                chunk,
                index + 1,
                chunks.Count,
                "PASS_3_CONTEXTUAL_SWEEP",
                ContextualSweepPromptFileName,
                rawResponses,
                cancellationToken));
        }

        payloadReview.Findings = PrivacyFindingGuardrailService.FilterAndOrderFindings(collectedFindings)
            .ToList();

        var deterministicRedaction = ApplyDeterministicRedaction(scrubResult.FullText, payloadReview.Findings);
        var auditResult = await RunIntegrityAuditAsync(
            uri,
            modelName,
            scrubResult,
            deterministicRedaction,
            rawResponses,
            cancellationToken);

        payloadReview.IntegrityStatus = auditResult.IntegrityStatus;
        payloadReview.IntegrityRecommendation = auditResult.Recommendation;
        payloadReview.IntegrityIssues = auditResult.Issues.ToList();
        return payloadReview;
    }

    private async Task<IReadOnlyList<LocalModelFinding>> RunFindingPassAsync(
        Uri uri,
        string modelName,
        ScrubResult scrubResult,
        string chunk,
        int chunkNumber,
        int totalChunks,
        string passName,
        string promptFileName,
        ICollection<string> rawResponses,
        CancellationToken cancellationToken)
    {
        var prompt = BuildFindingPrompt(
            LoadPrompt(promptFileName),
            scrubResult,
            chunk,
            chunkNumber,
            totalChunks,
            passName);
        string rawModelJson;

        try
        {
            rawModelJson = await ExecuteJsonPromptAsync(uri, modelName, prompt, 700, cancellationToken);
            rawResponses.Add($"{passName} {scrubResult.DisplayName} chunk {chunkNumber}/{totalChunks}\n{rawModelJson}");
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or UriFormatException or JsonException)
        {
            rawResponses.Add($"{passName} {scrubResult.DisplayName} chunk {chunkNumber}/{totalChunks} MODEL_PASS_FAILED\n{exception.Message}");
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(rawModelJson);
            var root = document.RootElement;

            if (!root.TryGetProperty("findings", out var findingsElement) ||
                findingsElement.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var findings = new List<LocalModelFinding>();

            foreach (var findingElement in findingsElement.EnumerateArray())
            {
                var finding = ParseFinding(findingElement);

                if (finding is not null)
                {
                    findings.Add(finding);
                }
            }

            return findings;
        }
        catch (JsonException exception)
        {
            rawResponses.Add($"{passName} {scrubResult.DisplayName} chunk {chunkNumber}/{totalChunks} JSON_PARSE_FAILED\n{exception.Message}");
            return [];
        }
    }

    private async Task<IntegrityAuditResult> RunIntegrityAuditAsync(
        Uri uri,
        string modelName,
        ScrubResult scrubResult,
        string deterministicRedaction,
        ICollection<string> rawResponses,
        CancellationToken cancellationToken)
    {
        var prompt = BuildIntegrityAuditPrompt(
            LoadPrompt(IntegrityAuditPromptFileName),
            scrubResult,
            deterministicRedaction);
        string rawModelJson;

        try
        {
            rawModelJson = await ExecuteJsonPromptAsync(uri, modelName, prompt, 900, cancellationToken);
            rawResponses.Add($"PASS_4_CLINICAL_INTEGRITY {scrubResult.DisplayName}\n{rawModelJson}");
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or UriFormatException or JsonException)
        {
            rawResponses.Add($"PASS_4_CLINICAL_INTEGRITY {scrubResult.DisplayName} MODEL_PASS_FAILED\n{exception.Message}");
            return new IntegrityAuditResult(
                "model_pass_unavailable",
                "Local Gemma privacy passes had a worker error; C# will continue with deterministic scrub findings and mark this payload for review.",
                [exception.Message]);
        }

        try
        {
            using var document = JsonDocument.Parse(rawModelJson);
            var root = document.RootElement;

            var issues = new List<string>();

            if (root.TryGetProperty("issues", out var issuesElement) &&
                issuesElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var issue in issuesElement.EnumerateArray())
                {
                    if (issue.ValueKind == JsonValueKind.String)
                    {
                        var value = issue.GetString();

                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            issues.Add(value.Trim());
                        }
                    }
                }
            }

            return new IntegrityAuditResult(
                ReadString(root, "integrity_status", "not_run"),
                ReadString(root, "recommendation", "Integrity audit completed."),
                issues);
        }
        catch (JsonException exception)
        {
            rawResponses.Add($"PASS_4_CLINICAL_INTEGRITY {scrubResult.DisplayName} JSON_PARSE_FAILED\n{exception.Message}");
            return new IntegrityAuditResult(
                "possible_loss",
                "Local Gemma returned malformed JSON during clinical integrity audit; C# should treat this as a privacy gate warning.",
                [exception.Message]);
        }
    }

    private async Task<string> ExecuteJsonPromptAsync(
        Uri uri,
        string modelName,
        string prompt,
        int numPredict,
        CancellationToken cancellationToken)
    {
        var requestBody = new
        {
            model = modelName,
            prompt,
            stream = false,
            format = "json",
            keep_alive = "30s",
            options = new
            {
                temperature = 0,
                num_predict = numPredict
            }
        };

        using var request = new StringContent(
            JsonSerializer.Serialize(requestBody, JsonOptions),
            Encoding.UTF8,
            "application/json");

        using var response = await _httpClient.PostAsync(uri, request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Local model endpoint returned {(int)response.StatusCode}: {body}");
        }

        var modelJson = LaptopWorkerProtocol.ExtractModelResponseOrThrow(body);

        if (string.IsNullOrWhiteSpace(modelJson))
        {
            throw new JsonException("Local model returned an empty response.");
        }

        return modelJson;
    }
    private string LoadPrompt(string promptFileName)
    {
        return _promptCacheService.GetPrompt(
            promptFileName,
            "Review the supplied scrubbed payload and return JSON only.");
    }

    private static string BuildFindingPrompt(
        string instructions,
        ScrubResult scrubResult,
        string chunk,
        int chunkNumber,
        int totalChunks,
        string passName)
    {
        return
            $"{instructions}\n\n" +
            "Use thinking mode internally for privacy data processing. Do not include reasoning. Return JSON only.\n\n" +
            $"Pass: {passName}\n" +
            $"File: {scrubResult.DisplayName}\n" +
            $"Scrub status: {scrubResult.Status}\n" +
            $"Chunk: {chunkNumber} of {totalChunks}\n" +
            $"First-pass findings already removed: {string.Join(", ", scrubResult.Findings)}\n\n" +
            "Only report text that appears exactly in this chunk. Do not invent, normalize, or summarize spans.\n\n" +
            $"Chunk text:\n{chunk}";
    }

    private static string BuildIntegrityAuditPrompt(
        string instructions,
        ScrubResult scrubResult,
        string deterministicRedaction)
    {
        return
            $"{instructions}\n\n" +
            "Use thinking mode internally for integrity review. Do not include reasoning. Return JSON only.\n\n" +
            $"File: {scrubResult.DisplayName}\n" +
            $"Original scrubbed payload:\n{scrubResult.FullText}\n\n" +
            $"Deterministically redacted payload:\n{deterministicRedaction}";
    }

    private static List<string> BuildChunks(string text)
    {
        var normalized = text.Replace("\r\n", "\n");
        var lines = normalized.Split('\n');
        var chunks = new List<string>();
        var builder = new StringBuilder();

        foreach (var line in lines)
        {
            var candidate = builder.Length == 0
                ? line
                : builder + "\n" + line;

            if (candidate.Length > MaxChunkLength && builder.Length > 0)
            {
                chunks.Add(builder.ToString());
                builder.Clear();
            }

            if (line.Length > MaxChunkLength)
            {
                var offset = 0;

                while (offset < line.Length)
                {
                    var length = Math.Min(MaxChunkLength, line.Length - offset);
                    chunks.Add(line.Substring(offset, length));
                    offset += length;
                }

                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append('\n');
            }

            builder.Append(line);
        }

        if (builder.Length > 0)
        {
            chunks.Add(builder.ToString());
        }

        return chunks.Count == 0 ? [string.Empty] : chunks;
    }

    private static string ApplyDeterministicRedaction(string input, IReadOnlyList<LocalModelFinding> findings)
    {
        var redacted = input;

        foreach (var finding in PrivacyFindingGuardrailService.FilterAndOrderFindings(findings))
        {
            if (string.IsNullOrWhiteSpace(finding.Text))
            {
                continue;
            }

            var replacement = string.IsNullOrWhiteSpace(finding.SuggestedReplacement)
                ? BuildReplacementToken(finding.Type)
                : finding.SuggestedReplacement.Trim();
            var pattern = Regex.Escape(finding.Text.Trim());
            redacted = Regex.Replace(redacted, pattern, replacement, RegexOptions.IgnoreCase);
        }

        return redacted;
    }

    private static string DetermineRemainingRisk(IReadOnlyList<LocalModelFinding> findings)
    {
        return findings.Count == 0
            ? "low"
            : "medium";
    }

    private static string DetermineStatus(IReadOnlyList<LocalModelReviewedPayload> reviewedPayloads)
    {
        if (reviewedPayloads.Count == 0)
        {
            return "LOCAL_MODEL_MULTI_PASS_EMPTY";
        }

        return reviewedPayloads.Any(payload => !string.Equals(payload.IntegrityStatus, "intact", StringComparison.OrdinalIgnoreCase))
            ? "LOCAL_MODEL_MULTI_PASS_INTEGRITY_WARNING"
            : "LOCAL_MODEL_MULTI_PASS_READY";
    }

    private static string BuildRecommendation(IReadOnlyList<LocalModelReviewedPayload> reviewedPayloads, int findingCount)
    {
        if (reviewedPayloads.Count == 0)
        {
            return "No scrubbed payload text was available for local review.";
        }

        var warnings = reviewedPayloads
            .Where(payload => !string.Equals(payload.IntegrityStatus, "intact", StringComparison.OrdinalIgnoreCase))
            .Select(payload => $"{payload.DisplayName}: {payload.IntegrityRecommendation}")
            .ToList();

        if (warnings.Count > 0)
        {
            return string.Join(" | ", warnings);
        }

        return $"Multi-pass scrub completed. Deterministic C# redaction will apply {findingCount} unique finding(s) before API use.";
    }

    private static string ReadString(JsonElement root, string propertyName, string fallback)
    {
        return root.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? fallback
            : fallback;
    }

    private static string? FormatFinding(LocalModelFinding finding)
    {
        var normalizedText = string.IsNullOrWhiteSpace(finding.Text)
            ? "unspecified text"
            : Truncate(finding.Text, 80);

        return string.IsNullOrWhiteSpace(finding.SuggestedReplacement)
            ? $"{finding.Type}: {normalizedText}"
            : $"{finding.Type}: {normalizedText} -> {finding.SuggestedReplacement}";
    }

    private static LocalModelFinding? ParseFinding(JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var text = ReadString(item, "text", string.Empty);

        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return new LocalModelFinding
        {
            Type = ReadString(item, "type", "other"),
            Text = text.Trim(),
            SuggestedReplacement = ReadString(item, "suggested_replacement", "[TOKEN]")
        };
    }

    private static string BuildReplacementToken(string type)
    {
        var normalized = string.IsNullOrWhiteSpace(type)
            ? "TOKEN"
            : Regex.Replace(type.Trim().ToUpperInvariant(), @"[^A-Z0-9]+", "_").Trim('_');

        return string.IsNullOrWhiteSpace(normalized)
            ? "[TOKEN]"
            : $"[{normalized}]";
    }

    private static string Truncate(string value, int maxLength)
    {
        var flattened = Regex.Replace(value.Trim(), @"\s+", " ");

        return flattened.Length <= maxLength
            ? flattened
            : $"{flattened[..maxLength]}...";
    }

    private sealed class LocalModelFindingComparer : IEqualityComparer<LocalModelFinding>
    {
        public static LocalModelFindingComparer Instance { get; } = new();

        public bool Equals(LocalModelFinding? x, LocalModelFinding? y)
        {
            if (x is null || y is null)
            {
                return false;
            }

            return string.Equals(x.Type, y.Type, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(x.Text, y.Text, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(x.SuggestedReplacement, y.SuggestedReplacement, StringComparison.Ordinal);
        }

        public int GetHashCode(LocalModelFinding obj)
        {
            return HashCode.Combine(
                obj.Type.ToUpperInvariant(),
                obj.Text.ToUpperInvariant(),
                obj.SuggestedReplacement);
        }
    }

    private sealed record IntegrityAuditResult(
        string IntegrityStatus,
        string Recommendation,
        IReadOnlyList<string> Issues);
}
