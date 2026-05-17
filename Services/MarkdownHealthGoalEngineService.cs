using System.IO;
using System.Text.RegularExpressions;
using VitaMR.Models;

namespace VitaMR.Services;

public sealed class MarkdownHealthGoalEngineService : IHealthGoalEngineService
{
    public HealthGoalResult CaptureIfGoalMention(string vaultRoot, ChartContext chartContext, string userText, string sessionId)
    {
        var result = new HealthGoalResult { WasRun = true, Status = "HEALTH_GOAL_SCANNED" };

        if (!LooksLikeGoal(userText))
        {
            return result;
        }

        var chartRoot = Path.Combine(vaultRoot, chartContext.ChartFolderName);
        var rawFolder = Path.Combine(chartRoot, "raw");
        var wikiFolder = Path.Combine(chartRoot, "wiki");
        Directory.CreateDirectory(rawFolder);
        Directory.CreateDirectory(wikiFolder);
        EnsureHealthGoalFile(wikiFolder, chartContext);

        var title = BuildGoalTitle(userText);
        var goalsPath = Path.Combine(wikiFolder, "Health_Goals.md");
        var content = File.ReadAllText(goalsPath);

        if (content.Contains(title, StringComparison.OrdinalIgnoreCase))
        {
            result.Status = "HEALTH_GOAL_DUPLICATE_SKIPPED";
            result.GoalsPath = goalsPath;
            result.GoalTitle = title;
            return result;
        }

        var rawPath = Path.Combine(rawFolder, $"{DateTime.Now:yyyy-MM-dd_HHmmss}_Goal_Statement.md");
        File.WriteAllText(
            rawPath,
            Normalize(
                $"---\ndocument_type: \"Goal_Statement\"\nchart_id: \"{chartContext.ChartId}\"\nrisk_level: low\ndolly_directive: \"WIKI_NATIVE\"\nsource_type: \"Conversation\"\nconversation_session_id: \"{sessionId}\"\nstatus: unverified\n---\n\n# Goal Statement\n\n> {userText.Trim()}\n\nPrototype note: Goal Engine v0 captures the stated goal only. The full 5-question goal interview is a later build.\n"));

        var row = $"| {NextGoalId(content)} | {EscapeTable(title)} | active | {DateTime.Now:yyyy-MM-dd} | not set | not set | monthly | none | [[raw/{Path.GetFileNameWithoutExtension(rawPath)}]] |";
        File.AppendAllText(goalsPath, row + Environment.NewLine);

        result.WasCaptured = true;
        result.Status = "HEALTH_GOAL_CAPTURED";
        result.RawEntryPath = rawPath;
        result.GoalsPath = goalsPath;
        result.GoalTitle = title;
        return result;
    }

    public static void EnsureHealthGoalFile(string wikiFolder, ChartContext chartContext)
    {
        Directory.CreateDirectory(wikiFolder);
        var path = Path.Combine(wikiFolder, "Health_Goals.md");

        if (!File.Exists(path))
        {
            File.WriteAllText(
                path,
                Normalize(
                    $"---\nchart_id: \"{chartContext.ChartId}\"\ndocument_type: \"Health_Goals\"\nrisk_level: low\ndolly_directive: \"WIKI_NATIVE\"\nstatus: scaffold\n---\n\n# Health Goals\n\n> **Summary:** Patient-owned goals captured from conversation. Prototype mode records goals but does not run coaching or clinical automation.\n\n| Goal ID | Goal | Status | Created | Target Date | Linked Data Field | Check-In Frequency | Last Check-In | Source |\n|---|---|---|---|---|---|---|---|---|\n"));
        }
    }

    private static bool LooksLikeGoal(string value)
    {
        return Regex.IsMatch(
            value,
            @"\b(i want to|i'm trying to|i am trying to|i'm working on|i am working on|my goal is|i'd like to|i would like to|trying to lose weight|want to be more active|cutting back on)\b",
            RegexOptions.IgnoreCase);
    }

    private static string BuildGoalTitle(string value)
    {
        var cleaned = Regex.Replace(value.Trim(), @"\s+", " ");
        cleaned = Regex.Replace(cleaned, @"^(for\s+[A-Z][A-Za-z.'-]+(?:\s+[A-Z][A-Za-z.'-]+){0,4},?\s*)", string.Empty);
        return cleaned.Length <= 90 ? cleaned : cleaned[..90].Trim() + "...";
    }

    private static string NextGoalId(string content)
    {
        var matches = Regex.Matches(content, @"\bGOAL-(?<number>\d+)\b");
        var next = matches.Select(match => int.TryParse(match.Groups["number"].Value, out var number) ? number : 0).DefaultIfEmpty(0).Max() + 1;
        return $"GOAL-{next:000}";
    }

    private static string EscapeTable(string value)
    {
        return Regex.Replace(value.Replace("|", "/").Trim(), @"\s+", " ");
    }

    private static string Normalize(string content)
    {
        return content.Replace("\r\n", "\n").TrimEnd() + Environment.NewLine;
    }
}
