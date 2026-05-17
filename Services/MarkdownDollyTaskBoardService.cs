using System.IO;
using VitaMR.Models;

namespace VitaMR.Services;

public sealed class MarkdownDollyTaskBoardService : IDollyTaskBoardService
{
    private const string Header =
        "# Dolly Task Board\n\n" +
        "| Task ID | Patient | Task Name | Task Type | Priority | Status | Attempts | Current Step | Route/Model | Last Error | Next Retry | Result Summary | Started | Updated | Elapsed |\n" +
        "|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|\n";

    public DollyTaskBoardResult EnsureBoard(string vaultRoot, ChartContext chartContext)
    {
        var boardPath = GetBoardPath(vaultRoot, chartContext);
        Directory.CreateDirectory(Path.GetDirectoryName(boardPath)!);

        if (!File.Exists(boardPath) || !File.ReadAllText(boardPath).Contains("| Task ID | Patient |", StringComparison.OrdinalIgnoreCase))
        {
            File.WriteAllText(boardPath, Header);
        }

        return BuildStatus(vaultRoot, chartContext);
    }

    public string StartTask(
        string vaultRoot,
        ChartContext chartContext,
        string taskName,
        string currentStep,
        string routeModel,
        string taskType = "user_task",
        int priority = 1)
    {
        EnsureBoard(vaultRoot, chartContext);
        var taskId = $"DTB-{DateTime.Now:yyyyMMdd-HHmmss}";
        var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var row = new DollyTaskRow(
            taskId,
            chartContext.ChartId,
            taskName,
            string.IsNullOrWhiteSpace(taskType) ? "user_task" : taskType,
            priority <= 0 ? 1 : priority,
            "running",
            1,
            currentStep,
            routeModel,
            "none",
            "none",
            "started",
            now,
            now,
            "0s");
        WriteRows(vaultRoot, chartContext, ReadRows(vaultRoot, chartContext).Append(row));
        return taskId;
    }

    public void UpdateTask(
        string vaultRoot,
        ChartContext chartContext,
        string taskId,
        string status,
        int attempts,
        string currentStep,
        string routeModel,
        string lastError,
        string nextRetry,
        string resultSummary,
        string taskType = "",
        int priority = 0)
    {
        EnsureBoard(vaultRoot, chartContext);
        var rows = ReadRows(vaultRoot, chartContext).ToList();
        var index = rows.FindIndex(row => row.TaskId.Equals(taskId, StringComparison.OrdinalIgnoreCase));
        var started = index >= 0 ? rows[index].Started : DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var updated = new DollyTaskRow(
            taskId,
            chartContext.ChartId,
            index >= 0 ? rows[index].TaskName : "Unknown task",
            string.IsNullOrWhiteSpace(taskType) && index >= 0 ? rows[index].TaskType : string.IsNullOrWhiteSpace(taskType) ? "user_task" : taskType,
            priority <= 0 && index >= 0 ? rows[index].Priority : priority <= 0 ? 1 : priority,
            status,
            attempts,
            currentStep,
            routeModel,
            string.IsNullOrWhiteSpace(lastError) ? "none" : lastError,
            string.IsNullOrWhiteSpace(nextRetry) ? "none" : nextRetry,
            string.IsNullOrWhiteSpace(resultSummary) ? "none" : resultSummary,
            started,
            now,
            FormatElapsed(started, now));

        if (index >= 0)
        {
            rows[index] = updated;
        }
        else
        {
            rows.Add(updated);
        }

        WriteRows(vaultRoot, chartContext, rows);
    }

    public DollyTaskBoardResult BuildStatus(string vaultRoot, ChartContext chartContext)
    {
        var boardPath = GetBoardPath(vaultRoot, chartContext);
        var rows = File.Exists(boardPath) ? ReadRows(vaultRoot, chartContext).ToList() : [];
        var activeRows = rows
            .Where(row => IsActiveStatus(row.Status) && !IsStaleActive(row))
            .OrderByDescending(row => row.Updated)
            .ToList();
        var staleRows = rows
            .Where(row => IsActiveStatus(row.Status) && IsStaleActive(row))
            .OrderByDescending(row => row.Updated)
            .ToList();
        var completedCount = rows.Count(row => row.Status.Equals("completed", StringComparison.OrdinalIgnoreCase));
        var result = new DollyTaskBoardResult
        {
            WasWritten = File.Exists(boardPath),
            Status = File.Exists(boardPath) ? "DOLLY_TASK_BOARD_READY" : "DOLLY_TASK_BOARD_MISSING",
            BoardPath = boardPath,
            ActiveTaskCount = activeRows.Count,
            CompletedTaskCount = completedCount
        };

        if (activeRows.Count == 0)
        {
            result.Lines.Add("Nothing is running for this chart right now.");
        }
        else
        {
            foreach (var row in activeRows.Take(5))
            {
                result.Lines.Add(BuildFriendlyActiveLine(row));
            }
        }

        foreach (var row in rows
                     .Where(row => row.Status.Equals("completed", StringComparison.OrdinalIgnoreCase))
                     .OrderByDescending(row => row.Updated)
                     .Take(3))
        {
            result.Lines.Add(BuildFriendlyCompletedLine(row));
        }

        foreach (var row in staleRows.Take(3))
        {
            result.Lines.Add(BuildFriendlyStaleLine(row));
        }

        return result;
    }

    private static string BuildFriendlyActiveLine(DollyTaskRow row)
    {
        var taskName = FriendlyTaskName(row.TaskName);
        var step = FriendlyStep(row.CurrentStep);
        var route = FriendlyRoute(row.RouteModel);
        var retry = string.IsNullOrWhiteSpace(row.NextRetry) || row.NextRetry.Equals("none", StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : $" Next retry: {row.NextRetry}.";

        return $"{taskName} is {FriendlyStatus(row.Status)}: {step}. It has been running {row.Elapsed}.{route}{retry}";
    }

    private static string BuildFriendlyCompletedLine(DollyTaskRow row)
    {
        return $"Recently finished: {FriendlyTaskName(row.TaskName)}. {FriendlyResult(row.ResultSummary)}";
    }

    private static string BuildFriendlyStaleLine(DollyTaskRow row)
    {
        return $"{FriendlyTaskName(row.TaskName)} may need a fresh run. It last stopped at {FriendlyStep(row.CurrentStep)} after {row.Elapsed}.";
    }

    private static string FriendlyTaskName(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "Chart task"
            : value.Trim();
    }

    private static string FriendlyStatus(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "queued" => "waiting its turn",
            "running" => "in progress",
            "retrying" => "trying again safely",
            _ => value.Trim()
        };
    }

    private static string FriendlyStep(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            return "getting ready";
        }

        return value
            .Replace('_', ' ')
            .Replace("backend chart synthesizer", "chart answer writer", StringComparison.OrdinalIgnoreCase)
            .Replace("sterile context", "safe chart context", StringComparison.OrdinalIgnoreCase)
            .Trim();
    }

    private static string FriendlyRoute(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        var route = value.Contains("host", StringComparison.OrdinalIgnoreCase) ||
                    value.Contains("desktop", StringComparison.OrdinalIgnoreCase)
            ? " Local Gemma is handling it."
            : string.Empty;

        return route;
    }

    private static string FriendlyResult(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            return "No extra note was recorded.";
        }

        return value.Trim().EndsWith(".", StringComparison.Ordinal)
            ? value.Trim()
            : $"{value.Trim()}.";
    }

    private static bool IsActiveStatus(string status)
    {
        return status is "queued" or "running" or "retrying";
    }

    private static bool IsStaleActive(DollyTaskRow row)
    {
        return DateTime.TryParse(row.Updated, out var updated) &&
               updated < DateTime.Now.AddMinutes(-30);
    }

    private static string GetBoardPath(string vaultRoot, ChartContext chartContext)
    {
        return Path.Combine(vaultRoot, chartContext.ChartFolderName, "wiki", "Dolly_Task_Board.md");
    }

    private static IReadOnlyList<DollyTaskRow> ReadRows(string vaultRoot, ChartContext chartContext)
    {
        var boardPath = GetBoardPath(vaultRoot, chartContext);

        if (!File.Exists(boardPath))
        {
            return [];
        }

        return File.ReadAllLines(boardPath)
            .Select(ParseRow)
            .Where(row => row is not null)
            .Cast<DollyTaskRow>()
            .ToList();
    }

    private static DollyTaskRow? ParseRow(string line)
    {
        var trimmed = line.Trim();

        if (!trimmed.StartsWith('|') ||
            trimmed.Contains("---", StringComparison.Ordinal) ||
            trimmed.Contains("Task ID", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var cells = trimmed.Trim('|').Split('|').Select(cell => cell.Trim()).ToArray();

        if (cells.Length >= 15 && int.TryParse(cells[4], out var priority) && int.TryParse(cells[6], out var attempts))
        {
            return new DollyTaskRow(
                cells[0],
                cells[1],
                cells[2],
                cells[3],
                priority,
                cells[5],
                attempts,
                cells[7],
                cells[8],
                cells[9],
                cells[10],
                cells[11],
                cells[12],
                cells[13],
                cells[14]);
        }

        if (cells.Length >= 13 && int.TryParse(cells[4], out priority) && int.TryParse(cells[6], out attempts))
        {
            var legacyUpdated = cells[12];

            return new DollyTaskRow(
                cells[0],
                cells[1],
                cells[2],
                cells[3],
                priority,
                cells[5],
                attempts,
                cells[7],
                cells[8],
                cells[9],
                cells[10],
                cells[11],
                legacyUpdated,
                legacyUpdated,
                FormatElapsed(legacyUpdated, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")));
        }

        if (cells.Length < 11 || !int.TryParse(cells[4], out attempts))
        {
            return null;
        }

        var legacyTimestamp = cells[10];

        return new DollyTaskRow(
            cells[0],
            cells[1],
            cells[2],
            "legacy_task",
            5,
            cells[3],
            attempts,
            cells[5],
            cells[6],
            cells[7],
            cells[8],
            cells[9],
            legacyTimestamp,
            legacyTimestamp,
            FormatElapsed(legacyTimestamp, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")));
    }

    private static void WriteRows(string vaultRoot, ChartContext chartContext, IEnumerable<DollyTaskRow> rows)
    {
        var boardPath = GetBoardPath(vaultRoot, chartContext);
        Directory.CreateDirectory(Path.GetDirectoryName(boardPath)!);
        File.WriteAllText(boardPath, Header + string.Concat(rows.Select(FormatRow)));
    }

    private static string FormatRow(DollyTaskRow row)
    {
        return $"| {Escape(row.TaskId)} | {Escape(row.Patient)} | {Escape(row.TaskName)} | {Escape(row.TaskType)} | {row.Priority} | {Escape(row.Status)} | {row.Attempts} | {Escape(row.CurrentStep)} | {Escape(row.RouteModel)} | {Escape(row.LastError)} | {Escape(row.NextRetry)} | {Escape(row.ResultSummary)} | {Escape(row.Started)} | {Escape(row.Updated)} | {Escape(row.Elapsed)} |{Environment.NewLine}";
    }

    private static string Escape(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "none"
            : value.Replace("|", "/", StringComparison.Ordinal).Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal).Trim();
    }

    private static string FormatElapsed(string started, string updated)
    {
        if (!DateTime.TryParse(started, out var startedAt) || !DateTime.TryParse(updated, out var updatedAt))
        {
            return "unknown";
        }

        var elapsed = updatedAt - startedAt;

        if (elapsed < TimeSpan.Zero)
        {
            elapsed = TimeSpan.Zero;
        }

        if (elapsed.TotalHours >= 1)
        {
            return $"{(int)elapsed.TotalHours}h {elapsed.Minutes}m {elapsed.Seconds}s";
        }

        if (elapsed.TotalMinutes >= 1)
        {
            return $"{(int)elapsed.TotalMinutes}m {elapsed.Seconds}s";
        }

        return $"{Math.Max(0, (int)elapsed.TotalSeconds)}s";
    }

    private sealed record DollyTaskRow(
        string TaskId,
        string Patient,
        string TaskName,
        string TaskType,
        int Priority,
        string Status,
        int Attempts,
        string CurrentStep,
        string RouteModel,
        string LastError,
        string NextRetry,
        string ResultSummary,
        string Started,
        string Updated,
        string Elapsed);
}
