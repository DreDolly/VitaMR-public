using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using VitaMR.Models;

namespace VitaMR.Services;

public sealed class PresidioScrubberService : ILocalModelReviewService
{
    private static readonly Uri DefaultEndpoint = new("http://localhost:8001/scrub_text");
    private static readonly string[] LocalAuditModels =
    [
        "phi4-mini:latest",
        "llama3.2:3b"
    ];

    private static readonly JsonSerializerOptions RequestJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly HttpClient _httpClient;
    private readonly Uri _endpoint;

    public PresidioScrubberService()
        : this(new HttpClient { Timeout = TimeSpan.FromSeconds(45) }, DefaultEndpoint)
    {
    }

    public PresidioScrubberService(HttpClient httpClient, Uri endpoint)
    {
        _httpClient = httpClient;
        _endpoint = endpoint;
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
            Route = "presidio_local_privacy",
            EndpointUsed = _endpoint.ToString(),
            ModelNameUsed = "presidio-analyzer",
            RouteStatus = "Presidio local privacy service"
        };

        if (scrubResults.Count == 0)
        {
            result.IsAvailable = true;
            result.Status = "PRESIDIO_NO_PAYLOADS";
            result.RemainingPhiRisk = "unknown";
            result.Recommendation = "No scrubbed payloads were available for Presidio review.";
            return result;
        }

        try
        {
            foreach (var scrubResult in scrubResults)
            {
                if (string.IsNullOrWhiteSpace(scrubResult.FullText))
                {
                    continue;
                }

                var response = await _httpClient.PostAsJsonAsync(
                    _endpoint,
                    new PresidioScrubRequest(scrubResult.FullText),
                    cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    result.IsAvailable = false;
                    result.Status = $"PRESIDIO_HTTP_{(int)response.StatusCode}";
                    result.RemainingPhiRisk = "unknown";
                    result.Recommendation = "Presidio privacy service returned an error; C# stopped before API transmission.";
                    return result;
                }

                var payload = await response.Content.ReadFromJsonAsync<PresidioScrubResponse>(cancellationToken);

                if (payload is null || string.IsNullOrWhiteSpace(payload.ScrubbedText))
                {
                    result.IsAvailable = false;
                    result.Status = "PRESIDIO_RESPONSE_EMPTY";
                    result.RemainingPhiRisk = "unknown";
                    result.Recommendation = "Presidio privacy service returned no scrubbed text; C# stopped before API transmission.";
                    return result;
                }

                var findings = payload.Entities
                    .Select(entity => new LocalModelFinding
                    {
                        Type = entity.EntityType,
                        Text = entity.Text,
                        SuggestedReplacement = BuildReplacementToken(entity.EntityType)
                    })
                    .Where(finding => !string.IsNullOrWhiteSpace(finding.Text))
                    .ToList();
                var auditFindings = await RunLocalMissedPhiAuditAsync(
                    endpoint,
                    payload.ScrubbedText,
                    cancellationToken);
                findings.AddRange(auditFindings);
                findings = PrivacyFindingGuardrailService.FilterAndOrderFindings(findings).ToList();

                result.ReviewedPayloads.Add(new LocalModelReviewedPayload
                {
                    SourcePath = scrubResult.SourcePath,
                    DisplayName = scrubResult.DisplayName,
                    IntegrityStatus = payload.PreservedClinicalTiming ? "intact" : "needs_review",
                    IntegrityRecommendation = payload.PreservedClinicalTiming
                        ? "Clinical timing markers were preserved by the local Presidio service."
                        : "Presidio completed, but clinical timing preservation could not be confirmed.",
                    IntegrityIssues = payload.Warnings,
                    Findings = findings
                });

                foreach (var finding in findings)
                {
                    result.StructuredFindings.Add(finding);
                }

                result.Findings.Add($"{scrubResult.DisplayName}: {payload.ReplacementCount} Presidio replacement(s)");
                if (auditFindings.Count > 0)
                {
                    result.Findings.Add($"{scrubResult.DisplayName}: {auditFindings.Count} local missed-identifier audit finding(s)");
                }
            }

            result.IsAvailable = true;
            result.Status = "PRESIDIO_PRIVACY_REVIEW_READY";
            result.RemainingPhiRisk = result.StructuredFindings.Count == 0 ? "low" : "reviewed";
            result.Recommendation = "Presidio completed local privacy scrubbing. The local missed-identifier audit added any extra exact spans it found, and C# can save final scrubbed payloads for API use.";
            result.RawResponse = $"Payloads reviewed: {result.ReviewedPayloads.Count}";
            return result;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            result.IsAvailable = false;
            result.Status = exception is TaskCanceledException ? "PRESIDIO_TIMEOUT" : "PRESIDIO_UNAVAILABLE";
            result.RemainingPhiRisk = "unknown";
            result.Recommendation = "Presidio local privacy service was unavailable; C# stopped before API transmission.";
            result.RawResponse = exception.Message;
            return result;
        }
    }

    private static string BuildReplacementToken(string entityType)
    {
        var normalized = string.IsNullOrWhiteSpace(entityType)
            ? "TOKEN"
            : entityType.Trim().ToUpperInvariant().Replace(" ", "_", StringComparison.Ordinal);

        return $"[{normalized}]";
    }

    private async Task<IReadOnlyList<LocalModelFinding>> RunLocalMissedPhiAuditAsync(
        string endpoint,
        string presidioScrubbedText,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(presidioScrubbedText))
        {
            return [];
        }

        foreach (var modelName in LocalAuditModels)
        {
            try
            {
                var requestBody = new
                {
                    model = modelName,
                    prompt = BuildMissedPhiAuditPrompt(presidioScrubbedText),
                    stream = false,
                    format = "json",
                    keep_alive = "45s",
                    options = new
                    {
                        temperature = 0.0,
                        num_predict = 500
                    }
                };

                using var request = new StringContent(
                    JsonSerializer.Serialize(requestBody, RequestJsonOptions),
                    Encoding.UTF8,
                    "application/json");
                using var response = await _httpClient.PostAsync(
                    LaptopWorkerProtocol.BuildInferenceUri(endpoint),
                    request,
                    cancellationToken);
                var body = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    continue;
                }

                var modelJson = LaptopWorkerProtocol.ExtractModelResponseOrThrow(body);
                return ParseMissedPhiFindings(modelJson, presidioScrubbedText);
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException or UriFormatException)
            {
            }
        }

        return [];
    }

    private static string BuildMissedPhiAuditPrompt(string text)
    {
        return
            "You are VitaMR's private local missed-identifier auditor. The text was already scrubbed by Presidio.\n" +
            "Find only explicit remaining identifiers that should not leave the local machine.\n" +
            "Include patient names, family member names, caregiver names, clinician names, addresses, phone numbers, emails, SSNs, MRNs, and workplace/school names when they identify a person.\n" +
            "Do not include medical conditions, medications, symptoms, labs, imaging, procedures, ages, relative timing, dates already inside brackets, or family-history conditions such as Lynch Syndrome.\n" +
            "Return exact substrings copied from the text. Do not rewrite the note.\n" +
            "Return JSON only with this shape: {\"findings\":[{\"type\":\"PERSON\",\"text\":\"Maxell James\",\"replacement\":\"[PERSON]\"}]}\n\n" +
            "Text:\n" +
            text.Trim();
    }

    private static IReadOnlyList<LocalModelFinding> ParseMissedPhiFindings(string rawJson, string sourceText)
    {
        var findings = new List<LocalModelFinding>();
        using var document = JsonDocument.Parse(ExtractJsonObject(rawJson));
        var root = document.RootElement;

        if (!root.TryGetProperty("findings", out var findingsElement) ||
            findingsElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        foreach (var element in findingsElement.EnumerateArray())
        {
            var text = ReadString(element, "text");

            if (string.IsNullOrWhiteSpace(text) ||
                text.Contains('[', StringComparison.Ordinal) ||
                sourceText.Contains(text, StringComparison.OrdinalIgnoreCase) is false)
            {
                continue;
            }

            var type = ReadString(element, "type");
            var replacement = ReadString(element, "replacement");

            findings.Add(new LocalModelFinding
            {
                Type = string.IsNullOrWhiteSpace(type) ? GuessFindingType(text) : type,
                Text = text.Trim(),
                SuggestedReplacement = string.IsNullOrWhiteSpace(replacement)
                    ? BuildReplacementToken(GuessFindingType(text))
                    : replacement.Trim()
            });
        }

        return PrivacyFindingGuardrailService.FilterAndOrderFindings(findings);
    }

    private static string ExtractJsonObject(string raw)
    {
        var trimmed = raw.Trim();
        var start = trimmed.IndexOf('{');
        var end = trimmed.LastIndexOf('}');

        if (start < 0 || end <= start)
        {
            throw new JsonException("No JSON object found.");
        }

        return trimmed[start..(end + 1)];
    }

    private static string ReadString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) &&
               property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? string.Empty
            : string.Empty;
    }

    private static string GuessFindingType(string text)
    {
        if (Regex.IsMatch(text, @"\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b", RegexOptions.IgnoreCase))
        {
            return "EMAIL_ADDRESS";
        }

        if (Regex.IsMatch(text, @"\b\d{3}[-.\s]?\d{2}[-.\s]?\d{4}\b"))
        {
            return "US_SSN";
        }

        if (Regex.IsMatch(text, @"\b(?:\+?1[-.\s]?)?(?:\(?\d{3}\)?[-.\s]?)\d{3}[-.\s]?\d{4}\b"))
        {
            return "PHONE_NUMBER";
        }

        return "PERSON";
    }

    private sealed record PresidioScrubRequest(
        [property: JsonPropertyName("text")] string Text,
        [property: JsonPropertyName("language")] string Language = "en");

    private sealed class PresidioScrubResponse
    {
        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("scrubbed_text")]
        public string ScrubbedText { get; set; } = string.Empty;

        [JsonPropertyName("replacement_count")]
        public int ReplacementCount { get; set; }

        [JsonPropertyName("preserved_clinical_timing")]
        public bool PreservedClinicalTiming { get; set; }

        [JsonPropertyName("warnings")]
        public List<string> Warnings { get; set; } = [];

        [JsonPropertyName("entities")]
        public List<PresidioEntity> Entities { get; set; } = [];
    }

    private sealed class PresidioEntity
    {
        [JsonPropertyName("entity_type")]
        public string EntityType { get; set; } = string.Empty;

        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;
    }
}
