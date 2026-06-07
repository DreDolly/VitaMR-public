using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace SafeHarborDeidDesktop;

internal sealed class LocalModelReviewer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(90)
    };

    public async Task<AiReviewResult> ReviewAsync(
        string endpoint,
        string privacyModel,
        string preservationModel,
        string originalText,
        string redactedText,
        CancellationToken cancellationToken = default)
    {
        var warnings = new List<string>();
        var privacyFindings = new List<AiFinding>();
        var preservationIssues = new List<AiPreservationIssue>();
        var finalText = redactedText;

        try
        {
            var privacyJson = await AskOllamaAsync(
                endpoint,
                privacyModel,
                BuildPrivacyPrompt(redactedText),
                cancellationToken);
            privacyFindings = ParsePrivacyFindings(privacyJson, redactedText).ToList();

            foreach (var finding in privacyFindings)
            {
                finalText = ReplaceExact(finalText, finding.Text, finding.Replacement);
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException or UriFormatException)
        {
            warnings.Add($"AI privacy review unavailable: {exception.Message}");
        }

        try
        {
            var preservationJson = await AskOllamaAsync(
                endpoint,
                preservationModel,
                BuildPreservationPrompt(originalText, finalText),
                cancellationToken);
            preservationIssues = ParsePreservationIssues(preservationJson).ToList();
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException or UriFormatException)
        {
            warnings.Add($"AI preservation review unavailable: {exception.Message}");
        }

        var status = preservationIssues.Count > 0
            ? "over_redacted_needs_review"
            : "ai_reviewed";

        return new AiReviewResult(
            status,
            endpoint,
            privacyModel,
            preservationModel,
            finalText,
            privacyFindings,
            preservationIssues,
            warnings);
    }

    private async Task<string> AskOllamaAsync(
        string endpoint,
        string model,
        string prompt,
        CancellationToken cancellationToken)
    {
        var baseUri = endpoint.TrimEnd('/');
        using var request = new StringContent(
            JsonSerializer.Serialize(new
            {
                model,
                prompt,
                stream = false,
                format = "json",
                options = new
                {
                    temperature = 0.0,
                    num_predict = 900
                }
            }, JsonOptions),
            Encoding.UTF8,
            "application/json");

        using var response = await _httpClient.PostAsync($"{baseUri}/api/generate", request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(body);
        return document.RootElement.TryGetProperty("response", out var responseElement)
            ? responseElement.GetString() ?? string.Empty
            : body;
    }

    private static string BuildPrivacyPrompt(string redactedText)
    {
        return
            "You are a local-only HIPAA Safe Harbor privacy reviewer.\n" +
            "The text has already been redacted. Find explicit remaining identifiers only.\n" +
            "Return JSON only. Do not rewrite, summarize, diagnose, or explain.\n" +
            "Do not flag diagnoses, medications, vitals, labs, CPET/echo measurements, HOCM, LVOT, VO2, LGE, EF, treatment names, or relative timing.\n" +
            "Flag remaining names, dates, facilities, schools, small locations, addresses, phones, emails, URLs, IPs, IDs, device IDs, vehicle IDs, and unique identifying phrases.\n" +
            "Do not flag a span merely because it is medically specific. A finding must explain how the span identifies a person, provider, facility, location, record, household member, employer, device, vehicle, account, or unique personal characteristic.\n" +
            "Use exact substrings copied from the text. If none, return an empty findings array.\n" +
            "Shape: {\"findings\":[{\"type\":\"person_name\",\"text\":\"exact visible span\",\"replacement\":\"[NAME]\",\"why_phi\":\"short identity-risk reason\"}]}\n\n" +
            "TEXT:\n" + Truncate(redactedText, 12000);
    }

    private static string BuildPreservationPrompt(string originalText, string redactedText)
    {
        return
            "You are a local-only clinical content preservation reviewer.\n" +
            "Compare ORIGINAL and REDACTED. Ignore identifiers being replaced with bracket tokens.\n" +
            "Find only important non-identifier medical facts that were removed, distorted, or changed by redaction.\n" +
            "Do not complain that names, dates, facilities, schools, addresses, IDs, or signatures were removed.\n" +
            "Important non-ID content includes diagnoses, tests, clinical findings, measurements, treatments, assessment, plan, clearance status, and clinical chronology meaning.\n" +
            "Return JSON only. Do not rewrite the note.\n" +
            "Shape: {\"issues\":[{\"original_text\":\"exact original span\",\"redacted_text\":\"exact redacted span or blank\",\"problem\":\"short reason\"}]}\n\n" +
            "ORIGINAL:\n" + Truncate(originalText, 9000) + "\n\n" +
            "REDACTED:\n" + Truncate(redactedText, 9000);
    }

    private static IReadOnlyList<AiFinding> ParsePrivacyFindings(string rawJson, string sourceText)
    {
        var findings = new List<AiFinding>();
        using var document = JsonDocument.Parse(ExtractJsonObject(rawJson));

        if (!document.RootElement.TryGetProperty("findings", out var findingsElement) ||
            findingsElement.ValueKind != JsonValueKind.Array)
        {
            return findings;
        }

        foreach (var element in findingsElement.EnumerateArray())
        {
            var text = ReadString(element, "text");
            if (string.IsNullOrWhiteSpace(text) ||
                text.Contains('[', StringComparison.Ordinal) ||
                !sourceText.Contains(text, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var type = ReadString(element, "type");
            var replacement = ReadString(element, "replacement");
            var whyPhi = ReadString(element, "why_phi");
            if (!IsAcceptableAiPrivacyFinding(text, type, whyPhi))
            {
                continue;
            }

            findings.Add(new AiFinding(
                string.IsNullOrWhiteSpace(type) ? "identifier" : type,
                text.Trim(),
                string.IsNullOrWhiteSpace(replacement) ? GuessReplacement(text) : replacement.Trim(),
                whyPhi.Trim()));
        }

        return findings
            .GroupBy(finding => finding.Text, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderByDescending(finding => finding.Text.Length)
            .ToList();
    }

    private static IReadOnlyList<AiPreservationIssue> ParsePreservationIssues(string rawJson)
    {
        var issues = new List<AiPreservationIssue>();
        using var document = JsonDocument.Parse(ExtractJsonObject(rawJson));

        if (!document.RootElement.TryGetProperty("issues", out var issuesElement) ||
            issuesElement.ValueKind != JsonValueKind.Array)
        {
            return issues;
        }

        foreach (var element in issuesElement.EnumerateArray())
        {
            var original = ReadString(element, "original_text");
            var redacted = ReadString(element, "redacted_text");
            var problem = ReadString(element, "problem");

            if (!string.IsNullOrWhiteSpace(original) || !string.IsNullOrWhiteSpace(problem))
            {
                issues.Add(new AiPreservationIssue(original, redacted, problem));
            }
        }

        return issues.Take(12).ToList();
    }

    private static string ReplaceExact(string input, string text, string replacement)
    {
        return Regex.Replace(input, Regex.Escape(text.Trim()), replacement, RegexOptions.IgnoreCase);
    }

    private static bool IsAcceptableAiPrivacyFinding(string text, string type, string whyPhi)
    {
        var combined = $"{type} {whyPhi}".ToLowerInvariant();
        var value = text.Trim();

        if (string.IsNullOrWhiteSpace(whyPhi))
        {
            return IsObviousIdentifier(value);
        }

        if (combined.Contains("diagnosis") ||
            combined.Contains("condition") ||
            combined.Contains("symptom") ||
            combined.Contains("procedure") ||
            combined.Contains("therapy") ||
            combined.Contains("treatment") ||
            combined.Contains("test") ||
            combined.Contains("lab") ||
            combined.Contains("vital") ||
            combined.Contains("measurement") ||
            combined.Contains("finding") ||
            combined.Contains("trial type") ||
            combined.Contains("medical concept"))
        {
            return IsObviousIdentifier(value);
        }

        return combined.Contains("name") ||
               combined.Contains("date") ||
               combined.Contains("facility") ||
               combined.Contains("school") ||
               combined.Contains("address") ||
               combined.Contains("location") ||
               combined.Contains("phone") ||
               combined.Contains("email") ||
               combined.Contains("url") ||
               combined.Contains("ip") ||
               combined.Contains("record") ||
               combined.Contains("account") ||
               combined.Contains("device") ||
               combined.Contains("vehicle") ||
               combined.Contains("license") ||
               combined.Contains("provider") ||
               combined.Contains("employer") ||
               combined.Contains("household") ||
               combined.Contains("relative") ||
               combined.Contains("unique personal");
    }

    private static bool IsObviousIdentifier(string text)
    {
        return Regex.IsMatch(text, @"\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b", RegexOptions.IgnoreCase) ||
               Regex.IsMatch(text, @"\b(?:https?://|www\.)\S+", RegexOptions.IgnoreCase) ||
               Regex.IsMatch(text, @"\b\d{3}[- ]\d{2}[- ]\d{4}\b") ||
               Regex.IsMatch(text, @"(?<!\w)(?:\+?1[-.\s]?)?(?:\(?\d{3}\)?[-.\s]?)\d{3}[-.\s]?\d{4}(?!\w)") ||
               Regex.IsMatch(text, @"\b\d{1,2}[/-]\d{1,2}[/-](?:\d{2}|\d{4})\b") ||
               Regex.IsMatch(text, @"\b(?:MRN|Medical Record|Patient ID|Account|Claim|Member ID|Device ID|Serial|VIN|License Plate)\b", RegexOptions.IgnoreCase);
    }

    private static string GuessReplacement(string text)
    {
        if (Regex.IsMatch(text, @"\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b", RegexOptions.IgnoreCase))
        {
            return "[EMAIL]";
        }

        if (Regex.IsMatch(text, @"\b\d{1,2}[/-]\d{1,2}[/-](?:\d{2}|\d{4})\b"))
        {
            return "[DATE]";
        }

        return "[IDENTIFIER]";
    }

    private static string ExtractJsonObject(string raw)
    {
        var trimmed = raw.Trim();
        var start = trimmed.IndexOf('{');
        var end = trimmed.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            throw new JsonException("No JSON object found in model response.");
        }

        return trimmed[start..(end + 1)];
    }

    private static string ReadString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? string.Empty
            : string.Empty;
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}

internal sealed record AiReviewResult(
    string Status,
    string Endpoint,
    string PrivacyModel,
    string PreservationModel,
    string FinalText,
    IReadOnlyList<AiFinding> PrivacyFindings,
    IReadOnlyList<AiPreservationIssue> PreservationIssues,
    IReadOnlyList<string> Warnings);

internal sealed record AiFinding(
    string Type,
    string Text,
    string Replacement,
    [property: JsonPropertyName("why_phi")] string WhyPhi);

internal sealed record AiPreservationIssue(
    [property: JsonPropertyName("original_text")] string OriginalText,
    [property: JsonPropertyName("redacted_text")] string RedactedText,
    string Problem);
