using System.Globalization;
using System.IO;
using VitaMR.Models;

namespace VitaMR.Services;

public sealed class MarkdownDollyAgentStateService : IDollyAgentStateService
{
    public void EnsureAgentScaffold(string vaultRoot)
    {
        var folder = GetAgentFolder(vaultRoot);
        Directory.CreateDirectory(folder);

        EnsureFile(Path.Combine(folder, "Agent_Profile.md"), """
        # Dolly Agent Profile

        Dolly is VitaMR's local front-desk agent. She manages turn flow, status awareness, task ownership, and user-facing clarity while C# keeps authority over routing, safety gates, file writes, patient identity, and clinical payload display.
        """);

        EnsureFile(Path.Combine(folder, "Personality_Matrix.md"), """
        # Dolly Personality Matrix

        | Context | Voice | Allowed Behavior |
        |---|---|---|
        | Normal chat | Warm, brief, practical | Answer from sterile local context when safe. |
        | User waiting | Calm status line | Speak only when task state changes or a delay matters. |
        | Backend chart answer | Steady handoff | Give an interstitial, then display backend payload without rewriting clinical content. |
        | Unclear patient/task | One short clarification | Ask for the missing patient or task. |
        | Failure/timeout | Plain explanation | Say what failed, what stayed protected, and what can be retried. |
        """);

        var currentTurnPath = Path.Combine(folder, "Current_Turn.md");
        EnsureFile(currentTurnPath, "# Current Turn\n\nStatus: idle\n");
        MarkStaleCurrentTurnIfNeeded(currentTurnPath);
        var currentTasksPath = Path.Combine(folder, "Current_Tasks.md");
        EnsureFile(currentTasksPath, "# Current Tasks\n\nNo active Dolly tasks recorded yet.\n");
        MarkStaleCurrentTaskIfNeeded(currentTasksPath);
        EnsureFile(Path.Combine(folder, "Task_History.md"), "# Dolly Task History\n\n");
        EnsureFile(Path.Combine(folder, "Memory_Index.md"), """
        # Dolly Memory Index

        ## Runtime Memory Files
        - Current_Turn.md: The active user turn and response ownership.
        - Current_Tasks.md: Compact active task state.
        - Task_History.md: Append-only completed task memory.
        - Heartbeat_Log.md: Dolly task-check heartbeat.
        - ../Chronos_Ledger.md: Sterile temporal event stream for session grounding.
        - Packet_Audit.md: Sterile action-packet routing audit.
        - Packet_Audit_Latest.md: Compact latest packet result for prototype visibility.
        """);
        EnsureFile(Path.Combine(folder, "Backend_Escalation_Rules.md"), """
        # Backend Escalation Rules

        - Chart/wiki medical questions use backend-owned answers.
        - Backend-owned clinical payloads are immutable for Dolly.
        - Local Gemma may provide status/interstitial text only during backend-owned turns.
        - C# validates patient identity, route, packet schema, allowed action, and final display permissions.
        - Local Gemma may be used as fallback only for allowed local-agent tasks or packet repair, not to rewrite locked backend chart answers.
        """);
        EnsureFile(Path.Combine(folder, "Agent_Operating_Rules.md"), """
        # Dolly Agent Operating Rules

        1. Receive the user turn and preserve the user's task as the active priority.
        2. Use Gemma to produce an action packet, then let C# validate it.
        3. Track who owns the final answer: local_agent, backend, or orchestrator.
        4. If backend owns the chart answer, Dolly may acknowledge and report progress but must not rewrite the clinical payload.
        5. Check active task state during long work and speak only when a status change helps the user.
        6. Record current turn, task updates, packet route, and completion in Dolly's system wiki.
        7. Never diagnose, triage, prescribe, order tests, or bypass privacy/scrub/API gates.
        """);
        EnsureFile(Path.Combine(folder, "Heartbeat_Log.md"), "# Dolly Heartbeat Log\n\n");
        EnsureFile(Path.Combine(folder, "Packet_Audit.md"), "# Gemma Action Packet Audit\n\n");
        EnsureFile(Path.Combine(folder, "Packet_Audit_Latest.md"), "# Latest Gemma Packet\n\nNo packet has been recorded yet.\n");
    }

    public string BuildAgentContext(string vaultRoot)
    {
        EnsureAgentScaffold(vaultRoot);
        var folder = GetAgentFolder(vaultRoot);
        var files = new[]
        {
            "Agent_Operating_Rules.md",
            "Personality_Matrix.md",
            "Current_Turn.md",
            "Current_Tasks.md",
            "Packet_Audit_Latest.md",
            "Backend_Escalation_Rules.md"
        };

        return string.Join(
            Environment.NewLine + Environment.NewLine,
            files.Select(file => File.ReadAllText(Path.Combine(folder, file)).Trim()));
    }

    public void StartTurn(
        string vaultRoot,
        ChartContext chartContext,
        string userText,
        GemmaActionPacket packet)
    {
        EnsureAgentScaffold(vaultRoot);
        var folder = GetAgentFolder(vaultRoot);
        var now = FormatNow();

        File.WriteAllText(Path.Combine(folder, "Current_Turn.md"), $"""
        # Current Turn

        Status: running
        Started: {now}
        Last heartbeat: {now}
        Active chart: {chartContext.ChartId}
        Active patient label: {FirstName(chartContext.LocalDisplayName)}
        User intent: {packet.Intent}
        Response owner: {packet.ResponseOwner}
        Display mode: {packet.DisplayMode}
        Backend needed: {packet.BackendNeeded}
        Clinical payload mutable: {packet.ClinicalPayloadMutable}
        Requires confirmation: {packet.RequiresConfirmation}
        Tool: {packet.Tool}

        ## User Request Preview
        {EscapeMarkdownLine(userText)}

        ## Dolly Permission
        {(packet.ResponseOwner.Equals("backend", StringComparison.OrdinalIgnoreCase) ? "Status/interstitial only until backend payload is ready." : "May answer from allowed sterile/local context.")}
        """);
    }

    public void CompleteTurn(
        string vaultRoot,
        ChartContext chartContext,
        string status,
        string resultSummary)
    {
        EnsureAgentScaffold(vaultRoot);
        var folder = GetAgentFolder(vaultRoot);
        var currentTurnPath = Path.Combine(folder, "Current_Turn.md");
        var now = FormatNow();
        var current = File.Exists(currentTurnPath) ? File.ReadAllText(currentTurnPath) : "# Current Turn\n\n";

        current = ReplaceOrAppendLine(current, "Status:", "Status: completed");
        File.WriteAllText(currentTurnPath, current.TrimEnd() + $"""

        ## Completion
        Completed: {now}
        Final status: {EscapeMarkdownLine(status)}
        Result summary: {EscapeMarkdownLine(resultSummary)}
        """);

        File.AppendAllText(Path.Combine(folder, "Task_History.md"), $"""
        ## {now} - Turn Complete
        - Chart: {chartContext.ChartId}
        - Status: {EscapeMarkdownLine(status)}
        - Result: {EscapeMarkdownLine(resultSummary)}

        """);
    }

    public void RecordTaskStarted(
        string vaultRoot,
        ChartContext chartContext,
        string taskId,
        string taskName,
        string currentStep)
    {
        EnsureAgentScaffold(vaultRoot);
        WriteCurrentTask(vaultRoot, chartContext, taskId, "running", taskName, currentStep, "started");
    }

    public void RecordTaskUpdated(
        string vaultRoot,
        ChartContext chartContext,
        string taskId,
        string status,
        string currentStep,
        string resultSummary)
    {
        EnsureAgentScaffold(vaultRoot);
        WriteCurrentTask(vaultRoot, chartContext, taskId, status, string.Empty, currentStep, resultSummary);

        if (status.Equals("completed", StringComparison.OrdinalIgnoreCase) ||
            status.Contains("blocked", StringComparison.OrdinalIgnoreCase))
        {
            File.AppendAllText(Path.Combine(GetAgentFolder(vaultRoot), "Task_History.md"), $"""
            ## {FormatNow()} - {taskId}
            - Chart: {chartContext.ChartId}
            - Status: {EscapeMarkdownLine(status)}
            - Step: {EscapeMarkdownLine(currentStep)}
            - Result: {EscapeMarkdownLine(resultSummary)}

            """);
        }
    }

    public void RecordHeartbeat(
        string vaultRoot,
        ChartContext chartContext,
        DollyTaskBoardResult taskBoardResult)
    {
        EnsureAgentScaffold(vaultRoot);
        var now = FormatNow();
        var folder = GetAgentFolder(vaultRoot);
        var activeLine = taskBoardResult.Lines.FirstOrDefault() ?? "No task status available.";

        File.AppendAllText(Path.Combine(folder, "Heartbeat_Log.md"), $"""
        ## {now}
        - Chart: {chartContext.ChartId}
        - Active tasks: {taskBoardResult.ActiveTaskCount}
        - Completed tasks: {taskBoardResult.CompletedTaskCount}
        - Top line: {EscapeMarkdownLine(activeLine)}

        """);

        var currentTurnPath = Path.Combine(folder, "Current_Turn.md");
        if (File.Exists(currentTurnPath))
        {
            var current = File.ReadAllText(currentTurnPath);
            File.WriteAllText(currentTurnPath, ReplaceOrAppendLine(current, "Last heartbeat:", $"Last heartbeat: {now}"));
        }
    }

    public void AppendPacketAudit(
        string vaultRoot,
        ChartContext chartContext,
        string routeUsed,
        string modelName,
        string scrubbedUserPreview,
        GemmaActionPacketResult result)
    {
        EnsureAgentScaffold(vaultRoot);
        var packet = result.Packet;
        var lines = new List<string>
        {
            $"## {FormatNow()} - Dolly API action packet",
            $"- Chart: {chartContext.ChartId}",
            $"- Route: {EscapeMarkdownLine(routeUsed)}",
            $"- Model: {EscapeMarkdownLine(modelName)}",
            $"- Status: {EscapeMarkdownLine(result.Status)}",
            $"- Valid: {result.WasValid}",
            $"- Attempts: {result.Attempts}",
            $"- Intent: {packet?.Intent ?? "none"}",
            $"- Patient display name: {packet?.PatientDisplayName ?? string.Empty}",
            $"- Confidence: {packet?.Confidence.ToString("0.00", CultureInfo.InvariantCulture) ?? "0.00"}",
            $"- Response owner: {packet?.ResponseOwner ?? string.Empty}",
            $"- Clinical payload mutable: {packet?.ClinicalPayloadMutable.ToString() ?? string.Empty}",
            $"- Display mode: {packet?.DisplayMode ?? string.Empty}",
            $"- Backend needed: {packet?.BackendNeeded.ToString() ?? string.Empty}",
            $"- Tool: {packet?.Tool ?? string.Empty}",
            $"- Scrubbed user preview: {EscapeMarkdownLine(scrubbedUserPreview)}"
        };

        if (result.Errors.Count > 0)
        {
            lines.Add($"- Errors: {EscapeMarkdownLine(string.Join(" | ", result.Errors))}");
        }

        lines.Add(string.Empty);
        File.AppendAllLines(Path.Combine(GetAgentFolder(vaultRoot), "Packet_Audit.md"), lines);
        File.WriteAllText(Path.Combine(GetAgentFolder(vaultRoot), "Packet_Audit_Latest.md"), BuildLatestPacketSummary(chartContext, routeUsed, modelName, scrubbedUserPreview, result));
    }

    private static string BuildLatestPacketSummary(
        ChartContext chartContext,
        string routeUsed,
        string modelName,
        string scrubbedUserPreview,
        GemmaActionPacketResult result)
    {
        var packet = result.Packet;
        var statusLine = result.WasValid
            ? "C# accepted the packet and dispatched the allowlisted action."
            : "C# rejected the packet or stopped before dispatch.";
        var userSafePreview = EscapeMarkdownLine(scrubbedUserPreview);

        return $"""
        # Latest Gemma Packet

        Updated: {FormatNow()}
        Patient label: {FirstName(chartContext.LocalDisplayName)}
        User request preview: {userSafePreview}

        ## Prototype Visibility
        - Status: {EscapeMarkdownLine(result.Status)}
        - Result: {statusLine}
        - Attempts: {result.Attempts}
        - Intent: {packet?.Intent ?? "none"}
        - Confidence: {packet?.Confidence.ToString("0.00", CultureInfo.InvariantCulture) ?? "0.00"}
        - Display: {packet?.DisplayMode ?? "none"}
        - Route: {EscapeMarkdownLine(FriendlyRoute(routeUsed, modelName))}
        - Final owner: {packet?.ResponseOwner ?? "none"}

        ## Safety Notes
        - Chart routing ID remains hidden from normal user replies.
        - Backend-owned chart answers stay locked; Dolly may only show status until the answer is ready.
        - Original-source requests attach saved originals unchanged and do not summarize them.

        {(result.Errors.Count == 0 ? string.Empty : $"## Validation Errors\n- {EscapeMarkdownLine(string.Join("\n- ", result.Errors))}\n")}
        """;
    }

    private static string FriendlyRoute(string routeUsed, string modelName)
    {
        var route = routeUsed.Contains("host", StringComparison.OrdinalIgnoreCase) ||
                    routeUsed.Contains("desktop", StringComparison.OrdinalIgnoreCase)
            ? "Local Gemma"
            : routeUsed;

        return string.IsNullOrWhiteSpace(modelName)
            ? route
            : $"{route} / {modelName}";
    }

    private static void WriteCurrentTask(
        string vaultRoot,
        ChartContext chartContext,
        string taskId,
        string status,
        string taskName,
        string currentStep,
        string resultSummary)
    {
        var folder = GetAgentFolder(vaultRoot);
        File.WriteAllText(Path.Combine(folder, "Current_Tasks.md"), $"""
        # Current Tasks

        Updated: {FormatNow()}
        Chart: {chartContext.ChartId}
        Task ID: {taskId}
        Task name: {EscapeMarkdownLine(taskName)}
        Status: {EscapeMarkdownLine(status)}
        Current step: {EscapeMarkdownLine(currentStep)}
        Result summary: {EscapeMarkdownLine(resultSummary)}
        """);
    }

    private static string GetAgentFolder(string vaultRoot)
    {
        return Path.Combine(vaultRoot, "_System", "Dolly");
    }

    private static void EnsureFile(string path, string content)
    {
        if (File.Exists(path))
        {
            return;
        }

        File.WriteAllText(path, content.Replace("\r\n", "\n").TrimStart() + Environment.NewLine);
    }

    private static void MarkStaleCurrentTaskIfNeeded(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        var content = File.ReadAllText(path);

        if (!content.Contains("Status: running", StringComparison.OrdinalIgnoreCase) &&
            !content.Contains("Status: queued", StringComparison.OrdinalIgnoreCase) &&
            !content.Contains("Status: retrying", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var updatedLine = content
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n')
            .FirstOrDefault(line => line.StartsWith("Updated:", StringComparison.OrdinalIgnoreCase));

        if (updatedLine is null ||
            !DateTime.TryParse(updatedLine["Updated:".Length..].Trim(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var updated) ||
            updated >= DateTime.Now.AddMinutes(-30))
        {
            return;
        }

        var normalized = ReplaceOrAppendLine(content, "Status:", "Status: stale_interrupted");
        normalized = ReplaceOrAppendLine(normalized, "Result summary:", "Result summary: Previous run appears interrupted or stopped before completion.");
        File.WriteAllText(path, normalized.TrimEnd() + Environment.NewLine);
    }

    private static void MarkStaleCurrentTurnIfNeeded(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        var content = File.ReadAllText(path);

        if (!content.Contains("Status: running", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var startedLine = content
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n')
            .FirstOrDefault(line => line.StartsWith("Started:", StringComparison.OrdinalIgnoreCase));

        if (startedLine is not null &&
            DateTime.TryParse(startedLine["Started:".Length..].Trim(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var started) &&
            started >= DateTime.Now.AddMinutes(-30))
        {
            return;
        }

        var normalized = ReplaceOrAppendLine(content, "Status:", "Status: idle");
        normalized = ReplaceOrAppendLine(normalized, "Result summary:", "Result summary: Previous turn was cleared on startup because it was no longer active.");
        File.WriteAllText(path, normalized.TrimEnd() + Environment.NewLine);
    }

    private static string FormatNow()
    {
        return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
    }

    private static string FirstName(string displayName)
    {
        var parts = displayName
            .Trim()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return parts.Length == 0 ? displayName.Trim() : parts[0];
    }

    private static string EscapeMarkdownLine(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "none"
            : value.Replace("\r", " ", StringComparison.Ordinal)
                .Replace("\n", " ", StringComparison.Ordinal)
                .Replace("|", "/", StringComparison.Ordinal)
                .Trim();
    }

    private static string ReplaceOrAppendLine(string content, string prefix, string replacement)
    {
        var lines = content.Replace("\r\n", "\n").Split('\n').ToList();
        var index = lines.FindIndex(line => line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

        if (index >= 0)
        {
            lines[index] = replacement;
            return string.Join(Environment.NewLine, lines);
        }

        return content.TrimEnd() + Environment.NewLine + replacement + Environment.NewLine;
    }
}
