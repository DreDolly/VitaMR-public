using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using VitaMR.Models;

namespace VitaMR.Services;

public sealed partial class MarkdownWikiPatchApplyService : IWikiPatchApplyService
{
    public WikiPatchApplyResult Apply(
        string vaultRoot,
        ChartContext chartContext,
        GeminiWikiPatchResult patch,
        IngestResult rawManualSource)
    {
        var result = new WikiPatchApplyResult();

        if (!patch.ShouldApply || patch.Updates.Count == 0)
        {
            result.Messages.Add("No wiki patch was applied.");
            return result;
        }

        var wikiFolder = Path.Combine(vaultRoot, chartContext.ChartFolderName, "wiki");
        Directory.CreateDirectory(wikiFolder);
        var date = DateTime.Now.ToString("yyyy-MM-dd");
        var sourceLink = $"[[raw/{Path.GetFileNameWithoutExtension(rawManualSource.RawVaultPath)}]]";

        var manualNodePath = WriteManualUpdateNode(wikiFolder, chartContext, patch, sourceLink, date);
        result.UpdatedFiles.Add(manualNodePath);

        foreach (var update in patch.Updates)
        {
            var topicPath = AppendTopicUpdate(wikiFolder, chartContext, update, sourceLink, date);
            AddUnique(result.UpdatedFiles, topicPath);
        }

        AddUnique(result.UpdatedFiles, AppendTimelineUpdate(wikiFolder, patch, sourceLink, date));
        AddUnique(result.UpdatedFiles, AppendIndexUpdate(wikiFolder, patch, sourceLink, date));
        AddUnique(result.UpdatedFiles, AppendCareGapUpdates(wikiFolder, patch, sourceLink, date));
        AddUnique(result.UpdatedFiles, EmergencyCardService.WriteEmergencyCard(wikiFolder, chartContext));

        result.WasApplied = true;
        result.Messages.Add(string.IsNullOrWhiteSpace(patch.PatchSummary)
            ? $"Applied {patch.Updates.Count} user-reported wiki update(s)."
            : patch.PatchSummary);

        return result;
    }

    private static string WriteManualUpdateNode(
        string wikiFolder,
        ChartContext chartContext,
        GeminiWikiPatchResult patch,
        string sourceLink,
        string date)
    {
        var folder = Path.Combine(wikiFolder, "manual_updates");
        Directory.CreateDirectory(folder);
        var path = GetNonConflictingPath(folder, $"{DateTime.Now:yyyy-MM-dd_HHmmss}_Manual_Update.md");
        var builder = new StringBuilder();

        builder.AppendLine("---");
        builder.AppendLine($"chart_id: \"{chartContext.ChartId}\"");
        builder.AppendLine("document_type: \"Manual_Entry_Update\"");
        builder.AppendLine($"date_of_service: {date}");
        builder.AppendLine("risk_level: low");
        builder.AppendLine("dolly_directive: \"WIKI_NATIVE\"");
        builder.AppendLine($"raw_source: \"{sourceLink}\"");
        builder.AppendLine("source_type: \"Manual_Entry\"");
        builder.AppendLine("verification_status: \"user_reported_unverified\"");
        builder.AppendLine("status: unverified");
        builder.AppendLine("---");
        builder.AppendLine();
        builder.AppendLine($"# Manual Update - {date}");
        builder.AppendLine();
        builder.AppendLine($"> **Summary:** {NormalizeParagraph(patch.PatchSummary, "User-reported chart update.")}");
        builder.AppendLine();
        builder.AppendLine("## Updates");
        builder.AppendLine();

        foreach (var update in patch.Updates)
        {
            builder.AppendLine($"- **{NormalizeText(update.Topic, "General Update")}:** {NormalizeUpdateValue(update)}");
            builder.AppendLine($"  - Status: {NormalizeText(update.Status, "user_reported")}");
            builder.AppendLine($"  - Verification: {NormalizeText(update.VerificationStatus, "user_reported_unverified")}");

            if (!string.IsNullOrWhiteSpace(update.ReplacesPendingText))
            {
                builder.AppendLine($"  - Related pending item: {update.ReplacesPendingText.Trim()}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Source");
        builder.AppendLine();
        builder.AppendLine($"- {sourceLink}");

        File.WriteAllText(path, NormalizeContent(builder.ToString()));
        return path;
    }

    private static string AppendTopicUpdate(
        string wikiFolder,
        ChartContext chartContext,
        WikiFactUpdate update,
        string sourceLink,
        string date)
    {
        var topic = NormalizeText(update.Topic, "Manual Update");
        var path = Path.Combine(wikiFolder, $"{BuildSafeSlug(topic)}.md");
        EnsureTopicPage(path, chartContext, topic, update);
        var marker = $"<!-- manual-update:{DateTime.Now:yyyyMMddHHmmssfff}:{BuildSafeSlug(topic)} -->";
        var builder = new StringBuilder(File.ReadAllText(path).TrimEnd());

        builder.AppendLine();
        builder.AppendLine();
        builder.AppendLine($"## Manual Update: {date}");
        builder.AppendLine(marker);
        builder.AppendLine();
        builder.AppendLine($"- **Value:** {NormalizeUpdateValue(update)}");
        builder.AppendLine($"- **Status:** {NormalizeText(update.Status, "user_reported")}");
        builder.AppendLine($"- **Verification:** {NormalizeText(update.VerificationStatus, "user_reported_unverified")}");
        builder.AppendLine($"- **Summary:** {NormalizeParagraph(update.Summary, $"User-reported update for {topic}.")}");

        if (!string.IsNullOrWhiteSpace(update.ReplacesPendingText))
        {
            builder.AppendLine($"- **Related pending item:** {update.ReplacesPendingText.Trim()}");
        }

        builder.AppendLine($"- **Source:** {sourceLink}");

        File.WriteAllText(path, NormalizeContent(builder.ToString()));
        return path;
    }

    private static string AppendTimelineUpdate(
        string wikiFolder,
        GeminiWikiPatchResult patch,
        string sourceLink,
        string date)
    {
        var path = Path.Combine(wikiFolder, "Timeline.md");
        EnsureTimeline(path);
        var content = File.ReadAllText(path);
        var row = $"| {date} | Manual Entry Update | {NormalizeTableCell(patch.PatchSummary)} | low | {sourceLink} |";
        content = InsertAfterTableHeader(content, string.Empty, row);
        File.WriteAllText(path, NormalizeContent(content));
        return path;
    }

    private static string AppendIndexUpdate(
        string wikiFolder,
        GeminiWikiPatchResult patch,
        string sourceLink,
        string date)
    {
        var path = Path.Combine(wikiFolder, "Index.md");
        EnsureIndex(path);
        var content = File.ReadAllText(path);

        foreach (var update in patch.Updates)
        {
            content = MarkMatchingPendingItemsResolved(content, update, sourceLink);

            var category = NormalizeText(update.Category, "general").ToLowerInvariant();
            var section = category switch
            {
                "lab" => "## Lab Results",
                "vital" => "## Vitals History",
                "medication" => "## Medications",
                "diagnosis" => "## Diagnoses / Conditions",
                "plan" => "## Care Plan Updates",
                "pending" => "## Pending Items",
                _ => "## Manual Updates"
            };
            content = EnsureSectionTable(content, section, category);
            content = InsertAfterTableHeader(
                content,
                section,
                BuildIndexRow(update, category, sourceLink, date));
        }

        File.WriteAllText(path, NormalizeContent(content));
        return path;
    }

    private static string AppendCareGapUpdates(
        string wikiFolder,
        GeminiWikiPatchResult patch,
        string sourceLink,
        string date)
    {
        var path = Path.Combine(wikiFolder, "Care_Gaps.md");
        EnsureCareGaps(path);
        var content = File.ReadAllText(path);

        foreach (var update in patch.Updates.Where(IsCareGapUpdate))
        {
            var title = NormalizeText(update.Topic, NormalizeUpdateValue(update));

            if (content.Contains(title, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var row = $"| {NextSequentialId(content, "GAP")} | {NormalizeTableCell(title)} | routine | open | {date} | none | user | {NormalizeTableCell(NormalizeUpdateValue(update))} {sourceLink} | none | 0 |";
            content = InsertAfterTableHeader(content, string.Empty, row);
        }

        File.WriteAllText(path, NormalizeContent(content));
        return path;
    }

    private static void EnsureTopicPage(
        string path,
        ChartContext chartContext,
        string topic,
        WikiFactUpdate update)
    {
        if (File.Exists(path))
        {
            return;
        }

        var builder = new StringBuilder();
        builder.AppendLine("---");
        builder.AppendLine($"chart_id: \"{chartContext.ChartId}\"");
        builder.AppendLine("document_type: \"Topic_Page\"");
        builder.AppendLine($"topic_category: \"{NormalizeText(update.Category, "general")}\"");
        builder.AppendLine("risk_level: low");
        builder.AppendLine("dolly_directive: \"WIKI_NATIVE\"");
        builder.AppendLine("conflict_status: none");
        builder.AppendLine("status: unverified");
        builder.AppendLine("---");
        builder.AppendLine();
        builder.AppendLine($"# {topic}");
        builder.AppendLine();
        builder.AppendLine($"> **Summary:** Living topic page for {topic}.");

        File.WriteAllText(path, NormalizeContent(builder.ToString()));
    }

    private static void EnsureIndex(string path)
    {
        if (!File.Exists(path))
        {
            File.WriteAllText(path, "# Index\n");
        }
    }

    private static void EnsureTimeline(string path)
    {
        if (!File.Exists(path))
        {
            File.WriteAllText(path, "# Timeline\n\n| Date | Event Type | Summary | Risk Level | Source |\n|---|---|---|---|---|\n");
        }
    }

    private static void EnsureCareGaps(string path)
    {
        if (!File.Exists(path))
        {
            File.WriteAllText(
                path,
                "# Care Gaps\n\n| Gap ID | Title | Priority | Status | Date Added | Due Date | Source | Notes | Surfaced At | Surface Count |\n|---|---|---|---|---|---|---|---|---|---|\n");
        }
    }

    private static string EnsureSectionTable(string content, string section, string category)
    {
        if (content.Contains(section, StringComparison.OrdinalIgnoreCase))
        {
            return content;
        }

        var header = category switch
        {
            "medication" => "| Medication | Dose | Status | Source |",
            "diagnosis" => "| Condition | Status | Risk Level | Source |",
            "pending" => "| Item | Priority | Status | Source |",
            "lab" => "| Date | Summary | Risk Level | Source |",
            "vital" => "| Date | Vital | Value | Source |",
            _ => "| Date | Topic | Value / Update | Verification | Source |"
        };
        var divider = string.Join('|', header.Split('|').Select((part, index) =>
            index == 0 || index == header.Split('|').Length - 1 ? string.Empty : "---"));

        return content.TrimEnd() +
               Environment.NewLine +
               Environment.NewLine +
               section +
               Environment.NewLine +
               header +
               Environment.NewLine +
               divider +
               Environment.NewLine;
    }

    private static string BuildIndexRow(WikiFactUpdate update, string category, string sourceLink, string date)
    {
        var topic = NormalizeTableCell(update.Topic);
        var value = NormalizeTableCell(NormalizeUpdateValue(update));
        var verification = NormalizeTableCell(update.VerificationStatus);

        return category switch
        {
            "medication" => $"| {topic} | {value} | {verification} | {sourceLink} |",
            "diagnosis" => $"| {topic} | {verification} | low | {sourceLink} |",
            "pending" => $"| {topic} | routine | open | {sourceLink} |",
            "lab" => $"| {date} | {topic}: {value} ({verification}) | low | {sourceLink} |",
            "vital" => $"| {date} | {topic} | {value} ({verification}) | {sourceLink} |",
            _ => $"| {date} | {topic} | {value} | {verification} | {sourceLink} |"
        };
    }

    private static string MarkMatchingPendingItemsResolved(string content, WikiFactUpdate update, string sourceLink)
    {
        if (string.IsNullOrWhiteSpace(update.ReplacesPendingText))
        {
            return content;
        }

        var lines = content.Replace("\r\n", "\n").Split('\n').ToList();
        var needle = update.ReplacesPendingText.Trim();

        for (var index = 0; index < lines.Count; index++)
        {
            if (!lines[index].Contains(needle, StringComparison.OrdinalIgnoreCase) ||
                !lines[index].StartsWith('|'))
            {
                continue;
            }

            lines[index] = Regex.Replace(
                lines[index],
                @"\|\s*open\s*\|",
                $"| resolved_by_manual_update |",
                RegexOptions.IgnoreCase);

            if (!lines[index].Contains(sourceLink, StringComparison.OrdinalIgnoreCase))
            {
                lines[index] = lines[index].TrimEnd('|', ' ') + $"; updated by {sourceLink} |";
            }
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string InsertAfterTableHeader(string content, string sectionName, string row)
    {
        var lines = content.Replace("\r\n", "\n").Split('\n').ToList();
        var start = string.IsNullOrWhiteSpace(sectionName)
            ? 0
            : lines.FindIndex(line => line.Trim().Equals(sectionName, StringComparison.OrdinalIgnoreCase));

        if (start < 0)
        {
            return content.TrimEnd() + Environment.NewLine + Environment.NewLine + sectionName + Environment.NewLine + row + Environment.NewLine;
        }

        for (var index = start; index < lines.Count; index++)
        {
            if (lines[index].StartsWith("|---", StringComparison.Ordinal))
            {
                lines.Insert(index + 1, row);
                return string.Join(Environment.NewLine, lines);
            }
        }

        lines.Add(row);
        return string.Join(Environment.NewLine, lines);
    }

    private static string NormalizeUpdateValue(WikiFactUpdate update)
    {
        var value = NormalizeText(update.NewValue, update.Summary);
        if (string.IsNullOrWhiteSpace(update.Unit) ||
            value.Contains(update.Unit.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        return string.IsNullOrWhiteSpace(update.Unit)
            ? value
            : $"{value} {update.Unit.Trim()}";
    }

    private static string NormalizeText(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value)
            ? fallback
            : Regex.Replace(value.Trim(), @"\s+", " ");
    }

    private static string NormalizeParagraph(string value, string fallback)
    {
        return NormalizeText(value, fallback).Replace("|", "/");
    }

    private static string NormalizeTableCell(string value)
    {
        var normalized = NormalizeParagraph(value, "Manual update")
            .Replace("\r", " ")
            .Replace("\n", " ");

        return normalized.Length <= 220 ? normalized : normalized[..220] + "...";
    }

    private static string BuildSafeSlug(string value)
    {
        var slug = Regex.Replace(NormalizeText(value, "Manual_Update"), @"[^A-Za-z0-9]+", "_").Trim('_');
        return string.IsNullOrWhiteSpace(slug) ? "Manual_Update" : slug;
    }

    private static bool IsCareGapUpdate(WikiFactUpdate update)
    {
        var combined = $"{update.Category} {update.Topic} {update.NewValue} {update.Summary}";

        return update.Category.Equals("pending", StringComparison.OrdinalIgnoreCase) ||
               combined.Contains("pending", StringComparison.OrdinalIgnoreCase) ||
               combined.Contains("follow", StringComparison.OrdinalIgnoreCase) ||
               combined.Contains("schedule", StringComparison.OrdinalIgnoreCase) ||
               combined.Contains("referral", StringComparison.OrdinalIgnoreCase) ||
               combined.Contains("ordered", StringComparison.OrdinalIgnoreCase) ||
               combined.Contains("await", StringComparison.OrdinalIgnoreCase);
    }

    private static string NextSequentialId(string content, string prefix)
    {
        var matches = Regex.Matches(content, $@"\b{Regex.Escape(prefix)}-(\d{{3}})\b", RegexOptions.IgnoreCase);
        var next = matches
            .Select(match => int.TryParse(match.Groups[1].Value, out var number) ? number : 0)
            .DefaultIfEmpty(0)
            .Max() + 1;

        return $"{prefix}-{next:000}";
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

    private static void AddUnique(ICollection<string> values, string value)
    {
        if (!values.Contains(value, StringComparer.OrdinalIgnoreCase))
        {
            values.Add(value);
        }
    }

    private static string NormalizeContent(string content)
    {
        return content.Replace("\r\n", "\n").TrimEnd() + Environment.NewLine;
    }
}
