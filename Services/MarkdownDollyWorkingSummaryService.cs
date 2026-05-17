using System.IO;
using System.Text;
using VitaMR.Models;

namespace VitaMR.Services;

public sealed class MarkdownDollyWorkingSummaryService : IDollyWorkingSummaryService
{
    public DollyWorkingSummaryResult RefreshSummary(string vaultRoot, ChartContext chartContext)
    {
        var result = new DollyWorkingSummaryResult
        {
            ChartId = chartContext.ChartId,
            PatientDisplayName = chartContext.LocalDisplayName
        };

        var wikiFolder = Path.Combine(vaultRoot, chartContext.ChartFolderName, "wiki");
        Directory.CreateDirectory(wikiFolder);
        var summaryPath = Path.Combine(wikiFolder, "Dolly_Working_Summary.md");
        result.SummaryPath = summaryPath;

        var builder = new StringBuilder();
        builder.AppendLine($"# Dolly Working Summary: {chartContext.ChartId}");
        builder.AppendLine();
        builder.AppendLine("> Sterile working packet for Dolly/Gemma conversation. This file is built from wiki context only; raw files stay outside normal conversation.");
        builder.AppendLine();
        builder.AppendLine("## Scope");
        builder.AppendLine($"- Chart: {chartContext.ChartId}");
        builder.AppendLine($"- Local display: {FirstName(chartContext.LocalDisplayName)}");
        builder.AppendLine($"- Refreshed: {DateTime.Now:yyyy-MM-dd HH:mm}");
        builder.AppendLine("- Status: prototype, report-only, no diagnosis/triage/treatment automation");
        result.SectionsIncluded++;

        AppendSection(builder, result, wikiFolder, "Diagnoses And Conditions", "Index.md", "## Diagnoses / Conditions", 8);
        AppendSection(builder, result, wikiFolder, "Medications", "Index.md", "## Medications", 8);
        AppendSection(builder, result, wikiFolder, "Vaccines", "Vaccines.md", null, 12);
        AppendSection(builder, result, wikiFolder, "Open Care Gaps", "Care_Gaps.md", null, 8);
        AppendSection(builder, result, wikiFolder, "Active Conflicts And Safety Flags", "Conflicts.md", null, 8);
        AppendSection(builder, result, wikiFolder, "Recent Timeline", "Timeline.md", null, 8);
        AppendSection(builder, result, wikiFolder, "Symptom Patterns", "Symptom_Patterns.md", null, 10);
        AppendSection(builder, result, wikiFolder, "Health Goals", "Health_Goals.md", null, 8);
        AppendSection(builder, result, wikiFolder, "Drug Interaction Prototype Output", "Drug_Interactions.md", null, 8);

        builder.AppendLine("## Confirmed Vs Unverified Facts");
        builder.AppendLine("- Treat rows marked unverified, pending, user-reported, or prototype as unverified.");
        builder.AppendLine("- Use source links and chart file names when answering.");
        builder.AppendLine("- Ask C# for a fresh backend context packet before complex retrieval or wiki updates.");
        builder.AppendLine();
        result.SectionsIncluded++;

        builder.AppendLine("## Conversation Hooks");
        foreach (var hook in BuildConversationHooks(wikiFolder).Take(6))
        {
            builder.AppendLine($"- {hook}");
            result.ConversationHooks.Add(hook);
        }

        if (result.ConversationHooks.Count == 0)
        {
            builder.AppendLine("- No high-signal hooks found yet; ask what the user wants to work on for this chart.");
        }

        builder.AppendLine();
        builder.AppendLine("## Boundaries");
        builder.AppendLine("- Dolly may answer from this summary for lightweight status and orientation.");
        builder.AppendLine("- Dolly should request C#/Gemini refresh for complex chart questions, new writes, extraction, or stale/missing context.");
        builder.AppendLine("- No raw source access during normal conversation.");
        builder.AppendLine("- No autonomous clinical advice, diagnosis, triage, orders, or real-world escalation.");

        File.WriteAllText(summaryPath, Normalize(builder.ToString()));
        result.WasWritten = true;
        result.Status = "DOLLY_WORKING_SUMMARY_READY";
        result.Messages.Add($"Working summary refreshed for {FirstName(chartContext.LocalDisplayName)}.");
        result.Messages.Add($"Summary written: {Path.GetFileName(summaryPath)}.");
        return result;
    }

    private static void AppendSection(
        StringBuilder builder,
        DollyWorkingSummaryResult result,
        string wikiFolder,
        string title,
        string fileName,
        string? heading,
        int maxLines)
    {
        var path = Path.Combine(wikiFolder, fileName);
        builder.AppendLine($"## {title}");

        if (!File.Exists(path))
        {
            builder.AppendLine($"- Missing: {fileName}");
            builder.AppendLine();
            return;
        }

        var lines = heading is null
            ? ReadSignalLines(path, maxLines)
            : ReadHeadingLines(path, heading, maxLines);

        if (lines.Count == 0)
        {
            builder.AppendLine("- No current entries found.");
        }
        else
        {
            foreach (var line in lines)
            {
                builder.AppendLine(line);
            }
        }

        builder.AppendLine();
        result.SectionsIncluded++;
    }

    private static IReadOnlyList<string> ReadHeadingLines(string path, string heading, int maxLines)
    {
        var lines = File.ReadAllLines(path);
        var start = Array.FindIndex(lines, line => line.Trim().Equals(heading, StringComparison.OrdinalIgnoreCase));

        if (start < 0)
        {
            return [];
        }

        return lines
            .Skip(start + 1)
            .TakeWhile(line => !line.StartsWith("## ", StringComparison.Ordinal))
            .Where(IsUsefulLine)
            .Take(maxLines)
            .ToList();
    }

    private static IReadOnlyList<string> ReadSignalLines(string path, int maxLines)
    {
        return File.ReadAllLines(path)
            .Where(IsUsefulLine)
            .Take(maxLines)
            .ToList();
    }

    private static IEnumerable<string> BuildConversationHooks(string wikiFolder)
    {
        foreach (var row in ReadTableRows(Path.Combine(wikiFolder, "Care_Gaps.md"))
                     .Where(row => row.Count >= 4 && row[3].Contains("open", StringComparison.OrdinalIgnoreCase))
                     .Take(3))
        {
            yield return $"Open care gap to keep in view: {row[1]} ({row[2]} priority).";
        }

        foreach (var row in ReadTableRows(Path.Combine(wikiFolder, "Conflicts.md"))
                     .Where(row => row.Count >= 5 && row[3].Contains("active", StringComparison.OrdinalIgnoreCase))
                     .Take(2))
        {
            yield return $"Active conflict/safety flag: {StripMarkdown(row[4])}.";
        }

        var briefPath = Path.Combine(wikiFolder, "Pre_Visit_Brief.md");
        if (File.Exists(briefPath))
        {
            var refreshed = File.ReadLines(briefPath)
                .FirstOrDefault(line => line.Contains("Generated:", StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(refreshed))
            {
                yield return $"Pre-visit brief exists ({StripMarkdown(refreshed)}).";
            }
        }
    }

    private static IReadOnlyList<IReadOnlyList<string>> ReadTableRows(string path)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        return File.ReadAllLines(path)
            .Select(ParseRow)
            .Where(row => row.Count > 0 && !row[0].Contains("ID", StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private static IReadOnlyList<string> ParseRow(string line)
    {
        var trimmed = line.Trim();

        if (!trimmed.StartsWith('|') || trimmed.Contains("---", StringComparison.Ordinal))
        {
            return [];
        }

        return trimmed.Trim('|').Split('|').Select(cell => cell.Trim()).ToList();
    }

    private static bool IsUsefulLine(string line)
    {
        var trimmed = line.Trim();
        return !string.IsNullOrWhiteSpace(trimmed) &&
               !trimmed.StartsWith('#') &&
               !trimmed.Contains("---", StringComparison.Ordinal) &&
               !trimmed.StartsWith("| Date |", StringComparison.OrdinalIgnoreCase) &&
               !trimmed.StartsWith("| Condition |", StringComparison.OrdinalIgnoreCase) &&
               !trimmed.StartsWith("| Medication |", StringComparison.OrdinalIgnoreCase);
    }

    private static string FirstName(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "the active chart"
            : value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? value.Trim();
    }

    private static string StripMarkdown(string value)
    {
        return value.Replace("**", string.Empty, StringComparison.Ordinal).Trim();
    }

    private static string Normalize(string content)
    {
        return content.Replace("\r\n", "\n").TrimEnd() + Environment.NewLine;
    }
}
