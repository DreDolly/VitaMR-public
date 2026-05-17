using System.IO;
using System.Text.RegularExpressions;
using VitaMR.Models;

namespace VitaMR.Services;

public sealed class SecondPassScrubberService : ISecondPassScrubberService
{
    public IReadOnlyList<SecondPassScrubResult> SaveFinalScrubbedPayloads(
        string vaultRoot,
        ChartContext chartContext,
        IReadOnlyList<ScrubResult> scrubResults,
        LocalModelReviewResult localModelReview)
    {
        if (!localModelReview.IsAvailable)
        {
            return [];
        }

        var scrubbedFolder = Path.Combine(vaultRoot, chartContext.ChartFolderName, "scrubbed");
        Directory.CreateDirectory(scrubbedFolder);

        var results = new List<SecondPassScrubResult>();

        foreach (var scrubResult in scrubResults)
        {
            if (string.IsNullOrWhiteSpace(scrubResult.FullText))
            {
                continue;
            }

            var payloadReview = localModelReview.ReviewedPayloads.FirstOrDefault(payload =>
                string.Equals(payload.SourcePath, scrubResult.SourcePath, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(payload.DisplayName, scrubResult.DisplayName, StringComparison.OrdinalIgnoreCase));
            var findings = payloadReview?.Findings ?? localModelReview.StructuredFindings;
            var deterministicText = scrubResult.FullText;
            var replacementCount = 0;

            foreach (var finding in PrivacyFindingGuardrailService.FilterAndOrderFindings(findings))
            {
                if (string.IsNullOrWhiteSpace(finding.Text))
                {
                    continue;
                }

                var replacement = string.IsNullOrWhiteSpace(finding.SuggestedReplacement)
                    ? BuildReplacementToken(finding.Type)
                    : finding.SuggestedReplacement.Trim();

                deterministicText = ReplaceExact(deterministicText, finding.Text, replacement, out var replacements);
                replacementCount += replacements;
            }

            var finalText = deterministicText;
            var status = "FINAL_SCRUBBED_PAYLOAD_SAVED";

            if (!string.IsNullOrWhiteSpace(localModelReview.RewrittenPayload))
            {
                var normalizedDeterministic = NormalizeForComparison(deterministicText);
                var normalizedRewrite = NormalizeForComparison(localModelReview.RewrittenPayload);

                if (string.Equals(normalizedDeterministic, normalizedRewrite, StringComparison.Ordinal))
                {
                    finalText = localModelReview.RewrittenPayload;
                    status = "FINAL_SCRUBBED_PAYLOAD_REWRITE_VALIDATED";
                }
                else
                {
                    status = "FINAL_SCRUBBED_PAYLOAD_REWRITE_REJECTED";
                }
            }

            if (payloadReview is not null &&
                !string.IsNullOrWhiteSpace(payloadReview.IntegrityStatus) &&
                !string.Equals(payloadReview.IntegrityStatus, "intact", StringComparison.OrdinalIgnoreCase))
            {
                status = $"FINAL_SCRUBBED_PAYLOAD_AUDIT_{payloadReview.IntegrityStatus.ToUpperInvariant()}";
            }

            var finalPath = GetNonConflictingPath(
                scrubbedFolder,
                $"{Path.GetFileNameWithoutExtension(scrubResult.DisplayName)}.final-scrubbed.txt");

            File.WriteAllText(finalPath, NormalizeContent(finalText));

            results.Add(new SecondPassScrubResult(
                scrubResult.SourcePath,
                Path.GetFileName(finalPath),
                status,
                finalPath,
                replacementCount,
                BuildPreview(finalText)));
        }

        return results;
    }

    private static string ReplaceExact(string input, string text, string replacement, out int replacementCount)
    {
        var pattern = Regex.Escape(text.Trim());
        replacementCount = Regex.Matches(input, pattern, RegexOptions.IgnoreCase).Count;

        return replacementCount == 0
            ? input
            : Regex.Replace(input, pattern, replacement, RegexOptions.IgnoreCase);
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

    private static string BuildPreview(string text)
    {
        const int previewLimit = 1200;
        var normalized = text.Replace("\r\n", "\n").Trim();

        return normalized.Length <= previewLimit
            ? normalized
            : normalized[..previewLimit] + "\n[TRUNCATED]";
    }

    private static string NormalizeContent(string content)
    {
        return content.Replace("\r\n", "\n").TrimEnd() + Environment.NewLine;
    }

    private static string NormalizeForComparison(string content)
    {
        return content.Replace("\r\n", "\n").TrimEnd();
    }
}
