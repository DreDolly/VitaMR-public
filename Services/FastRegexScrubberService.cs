using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using VitaMR.Models;

namespace VitaMR.Services;

public sealed partial class FastRegexScrubberService : IFastScrubberService
{
    private static readonly Uri LocalOcrEndpoint = new("http://localhost:8000/extract_text");

    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt",
        ".md",
        ".csv",
        ".json",
        ".xml",
        ".html",
        ".htm",
        ".log"
    };

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".tif",
        ".tiff",
        ".bmp",
        ".gif",
        ".webp"
    };

    public ScrubResult ScrubFile(string rawFilePath)
    {
        var displayName = Path.GetFileName(rawFilePath);
        var extension = Path.GetExtension(rawFilePath);

        if (!File.Exists(rawFilePath))
        {
            return new ScrubResult(rawFilePath, displayName, "RAW_FILE_MISSING", string.Empty, []);
        }

        if (ImageExtensions.Contains(extension) ||
            string.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            var extractedText = TryExtractTextWithLocalOcr(rawFilePath, out var ocrStatus, out var ocrFindings);
            if (string.IsNullOrWhiteSpace(extractedText))
            {
                return new ScrubResult(rawFilePath, displayName, ocrStatus, string.Empty, ocrFindings);
            }

            return ScrubText(rawFilePath, displayName, extractedText, "OCR_FAST_SCRUB_PREVIEW_READY", ocrFindings);
        }

        if (extension is ".doc" or ".docx")
        {
            return new ScrubResult(rawFilePath, displayName, "DOC_TEXT_EXTRACTION_REQUIRED", string.Empty, ["Word document text extraction is not enabled yet."]);
        }

        if (!TextExtensions.Contains(extension))
        {
            return new ScrubResult(rawFilePath, displayName, "UNSUPPORTED_FOR_FAST_SCRUB", string.Empty, ["Fast scrubber only previews simple text files."]);
        }

        return ScrubText(rawFilePath, displayName, File.ReadAllText(rawFilePath), "FAST_SCRUB_PREVIEW_READY", []);
    }

    private static ScrubResult ScrubText(
        string sourcePath,
        string displayName,
        string text,
        string readyStatus,
        IReadOnlyList<string> seedFindings)
    {
        var findings = new List<string>(seedFindings);
        var scrubbed = ApplyPattern(text, EmailPattern(), "[EMAIL]", "Email", findings);
        scrubbed = ApplyPattern(scrubbed, PhonePattern(), "[PHONE]", "Phone", findings);
        scrubbed = ApplyPattern(scrubbed, SsnPattern(), "[SSN]", "SSN-like number", findings);
        scrubbed = ApplyPattern(scrubbed, DatePattern(), "[DATE]", "Date", findings);
        scrubbed = ApplyPattern(scrubbed, MedicalIdPattern(), "[MEDICAL_ID]", "Medical/account ID", findings);
        scrubbed = ApplyPattern(scrubbed, AddressPattern(), "[ADDRESS]", "Address-like text", findings);

        return new ScrubResult(
            sourcePath,
            displayName,
            readyStatus,
            BuildPreview(scrubbed),
            findings,
            scrubbed);
    }

    private static string TryExtractTextWithLocalOcr(
        string rawFilePath,
        out string status,
        out IReadOnlyList<string> findings)
    {
        var localFindings = new List<string>();

        try
        {
            using var httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(2)
            };
            using var form = new MultipartFormDataContent();
            using var stream = File.OpenRead(rawFilePath);
            using var content = new StreamContent(stream);
            form.Add(content, "file", Path.GetFileName(rawFilePath));

            using var response = httpClient.PostAsync(LocalOcrEndpoint, form).GetAwaiter().GetResult();

            if (!response.IsSuccessStatusCode)
            {
                status = $"LOCAL_OCR_HTTP_{(int)response.StatusCode}";
                findings = [$"Local OCR returned HTTP {(int)response.StatusCode}."];
                return string.Empty;
            }

            var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            var result = System.Text.Json.JsonSerializer.Deserialize<LocalOcrResponse>(body);

            if (result is null || string.IsNullOrWhiteSpace(result.Text))
            {
                status = result?.Status ?? "LOCAL_OCR_EMPTY";
                findings = ["Local OCR did not return extractable text."];
                return string.Empty;
            }

            status = result.Status;
            localFindings.Add($"Local OCR: {result.Status}");
            localFindings.Add($"OCR page count: {result.PageCount}");
            localFindings.AddRange(result.Warnings.Select(warning => $"OCR warning: {warning}"));
            findings = localFindings;
            return result.Text;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or IOException)
        {
            status = exception is TaskCanceledException ? "LOCAL_OCR_TIMEOUT" : "LOCAL_OCR_UNAVAILABLE";
            findings = [$"Local OCR unavailable: {exception.Message}"];
            return string.Empty;
        }
    }

    private sealed class LocalOcrResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("page_count")]
        public int PageCount { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("warnings")]
        public List<string> Warnings { get; set; } = [];
    }

    private static string ApplyPattern(
        string input,
        Regex pattern,
        string replacement,
        string label,
        ICollection<string> findings)
    {
        var matches = pattern.Matches(input).Count;

        if (matches == 0)
        {
            return input;
        }

        findings.Add($"{label}: {matches}");
        return pattern.Replace(input, replacement);
    }

    private static string BuildPreview(string text)
    {
        const int previewLimit = 1200;
        var normalized = text.Replace("\r\n", "\n").Trim();

        return normalized.Length <= previewLimit
            ? normalized
            : normalized[..previewLimit] + "\n[TRUNCATED]";
    }

    [GeneratedRegex(@"\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b", RegexOptions.IgnoreCase)]
    private static partial Regex EmailPattern();

    [GeneratedRegex(@"(?<!\w)(?:\+?1[-.\s]?)?(?:\(?\d{3}\)?[-.\s]?)\d{3}[-.\s]?\d{4}(?!\w)")]
    private static partial Regex PhonePattern();

    [GeneratedRegex(@"\b\d{3}-\d{2}-\d{4}\b")]
    private static partial Regex SsnPattern();

    [GeneratedRegex(@"\b(?:\d{1,2}[/-]\d{1,2}[/-]\d{2,4}|\d{4}-\d{2}-\d{2})\b")]
    private static partial Regex DatePattern();

    [GeneratedRegex(@"\b(?:MRN|Medical Record|Account|Acct|Patient ID|Member ID|ID)\s*[:#-]?\s*[A-Z0-9-]{4,}\b", RegexOptions.IgnoreCase)]
    private static partial Regex MedicalIdPattern();

    [GeneratedRegex(@"\b\d{1,6}\s+[A-Za-z0-9 .'-]+\s+(?:Street|St|Avenue|Ave|Road|Rd|Boulevard|Blvd|Drive|Dr|Lane|Ln|Court|Ct|Way|Circle|Cir)\b(?:,\s*[A-Za-z .'-]+,\s*[A-Z]{2}\s+\d{5}(?:-\d{4})?)?", RegexOptions.IgnoreCase)]
    private static partial Regex AddressPattern();
}
