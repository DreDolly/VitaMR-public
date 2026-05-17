using System.IO;

namespace VitaMR.Services;

public static class SchedulerFileService
{
    private static readonly string[] DefaultTaskRows =
    [
        "| TASK-004 | Symptom Pattern Scan | SYMPTOM_WATCHER.md | manual, scheduled | weekly | 4 | pending | none | none | Pattern Linker v0 report-only scan. |",
        "| TASK-005 | Goal Progress Sync | GOAL_ENGINE.md | manual, scheduled | monthly | 6 | pending | none | none | Health Goal Engine v0 scaffold/report-only sync. |",
        "| TASK-008 | Drug Interaction Scan | DRUG_INTERACTION_SCANNER.md | manual, event | on-demand | 5 | pending | none | none | Prototype known-rule medication scan only. |",
        "| TASK-009 | Pre-Visit Brief | PRE_VISIT_BRIEF.md | manual | on-demand | 7 | pending | none | none | Compile sterile chart summary for visit prep. |"
    ];

    public static void EnsureSchedulerFiles(string vaultRoot)
    {
        Directory.CreateDirectory(vaultRoot);
        var queuePath = Path.Combine(vaultRoot, "Task_Queue.md");
        var logPath = Path.Combine(vaultRoot, "Schedule_Log.md");
        var remindersPath = Path.Combine(vaultRoot, "User_Reminders.md");
        EnsureQueue(queuePath);
        EnsureLog(logPath);
        EnsureReminders(remindersPath);
    }

    public static void AppendScheduleLog(string vaultRoot, string taskId, string taskName, string trigger, string result, string notes)
    {
        EnsureSchedulerFiles(vaultRoot);
        var logPath = Path.Combine(vaultRoot, "Schedule_Log.md");
        File.AppendAllText(logPath, $"| {DateTime.Now:yyyy-MM-dd HH:mm} | {taskId} | {taskName} | {trigger} | {result} | {Escape(notes)} |\n");
    }

    private static void EnsureQueue(string queuePath)
    {
        var header = "# Task Queue\n\n| Task ID | Task Name | Instruction File | Trigger Type | Schedule Interval | Priority | Status | Last Run | Next Run | Notes |\n|---|---|---|---|---|---|---|---|---|---|\n";
        var content = File.Exists(queuePath) ? File.ReadAllText(queuePath) : header;

        if (!content.Contains("| Task ID | Task Name |", StringComparison.OrdinalIgnoreCase))
        {
            content = header;
        }

        foreach (var row in DefaultTaskRows)
        {
            var taskId = row.Split('|', StringSplitOptions.TrimEntries)[1];

            if (!content.Contains($"| {taskId} |", StringComparison.OrdinalIgnoreCase))
            {
                content = content.TrimEnd() + Environment.NewLine + row + Environment.NewLine;
            }
        }

        File.WriteAllText(queuePath, Normalize(content));
    }

    private static void EnsureLog(string logPath)
    {
        if (!File.Exists(logPath))
        {
            File.WriteAllText(logPath, "# Schedule Log\n\n| Date | Task ID | Task Name | Trigger | Result | Notes |\n|---|---|---|---|---|---|\n");
        }
        else if (!File.ReadAllText(logPath).Contains("| Date | Task ID |", StringComparison.OrdinalIgnoreCase))
        {
            File.AppendAllText(logPath, "\n| Date | Task ID | Task Name | Trigger | Result | Notes |\n|---|---|---|---|---|---|\n");
        }
    }

    private static void EnsureReminders(string remindersPath)
    {
        if (!File.Exists(remindersPath))
        {
            File.WriteAllText(
                remindersPath,
                "# User Reminders\n\n| Reminder ID | Due Date | Status | Reminder | Related Chart | Created | Notes |\n|---|---|---|---|---|---|---|\n");
        }
        else if (!File.ReadAllText(remindersPath).Contains("| Reminder ID | Due Date |", StringComparison.OrdinalIgnoreCase))
        {
            File.AppendAllText(remindersPath, "\n| Reminder ID | Due Date | Status | Reminder | Related Chart | Created | Notes |\n|---|---|---|---|---|---|---|\n");
        }
    }

    private static string Escape(string value)
    {
        return value.Replace("|", "/").Trim();
    }

    private static string Normalize(string content)
    {
        return content.Replace("\r\n", "\n").TrimEnd() + Environment.NewLine;
    }
}
