using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using VitaMR.Models;

namespace VitaMR.Services;

public sealed partial class DeterministicImageDocumentSafetyService : IImageDocumentReviewService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
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

    private static readonly HashSet<string> ReviewExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf"
    };

    public Task<IReadOnlyList<ImageDocumentReviewResult>> ReviewImageDocumentsAsync(
        string vaultRoot,
        ChartContext chartContext,
        string endpoint,
        string modelName,
        IReadOnlyList<IngestResult> ingestResults,
        CancellationToken cancellationToken = default)
    {
        var candidates = ingestResults
            .Where(result => ShouldReview(result.RawVaultPath))
            .ToList();

        if (candidates.Count == 0)
        {
            return Task.FromResult<IReadOnlyList<ImageDocumentReviewResult>>([]);
        }

        var results = candidates
            .Select(result => ReviewOne(vaultRoot, chartContext, result))
            .ToList();

        return Task.FromResult<IReadOnlyList<ImageDocumentReviewResult>>(results);
    }

    private static ImageDocumentReviewResult ReviewOne(
        string vaultRoot,
        ChartContext chartContext,
        IngestResult ingestResult)
    {
        var displayName = ingestResult.DisplayName;
        var extractedText = TryReadExtractedTextFromScrubbedPreview(ingestResult.RawVaultPath);
        var combined = $"{displayName}\n{extractedText}";
        var result = new ImageDocumentReviewResult
        {
            SourcePath = ingestResult.RawVaultPath,
            DisplayName = displayName,
            WasAttempted = true,
            IsAvailable = true,
            Status = "DETERMINISTIC_IMAGE_GATE_READY",
            ImageDocumentType = DetectDocumentType(combined),
            RawResponse = BuildRawSummary(combined)
        };

        result.IsRadiologyOrCardiology = IsRadiologyOrCardiology(result.ImageDocumentType, combined);
        result.OfficialReadPresent = HasOfficialReadText(combined);
        result.OfficialReadRequired = result.IsRadiologyOrCardiology && !result.OfficialReadPresent;
        result.ClinicalInterpretationAttempted = false;

        if (result.OfficialReadRequired)
        {
            result.RequiredFollowup = "Official radiologist/cardiologist report needed before this image can update the sterile wiki.";
        }
        else if (result.IsRadiologyOrCardiology)
        {
            result.RequiredFollowup = "Official report text appears present; OCR text still goes through the Presidio privacy gate before API use.";
        }
        else
        {
            result.RequiredFollowup = "No radiology/cardiology official-read block detected by deterministic safety rules.";
        }

        return SaveResult(vaultRoot, chartContext, result);
    }

    private static string TryReadExtractedTextFromScrubbedPreview(string rawVaultPath)
    {
        if (!File.Exists(rawVaultPath))
        {
            return string.Empty;
        }

        var extension = Path.GetExtension(rawVaultPath);
        if (!extension.Equals(".txt", StringComparison.OrdinalIgnoreCase) &&
            !extension.Equals(".md", StringComparison.OrdinalIgnoreCase) &&
            !extension.Equals(".csv", StringComparison.OrdinalIgnoreCase) &&
            !extension.Equals(".json", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        try
        {
            return File.ReadAllText(rawVaultPath);
        }
        catch (IOException)
        {
            return string.Empty;
        }
        catch (UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }

    private static string DetectDocumentType(string value)
    {
        if (EchoPattern().IsMatch(value))
        {
            return "echo";
        }

        if (EkgPattern().IsMatch(value))
        {
            return "ekg";
        }

        if (CtPattern().IsMatch(value))
        {
            return "ct";
        }

        if (MriPattern().IsMatch(value))
        {
            return "mri";
        }

        if (UltrasoundPattern().IsMatch(value))
        {
            return "ultrasound";
        }

        if (XrayPattern().IsMatch(value))
        {
            return "xray";
        }

        if (RadiologyPattern().IsMatch(value))
        {
            return "radiology_document";
        }

        if (CardiologyPattern().IsMatch(value))
        {
            return "cardiology_document";
        }

        return "other";
    }

    private static bool IsRadiologyOrCardiology(string documentType, string value)
    {
        return documentType is "xray" or "ct" or "mri" or "ultrasound" or "ekg" or "echo" or "radiology_document" or "cardiology_document" ||
               RadiologyPattern().IsMatch(value) ||
               CardiologyPattern().IsMatch(value);
    }

    private static bool HasOfficialReadText(string value)
    {
        if (!OfficialReportPattern().IsMatch(value))
        {
            return false;
        }

        return FindingsPattern().IsMatch(value) ||
               ImpressionPattern().IsMatch(value) ||
               InterpretedByPattern().IsMatch(value);
    }

    private static ImageDocumentReviewResult SaveResult(
        string vaultRoot,
        ChartContext chartContext,
        ImageDocumentReviewResult result)
    {
        var imageReviewFolder = Path.Combine(vaultRoot, chartContext.ChartFolderName, "scrubbed", "image-review");
        Directory.CreateDirectory(imageReviewFolder);

        var metadataPath = GetNonConflictingPath(
            imageReviewFolder,
            $"{Path.GetFileNameWithoutExtension(result.DisplayName)}.deterministic-image-gate.json");

        File.WriteAllText(metadataPath, JsonSerializer.Serialize(result, JsonOptions));
        result.SavedMetadataPath = metadataPath;

        if (result.OfficialReadRequired && !result.OfficialReadPresent)
        {
            AppendFutureDataNeeded(vaultRoot, chartContext, result);
        }

        return result;
    }

    private static void AppendFutureDataNeeded(
        string vaultRoot,
        ChartContext chartContext,
        ImageDocumentReviewResult result)
    {
        var wikiFolder = Path.Combine(vaultRoot, chartContext.ChartFolderName, "wiki");
        Directory.CreateDirectory(wikiFolder);

        var path = Path.Combine(wikiFolder, "Future_Data_Needed.md");
        var line = $"- {DateTime.Now:yyyy-MM-dd HH:mm}: {result.RequiredFollowup} Source: {result.DisplayName}{Environment.NewLine}";

        if (!File.Exists(path))
        {
            File.WriteAllText(path, $"# Future Data Needed{Environment.NewLine}{Environment.NewLine}");
        }

        File.AppendAllText(path, line);
    }

    private static bool ShouldReview(string path)
    {
        var extension = Path.GetExtension(path);
        return ImageExtensions.Contains(extension) || ReviewExtensions.Contains(extension);
    }

    private static string BuildRawSummary(string value)
    {
        var normalized = Regex.Replace(value, @"\s+", " ").Trim();
        return normalized.Length <= 600 ? normalized : normalized[..600] + " [TRUNCATED]";
    }

    private static string GetNonConflictingPath(string folder, string fileName)
    {
        var targetPath = Path.Combine(folder, fileName);

        if (!File.Exists(targetPath))
        {
            return targetPath;
        }

        var name = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);

        for (var index = 2; ; index++)
        {
            var candidate = Path.Combine(folder, $"{name}_{index}{extension}");

            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }
    }

    [GeneratedRegex(@"\b(?:x\s*-?\s*ray|xray|radiograph)\b", RegexOptions.IgnoreCase)]
    private static partial Regex XrayPattern();

    [GeneratedRegex(@"\b(?:ct|computed tomography)\b", RegexOptions.IgnoreCase)]
    private static partial Regex CtPattern();

    [GeneratedRegex(@"\b(?:mri|magnetic resonance)\b", RegexOptions.IgnoreCase)]
    private static partial Regex MriPattern();

    [GeneratedRegex(@"\b(?:ultrasound|sonogram|doppler)\b", RegexOptions.IgnoreCase)]
    private static partial Regex UltrasoundPattern();

    [GeneratedRegex(@"\b(?:ekg|ecg|electrocardiogram)\b", RegexOptions.IgnoreCase)]
    private static partial Regex EkgPattern();

    [GeneratedRegex(@"\b(?:echo|echocardiogram|echocardiography)\b", RegexOptions.IgnoreCase)]
    private static partial Regex EchoPattern();

    [GeneratedRegex(@"\b(?:radiology|radiologist|imaging|exam(?:ination)?\s*:\s*(?:x\s*-?\s*ray|ct|mri|ultrasound))\b", RegexOptions.IgnoreCase)]
    private static partial Regex RadiologyPattern();

    [GeneratedRegex(@"\b(?:cardiology|cardiologist|ekg|ecg|echo|echocardiogram)\b", RegexOptions.IgnoreCase)]
    private static partial Regex CardiologyPattern();

    [GeneratedRegex(@"\b(?:report|final report|official report|signed|electronically signed|dictated|transcribed)\b", RegexOptions.IgnoreCase)]
    private static partial Regex OfficialReportPattern();

    [GeneratedRegex(@"\bfindings?\s*:", RegexOptions.IgnoreCase)]
    private static partial Regex FindingsPattern();

    [GeneratedRegex(@"\bimpression\s*:", RegexOptions.IgnoreCase)]
    private static partial Regex ImpressionPattern();

    [GeneratedRegex(@"\b(?:interpreted|reviewed|signed|reported)\s+by\b|\b(?:radiologist|cardiologist)\s*:", RegexOptions.IgnoreCase)]
    private static partial Regex InterpretedByPattern();
}
