using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using VitaMR.Models;

namespace VitaMR.Services;

public sealed class MarkdownPatternLinkerService : IPatternLinkerService
{
    public PatternLinkerResult ScanAllCharts(string vaultRoot, IReadOnlyList<PatientIdentityRecord> activePatients)
    {
        var result = new PatternLinkerResult
        {
            WasRun = true,
            Status = "PATTERN_LINKER_STARTED"
        };

        EnsureSchedulerFiles(vaultRoot);

        foreach (var patient in activePatients.Where(patient => !string.IsNullOrWhiteSpace(patient.ChartId)))
        {
            var chartContext = new ChartContext(patient.ChartId, patient.PatientDisplayName);
            var chartRoot = Path.Combine(vaultRoot, chartContext.ChartFolderName);

            if (!Directory.Exists(chartRoot))
            {
                continue;
            }

            result.ChartsScanned++;
            ScanChart(chartRoot, chartContext, result);
        }

        result.Status = result.PatternsWritten > 0
            ? "PATTERN_LINKER_UPDATED"
            : "PATTERN_LINKER_NO_NEW_PATTERNS";
        result.ScheduleLogPath = AppendScheduleLog(vaultRoot, result);
        UpdateTaskQueue(vaultRoot, result.Status);
        return result;
    }

    public static void EnsureSchedulerFiles(string vaultRoot)
    {
        SchedulerFileService.EnsureSchedulerFiles(vaultRoot);
    }

    private static void ScanChart(string chartRoot, ChartContext chartContext, PatternLinkerResult result)
    {
        var wikiFolder = Path.Combine(chartRoot, "wiki");
        var journalPath = Path.Combine(wikiFolder, "Symptom_Journal.md");
        var patternsPath = Path.Combine(wikiFolder, "Symptom_Patterns.md");
        var careGapsPath = Path.Combine(wikiFolder, "Care_Gaps.md");

        if (!File.Exists(journalPath))
        {
            MarkdownSymptomWatcherService.EnsureSymptomFiles(wikiFolder, chartContext);
            return;
        }

        MarkdownSymptomWatcherService.EnsureSymptomFiles(wikiFolder, chartContext);
        EnsureCareGaps(careGapsPath);
        var rows = ReadSymptomRows(journalPath).ToList();
        result.SymptomRowsScanned += rows.Count;

        foreach (var pattern in DetectPatterns(rows))
        {
            if (AppendPattern(patternsPath, pattern))
            {
                result.PatternsWritten++;
                result.Messages.Add($"{chartContext.ChartId}: {pattern.Label} ({pattern.Classification}).");

                if (pattern.Classification is "clinically_significant" or "urgent")
                {
                    if (AppendCareGap(careGapsPath, pattern))
                    {
                        result.CareGapsWritten++;
                    }
                }
            }
            else
            {
                result.DuplicatePatternsSkipped++;
            }
        }
    }

    private static IEnumerable<SymptomPattern> DetectPatterns(IReadOnlyList<SymptomJournalRow> rows)
    {
        foreach (var group in rows.GroupBy(row => row.Symptom, StringComparer.OrdinalIgnoreCase))
        {
            var ordered = group.OrderBy(row => row.Date).ToList();

            for (var index = 0; index < ordered.Count; index++)
            {
                var window = ordered
                    .Where(row => row.Date >= ordered[index].Date.AddDays(-30) && row.Date <= ordered[index].Date.AddDays(30))
                    .ToList();

                if (window.Count >= 2)
                {
                    yield return BuildPattern(
                        $"{group.Key} recurrence",
                        "needs_monitoring",
                        "Recurrence Window",
                        window,
                        $"{group.Key} appeared {window.Count} times within a 30-day window.");
                    break;
                }
            }

            if (HasWorseningSeverity(ordered))
            {
                yield return BuildPattern(
                    $"{group.Key} worsening severity",
                    "clinically_significant",
                    "Severity Escalation",
                    ordered,
                    $"{group.Key} appears with increasing reported severity over time.");
            }

            if (HasActivityThresholdRegression(ordered))
            {
                yield return BuildPattern(
                    $"{group.Key} lower activity threshold",
                    "clinically_significant",
                    "Activity Threshold Regression",
                    ordered,
                    $"{group.Key} appears with a lower activity threshold across entries.");
            }
        }

        foreach (var group in rows.GroupBy(row => row.BodySystem, StringComparer.OrdinalIgnoreCase))
        {
            var distinctSymptoms = group
                .Select(row => row.Symptom)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (distinctSymptoms.Count < 2)
            {
                continue;
            }

            var ordered = group.OrderBy(row => row.Date).ToList();
            var first = ordered.First().Date;
            var last = ordered.Last().Date;

            if ((last - first).TotalDays <= 30)
            {
                yield return BuildPattern(
                    $"{group.Key} symptom cluster",
                    "clinically_significant",
                    "Body System Clustering",
                    ordered,
                    $"{distinctSymptoms.Count} different {group.Key} symptoms appeared within 30 days.");
            }
        }
    }

    private static SymptomPattern BuildPattern(
        string label,
        string classification,
        string patternType,
        IReadOnlyList<SymptomJournalRow> rows,
        string summary)
    {
        return new SymptomPattern(
            label,
            classification,
            patternType,
            summary,
            rows.Select(row => row.Source).Distinct(StringComparer.OrdinalIgnoreCase).ToList());
    }

    private static bool AppendPattern(string patternsPath, SymptomPattern pattern)
    {
        var content = File.ReadAllText(patternsPath);
        var patternId = BuildSafeId(pattern.Label);

        if (content.Contains($"pattern_id: {patternId}", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var builder = new StringBuilder();
        builder.AppendLine();
        builder.AppendLine($"## Pattern: {pattern.Label} - Detected {DateTime.Now:yyyy-MM-dd}");
        builder.AppendLine();
        builder.AppendLine($"pattern_id: {patternId}");
        builder.AppendLine($"classification: {pattern.Classification}");
        builder.AppendLine($"pattern_type: {pattern.PatternType}");
        builder.AppendLine("status: active");
        builder.AppendLine($"summary: {pattern.Summary}");
        builder.AppendLine($"entries_involved: {string.Join(", ", pattern.Sources)}");
        builder.AppendLine();
        builder.AppendLine("Prototype safety note: This is a report-only longitudinal signal. It is not a diagnosis, triage decision, or treatment recommendation.");

        File.AppendAllText(patternsPath, NormalizeContent(builder.ToString()));
        return true;
    }

    private static bool AppendCareGap(string careGapsPath, SymptomPattern pattern)
    {
        var content = File.ReadAllText(careGapsPath);
        var title = $"Symptom pattern review: {pattern.Label}";

        if (content.Contains(title, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var priority = pattern.Classification.Equals("urgent", StringComparison.OrdinalIgnoreCase)
            ? "urgent"
            : "high";
        var row = $"| {NextSequentialId(content, "GAP")} | {EscapeTable(title)} | {priority} | open | {DateTime.Now:yyyy-MM-dd} | none | symptom_watcher | {EscapeTable(pattern.Summary)} | none | 0 |";
        File.AppendAllText(careGapsPath, row + Environment.NewLine);
        return true;
    }

    private static IReadOnlyList<SymptomJournalRow> ReadSymptomRows(string journalPath)
    {
        return File.ReadAllLines(journalPath)
            .Select(ParseTableRow)
            .Where(row => row.Count >= 7 && !row[0].Equals("Date", StringComparison.OrdinalIgnoreCase))
            .Select(row => new SymptomJournalRow(
                ParseDate(row[0]),
                row[1],
                row[2],
                row[3],
                row[4],
                row[5],
                row[6]))
            .Where(row => row.Date != DateTime.MinValue)
            .ToList();
    }

    private static IReadOnlyList<string> ParseTableRow(string line)
    {
        var trimmed = line.Trim();

        if (!trimmed.StartsWith('|') || trimmed.Contains("---", StringComparison.Ordinal))
        {
            return [];
        }

        return trimmed.Trim('|')
            .Split('|')
            .Select(cell => cell.Trim())
            .ToList();
    }

    private static bool HasWorseningSeverity(IReadOnlyList<SymptomJournalRow> rows)
    {
        var ranked = rows
            .Select(row => SeverityRank(row.Severity))
            .Where(rank => rank > 0)
            .ToList();

        return ranked.Count >= 2 && ranked.Last() > ranked.First();
    }

    private static bool HasActivityThresholdRegression(IReadOnlyList<SymptomJournalRow> rows)
    {
        var ranked = rows
            .Select(row => ActivityRank(row.ActivityContext))
            .Where(rank => rank > 0)
            .ToList();

        return ranked.Count >= 2 && ranked.Last() < ranked.First();
    }

    private static int SeverityRank(string severity)
    {
        if (severity.Contains("mild", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        if (severity.Contains("moderate", StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        if (severity.Contains("severe", StringComparison.OrdinalIgnoreCase) ||
            severity.Contains("worse", StringComparison.OrdinalIgnoreCase) ||
            severity.Contains("worsening", StringComparison.OrdinalIgnoreCase))
        {
            return 3;
        }

        var score = Regex.Match(severity, @"\b(?<score>[0-9]|10)/10\b");
        return score.Success ? int.Parse(score.Groups["score"].Value, CultureInfo.InvariantCulture) : 0;
    }

    private static int ActivityRank(string context)
    {
        if (context.Contains("run", StringComparison.OrdinalIgnoreCase) ||
            context.Contains("exercise", StringComparison.OrdinalIgnoreCase) ||
            context.Contains("sport", StringComparison.OrdinalIgnoreCase))
        {
            return 4;
        }

        if (context.Contains("stairs", StringComparison.OrdinalIgnoreCase) ||
            context.Contains("walk", StringComparison.OrdinalIgnoreCase))
        {
            return 3;
        }

        if (context.Contains("around", StringComparison.OrdinalIgnoreCase) ||
            context.Contains("house", StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        if (context.Contains("rest", StringComparison.OrdinalIgnoreCase) ||
            context.Contains("sitting", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        return 0;
    }

    private static DateTime ParseDate(string value)
    {
        return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed)
            ? parsed.Date
            : DateTime.MinValue;
    }

    private static void EnsureCareGaps(string path)
    {
        if (!File.Exists(path))
        {
            File.WriteAllText(path, "# Care Gaps\n\n| Gap ID | Title | Priority | Status | Date Added | Due Date | Source | Notes | Surfaced At | Surface Count |\n|---|---|---|---|---|---|---|---|---|---|\n");
        }
    }

    private static string AppendScheduleLog(string vaultRoot, PatternLinkerResult result)
    {
        SchedulerFileService.AppendScheduleLog(
            vaultRoot,
            "TASK-004",
            "Symptom Pattern Scan",
            "manual",
            result.Status,
            $"charts={result.ChartsScanned}; rows={result.SymptomRowsScanned}; patterns={result.PatternsWritten}; care_gaps={result.CareGapsWritten}");
        return Path.Combine(vaultRoot, "Schedule_Log.md");
    }

    private static void UpdateTaskQueue(string vaultRoot, string status)
    {
        var path = Path.Combine(vaultRoot, "Task_Queue.md");
        var today = DateTime.Now.Date;
        File.WriteAllText(
            path,
            "# Task Queue\n\n| Task ID | Task Name | Instruction File | Trigger Type | Schedule Interval | Priority | Status | Last Run | Next Run | Notes |\n|---|---|---|---|---|---|---|---|---|---|\n" +
            $"| TASK-004 | Symptom Pattern Scan | SYMPTOM_WATCHER.md | manual, scheduled | weekly | 4 | pending | {today:yyyy-MM-dd} | {today.AddDays(7):yyyy-MM-dd} | Last result: {status}. |\n");
    }

    private static string NextSequentialId(string content, string prefix)
    {
        var matches = Regex.Matches(content, $@"\b{Regex.Escape(prefix)}-(?<number>\d+)\b");
        var next = matches
            .Select(match => int.TryParse(match.Groups["number"].Value, out var number) ? number : 0)
            .DefaultIfEmpty(0)
            .Max() + 1;
        return $"{prefix}-{next:000}";
    }

    private static string BuildSafeId(string value)
    {
        var safe = Regex.Replace(value.ToLowerInvariant(), @"[^a-z0-9]+", "_").Trim('_');
        return string.IsNullOrWhiteSpace(safe) ? "pattern" : safe;
    }

    private static string EscapeTable(string value)
    {
        return Regex.Replace(value.Replace("|", "/").Trim(), @"\s+", " ");
    }

    private static string NormalizeContent(string content)
    {
        return content.Replace("\r\n", "\n").TrimEnd() + Environment.NewLine;
    }

    private sealed record SymptomJournalRow(
        DateTime Date,
        string Symptom,
        string BodySystem,
        string Severity,
        string ActivityContext,
        string Duration,
        string Source);

    private sealed record SymptomPattern(
        string Label,
        string Classification,
        string PatternType,
        string Summary,
        IReadOnlyList<string> Sources);
}
