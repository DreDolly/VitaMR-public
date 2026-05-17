using System.IO;
using VitaMR.Models;

namespace VitaMR.Services;

public sealed class MarkdownSessionOpenAwarenessService : ISessionOpenAwarenessService
{
    public SessionOpenAwarenessResult BuildSessionOpenAwareness(string vaultRoot, ChartContext activeChart)
    {
        SchedulerFileService.EnsureSchedulerFiles(vaultRoot);
        var result = new SessionOpenAwarenessResult
        {
            WasRun = true,
            Status = "SESSION_OPEN_READY",
            ActiveChartId = activeChart.ChartId,
            ActivePatientDisplayName = activeChart.LocalDisplayName
        };

        var wikiFolder = Path.Combine(vaultRoot, activeChart.ChartFolderName, "wiki");
        result.Lines.Add($"Session awareness loaded for {FirstName(activeChart.LocalDisplayName)}.");
        AddSchedulerLine(vaultRoot, result);
        AddUserReminderLine(vaultRoot, result);
        AddHighestPriorityCareGap(wikiFolder, result);
        AddActiveConflict(wikiFolder, result);
        AddSymptomPattern(wikiFolder, result);
        AddGoalLine(wikiFolder, result);
        AddDreamRunnerLine(wikiFolder, result);

        if (result.Lines.Count == 1)
        {
            result.Lines.Add("No open chart-maintenance alerts were found for the active chart.");
        }

        result.Lines.Add("Prototype note: these are report-only reminders. I will not take clinical action unless you ask.");
        return result;
    }

    private static void AddSchedulerLine(string vaultRoot, SessionOpenAwarenessResult result)
    {
        var queuePath = Path.Combine(vaultRoot, "Task_Queue.md");
        var rows = ReadTableRows(queuePath)
            .Where(row => row.Count >= 10)
            .Where(row => row[6].Equals("pending", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (rows.Count > 0)
        {
            result.Lines.Add($"Scheduler: {rows.Count} pending background task(s). Say `show queue` if you want to see them.");
        }
    }

    private static void AddUserReminderLine(string vaultRoot, SessionOpenAwarenessResult result)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var rows = ReadTableRows(Path.Combine(vaultRoot, "User_Reminders.md"))
            .Where(row => row.Count >= 5 && row[2].Equals("open", StringComparison.OrdinalIgnoreCase))
            .Select(row => new
            {
                Row = row,
                DueDate = DateOnly.TryParse(row[1], out var dueDate) ? dueDate : DateOnly.MaxValue
            })
            .Where(item => item.DueDate <= today)
            .OrderBy(item => item.DueDate)
            .ToList();

        if (rows.Count > 0)
        {
            result.Lines.Add($"Reminder due: {rows[0].Row[3]}.");
        }
    }

    private static void AddHighestPriorityCareGap(string wikiFolder, SessionOpenAwarenessResult result)
    {
        var rows = ReadTableRows(Path.Combine(wikiFolder, "Care_Gaps.md"))
            .Where(row => row.Count >= 4 && row[3].Equals("open", StringComparison.OrdinalIgnoreCase))
            .OrderBy(row => PriorityRank(row[2]))
            .ThenBy(row => row.Count > 4 ? row[4] : string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (rows.Count > 0)
        {
            var row = rows[0];
            result.Lines.Add($"Top open care gap: {row[1]} ({row[2]} priority).");
        }
    }

    private static void AddActiveConflict(string wikiFolder, SessionOpenAwarenessResult result)
    {
        var rows = ReadTableRows(Path.Combine(wikiFolder, "Conflicts.md"))
            .Where(row => row.Count >= 5 && row[3].Contains("active", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (rows.Count > 0)
        {
            result.Lines.Add($"Active conflict: {StripMarkdown(rows[0][4])}");
        }
    }

    private static void AddSymptomPattern(string wikiFolder, SessionOpenAwarenessResult result)
    {
        var path = Path.Combine(wikiFolder, "Symptom_Patterns.md");

        if (!File.Exists(path))
        {
            return;
        }

        var heading = File.ReadLines(path)
            .FirstOrDefault(line => line.StartsWith("## Pattern:", StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(heading))
        {
            result.Lines.Add($"Symptom pattern on file: {heading.Replace("## Pattern:", string.Empty, StringComparison.OrdinalIgnoreCase).Trim()}.");
        }
    }

    private static void AddGoalLine(string wikiFolder, SessionOpenAwarenessResult result)
    {
        var rows = ReadTableRows(Path.Combine(wikiFolder, "Health_Goals.md"))
            .Where(row => row.Count >= 3 && row[2].Equals("active", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (rows.Count > 0)
        {
            result.Lines.Add($"Active health goal: {rows[0][1]}.");
        }
    }

    private static void AddDreamRunnerLine(string wikiFolder, SessionOpenAwarenessResult result)
    {
        var path = Path.Combine(wikiFolder, "Dream_Runner_Log.md");

        if (!File.Exists(path))
        {
            result.Lines.Add("Dream Runner: no audit log for this chart yet. Type run Dream Runner when you want me to check it.");
            return;
        }

        var lastStatus = File.ReadLines(path)
            .Reverse()
            .FirstOrDefault(line => line.TrimStart().StartsWith("- Status:", StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(lastStatus))
        {
            result.Lines.Add($"Dream Runner {lastStatus.Trim().TrimStart('-').Trim()}.");
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
            .Where(row => row.Count > 0 && !row[0].Equals("Task ID", StringComparison.OrdinalIgnoreCase) && !row[0].Contains("ID", StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private static IReadOnlyList<string> ParseRow(string line)
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

    private static int PriorityRank(string priority)
    {
        return priority.ToLowerInvariant() switch
        {
            "urgent" => 0,
            "high" => 1,
            "moderate" => 2,
            "routine" => 3,
            _ => 4
        };
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
}
