using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using VitaMR.Models;

namespace VitaMR.Services;

public static class EmergencyCardService
{
    public static string WriteEmergencyCard(string wikiFolder, ChartContext chartContext)
    {
        Directory.CreateDirectory(wikiFolder);
        var path = Path.Combine(wikiFolder, "Emergency_Card.md");
        var indexPath = Path.Combine(wikiFolder, "Index.md");
        var careGapsPath = Path.Combine(wikiFolder, "Care_Gaps.md");
        var conflictsPath = Path.Combine(wikiFolder, "Conflicts.md");
        var timelinePath = Path.Combine(wikiFolder, "Timeline.md");

        var index = File.Exists(indexPath) ? File.ReadAllText(indexPath) : string.Empty;
        var careGaps = File.Exists(careGapsPath) ? File.ReadAllText(careGapsPath) : string.Empty;
        var conflicts = File.Exists(conflictsPath) ? File.ReadAllText(conflictsPath) : string.Empty;
        var timeline = File.Exists(timelinePath) ? File.ReadAllText(timelinePath) : string.Empty;

        var builder = new StringBuilder();
        builder.AppendLine("---");
        builder.AppendLine($"chart_id: \"{EscapeYaml(chartContext.ChartId)}\"");
        builder.AppendLine("document_type: \"Emergency_Card\"");
        builder.AppendLine($"generated_at: {DateTime.Now:yyyy-MM-dd}");
        builder.AppendLine("status: auto_generated");
        builder.AppendLine("source: \"wiki_index_care_gaps_conflicts_timeline\"");
        builder.AppendLine("---");
        builder.AppendLine();
        builder.AppendLine("# Emergency Card");
        builder.AppendLine();
        builder.AppendLine($"> **Patient:** {EscapeMarkdown(FirstNameOnly(chartContext.LocalDisplayName))}");
        builder.AppendLine($"> **Chart ID:** {chartContext.ChartId}");
        builder.AppendLine($"> **Generated:** {DateTime.Now:yyyy-MM-dd HH:mm}");
        builder.AppendLine();
        builder.AppendLine("> **Safety note:** This card is auto-generated from the sterile VitaMR wiki. Confirm against source documents for high-risk or time-sensitive decisions.");
        builder.AppendLine();
        AppendSection(builder, "Active Diagnoses / Conditions", ExtractIndexSection(index, "## Diagnoses / Conditions", 0, row => IsStatus(row, 1, "active")), "No active diagnoses documented in the wiki index.");
        AppendSection(builder, "Current Medications", ExtractIndexSection(index, "## Medications", 0, row => IsStatus(row, 2, "active")), "No active medications documented in the wiki index.");
        AppendSection(builder, "Open Care Gaps", ExtractTableValues(careGaps, 1, row => IsStatus(row, 3, "open")).Take(8), "No open care gaps documented.");
        AppendSection(builder, "Active Conflicts / Human Review", ExtractTableValues(conflicts, 4, row => IsStatus(row, 3, "active")).Take(8), "No active conflicts documented.");
        AppendSection(builder, "Recent Timeline", ExtractTableValues(timeline, 2).Take(5), "No timeline events documented.");
        builder.AppendLine("## Source Files");
        builder.AppendLine();
        builder.AppendLine("- [[Index]]");
        builder.AppendLine("- [[Care_Gaps]]");
        builder.AppendLine("- [[Conflicts]]");
        builder.AppendLine("- [[Timeline]]");

        File.WriteAllText(path, NormalizeContent(builder.ToString()));
        return path;
    }

    private static IEnumerable<string> ExtractIndexSection(
        string content,
        string section,
        int valueColumn,
        Func<IReadOnlyList<string>, bool>? predicate = null)
    {
        return ExtractTableValues(ReadSection(content, section), valueColumn, predicate);
    }

    private static IEnumerable<string> ExtractTableValues(
        string content,
        int valueColumn,
        Func<IReadOnlyList<string>, bool>? predicate = null)
    {
        foreach (var row in ReadTableRows(content))
        {
            if (row.Count <= valueColumn || predicate is not null && !predicate(row))
            {
                continue;
            }

            var value = NormalizeCell(row[valueColumn]);

            if (!string.IsNullOrWhiteSpace(value))
            {
                yield return value;
            }
        }
    }

    private static IEnumerable<IReadOnlyList<string>> ReadTableRows(string content)
    {
        foreach (var line in content.Replace("\r\n", "\n").Split('\n'))
        {
            var trimmed = line.Trim();

            if (!trimmed.StartsWith('|') ||
                trimmed.Contains("---", StringComparison.Ordinal) ||
                trimmed.Contains("Date |", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains("Condition |", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains("Medication |", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains("Gap ID |", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains("Conflict ID |", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            yield return trimmed.Trim('|')
                .Split('|')
                .Select(cell => cell.Trim())
                .ToList();
        }
    }

    private static string ReadSection(string content, string section)
    {
        var lines = content.Replace("\r\n", "\n").Split('\n');
        var start = Array.FindIndex(lines, line => line.Trim().Equals(section, StringComparison.OrdinalIgnoreCase));

        if (start < 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();

        for (var index = start + 1; index < lines.Length; index++)
        {
            if (lines[index].StartsWith("## ", StringComparison.Ordinal))
            {
                break;
            }

            builder.AppendLine(lines[index]);
        }

        return builder.ToString();
    }

    private static bool IsStatus(IReadOnlyList<string> row, int column, string expectedStatus)
    {
        return row.Count > column &&
               row[column].Contains(expectedStatus, StringComparison.OrdinalIgnoreCase);
    }

    private static void AppendSection(
        StringBuilder builder,
        string title,
        IEnumerable<string> values,
        string emptyText)
    {
        var items = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(12)
            .ToList();

        builder.AppendLine($"## {title}");
        builder.AppendLine();

        if (items.Count == 0)
        {
            builder.AppendLine($"- {emptyText}");
        }
        else
        {
            foreach (var item in items)
            {
                builder.AppendLine($"- {item}");
            }
        }

        builder.AppendLine();
    }

    private static string NormalizeCell(string value)
    {
        return Regex.Replace(value.Replace("<!--", string.Empty).Replace("-->", string.Empty), @"\s+", " ").Trim();
    }

    private static string FirstNameOnly(string displayName)
    {
        var cleaned = Regex.Replace(displayName.Trim(), @"\s+", " ");
        var first = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return string.IsNullOrWhiteSpace(first) ? displayName : first;
    }

    private static string EscapeYaml(string value)
    {
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private static string EscapeMarkdown(string value)
    {
        return value.Replace("|", "/").Trim();
    }

    private static string NormalizeContent(string content)
    {
        return content.Replace("\r\n", "\n").TrimEnd() + Environment.NewLine;
    }
}
