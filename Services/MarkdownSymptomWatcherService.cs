using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using VitaMR.Models;

namespace VitaMR.Services;

public sealed class MarkdownSymptomWatcherService : ISymptomWatcherService
{
    private static readonly SymptomRule[] SymptomRules =
    [
        new("epigastric_pain", "GI", @"epigastric|upper abdominal|stomach pain|abdominal pain|belly pain"),
        new("weight_loss", "General", @"weight loss|losing weight|lost weight|unintentional weight"),
        new("chest_pain_or_heaviness", "Cardiovascular", @"chest pain|chest pressure|chest heaviness|chest tightness"),
        new("shortness_of_breath", "Respiratory", @"shortness of breath|sob\b|dyspnea|trouble breathing|hard to breathe"),
        new("dizziness_or_lightheadedness", "Neurological", @"dizzy|dizziness|lightheaded|light-headed|vertigo"),
        new("syncope_or_near_syncope", "Cardiovascular", @"near syncope|near-syncope|syncope|fainted|passed out|almost passed out"),
        new("fatigue", "General", @"fatigue|tired|exhausted|low energy"),
        new("swelling", "Cardiovascular", @"swelling|edema|ankle swelling|leg swelling"),
        new("palpitations", "Cardiovascular", @"palpitation|heart racing|racing heart|irregular heartbeat"),
        new("headache", "Neurological", @"headache|migraine"),
        new("nausea_or_vomiting", "GI", @"nausea|nauseous|vomiting|throwing up"),
        new("pain", "General", @"\bpain\b|hurts|aching|ache")
    ];

    public SymptomWatcherResult CaptureIfSymptomMention(
        string vaultRoot,
        ChartContext chartContext,
        string userText,
        string sessionId)
    {
        var result = new SymptomWatcherResult
        {
            WasRun = true,
            ChartId = chartContext.ChartId,
            Status = "SYMPTOM_WATCHER_SCANNED"
        };

        if (string.IsNullOrWhiteSpace(userText) || LooksLikeQuestionOnly(userText))
        {
            result.Messages.Add("No patient-reported symptom statement detected.");
            return result;
        }

        foreach (var mention in ExtractMentions(userText))
        {
            result.Mentions.Add(mention);
        }

        if (result.Mentions.Count == 0)
        {
            result.Messages.Add("No patient-reported symptom statement detected.");
            return result;
        }

        var chartRoot = Path.Combine(vaultRoot, chartContext.ChartFolderName);
        var rawFolder = Path.Combine(chartRoot, "raw");
        var wikiFolder = Path.Combine(chartRoot, "wiki");
        Directory.CreateDirectory(rawFolder);
        Directory.CreateDirectory(wikiFolder);
        EnsureSymptomFiles(wikiFolder, chartContext);

        var rawPath = WriteRawSymptomEntry(rawFolder, chartContext, userText, sessionId, result.Mentions);
        AppendJournalRows(wikiFolder, chartContext, rawPath, result.Mentions);

        result.WasCaptured = true;
        result.Status = "SYMPTOM_WATCHER_CAPTURED";
        result.RawEntryPath = rawPath;
        result.JournalPath = Path.Combine(wikiFolder, "Symptom_Journal.md");
        result.Messages.Add($"Captured {result.Mentions.Count} symptom mention(s) in the patient symptom journal.");
        return result;
    }

    public static void EnsureSymptomFiles(string wikiFolder, ChartContext chartContext)
    {
        Directory.CreateDirectory(wikiFolder);
        EnsureFile(
            Path.Combine(wikiFolder, "Symptom_Journal.md"),
            $"""
            ---
            chart_id: "{chartContext.ChartId}"
            document_type: "Symptom_Journal"
            risk_level: low
            dolly_directive: "WIKI_NATIVE"
            status: scaffold
            ---

            # Symptom Journal

            > **Summary:** Patient-reported symptoms captured from conversation. This journal is informational and does not diagnose or triage.

            | Date | Symptom | Body System | Severity | Activity Context | Duration | Source |
            |---|---|---|---|---|---|---|
            """);

        EnsureFile(
            Path.Combine(wikiFolder, "Symptom_Patterns.md"),
            $"""
            ---
            chart_id: "{chartContext.ChartId}"
            document_type: "Symptom_Patterns"
            risk_level: low
            dolly_directive: "WIKI_NATIVE"
            status: scaffold
            ---

            # Symptom Patterns

            > **Summary:** Pattern Linker findings will appear here after scheduled review. Prototype mode is report-only and does not diagnose.

            | Detected | Pattern | Classification | Status | Source |
            |---|---|---|---|---|
            """);
    }

    private static IEnumerable<SymptomMention> ExtractMentions(string userText)
    {
        var mentions = new List<SymptomMention>();

        foreach (var rule in SymptomRules)
        {
            if (!Regex.IsMatch(userText, rule.Pattern, RegexOptions.IgnoreCase))
            {
                continue;
            }

            mentions.Add(new SymptomMention
            {
                Quote = userText.Trim(),
                ExtractedSymptom = rule.Label,
                BodySystem = rule.BodySystem,
                SeverityReported = ExtractSeverity(userText),
                ActivityContext = ExtractActivityContext(userText),
                Duration = ExtractDuration(userText)
            });
        }

        if (mentions.Any(mention => mention.ExtractedSymptom.EndsWith("_pain", StringComparison.OrdinalIgnoreCase)))
        {
            mentions.RemoveAll(mention => mention.ExtractedSymptom.Equals("pain", StringComparison.OrdinalIgnoreCase));
        }

        foreach (var mention in mentions)
        {
            yield return mention;
        }
    }

    private static string WriteRawSymptomEntry(
        string rawFolder,
        ChartContext chartContext,
        string userText,
        string sessionId,
        IReadOnlyList<SymptomMention> mentions)
    {
        var now = DateTime.Now;
        var fileName = $"{now:yyyy-MM-dd_HHmmss}_Symptom_Journal_Entry.md";
        var path = GetNonConflictingPath(rawFolder, fileName);
        var builder = new StringBuilder();

        builder.AppendLine("---");
        builder.AppendLine("document_type: \"Symptom_Journal_Entry\"");
        builder.AppendLine($"chart_id: \"{chartContext.ChartId}\"");
        builder.AppendLine($"symptom_date: {now:yyyy-MM-dd}");
        builder.AppendLine("risk_level: low");
        builder.AppendLine("dolly_directive: \"WIKI_NATIVE\"");
        builder.AppendLine("source_type: \"Conversation\"");
        builder.AppendLine($"conversation_session_id: \"{EscapeYaml(sessionId)}\"");
        builder.AppendLine("status: unverified");
        builder.AppendLine("---");
        builder.AppendLine();
        builder.AppendLine("# Symptom Journal Entry");
        builder.AppendLine();
        builder.AppendLine("## Verbatim Quote");
        builder.AppendLine();
        builder.AppendLine($"> {userText.Trim()}");
        builder.AppendLine();
        builder.AppendLine("## Extracted Mentions");
        builder.AppendLine();
        builder.AppendLine("| Symptom | Body System | Severity | Activity Context | Duration |");
        builder.AppendLine("|---|---|---|---|---|");

        foreach (var mention in mentions)
        {
            builder.AppendLine($"| {EscapeTable(mention.ExtractedSymptom)} | {EscapeTable(mention.BodySystem)} | {EscapeTable(mention.SeverityReported)} | {EscapeTable(mention.ActivityContext)} | {EscapeTable(mention.Duration)} |");
        }

        builder.AppendLine();
        builder.AppendLine("## Prototype Safety Note");
        builder.AppendLine();
        builder.AppendLine("This is a patient-reported symptom capture for longitudinal tracking. It is not a diagnosis, triage decision, or treatment recommendation.");

        File.WriteAllText(path, NormalizeContent(builder.ToString()));
        return path;
    }

    private static void AppendJournalRows(
        string wikiFolder,
        ChartContext chartContext,
        string rawPath,
        IReadOnlyList<SymptomMention> mentions)
    {
        var journalPath = Path.Combine(wikiFolder, "Symptom_Journal.md");
        EnsureSymptomFiles(wikiFolder, chartContext);
        var rawFileName = Path.GetFileNameWithoutExtension(rawPath);
        var source = $"[[raw/{rawFileName}]]";
        var existing = File.ReadAllText(journalPath);
        var builder = new StringBuilder();

        foreach (var mention in mentions)
        {
            var row = $"| {DateTime.Now:yyyy-MM-dd} | {EscapeTable(mention.ExtractedSymptom)} | {EscapeTable(mention.BodySystem)} | {EscapeTable(mention.SeverityReported)} | {EscapeTable(mention.ActivityContext)} | {EscapeTable(mention.Duration)} | {source} |";

            if (!existing.Contains(row, StringComparison.OrdinalIgnoreCase))
            {
                builder.AppendLine(row);
            }
        }

        if (builder.Length > 0)
        {
            File.AppendAllText(journalPath, NormalizeContent(builder.ToString()));
        }
    }

    private static bool LooksLikeQuestionOnly(string value)
    {
        var trimmed = value.Trim();
        return trimmed.EndsWith("?", StringComparison.Ordinal) &&
               Regex.IsMatch(trimmed, @"^(what|when|where|why|how|can|could|should|does|do|did|is|are)\b", RegexOptions.IgnoreCase);
    }

    private static string ExtractSeverity(string value)
    {
        var numeric = Regex.Match(value, @"\b(?<score>[0-9]|10)\s*/\s*10\b");

        if (numeric.Success)
        {
            return $"{numeric.Groups["score"].Value}/10";
        }

        var descriptive = Regex.Match(value, @"\b(mild|moderate|severe|bad|worse|worsening|better|improved)\b", RegexOptions.IgnoreCase);
        return descriptive.Success ? descriptive.Value.ToLowerInvariant() : "not stated";
    }

    private static string ExtractActivityContext(string value)
    {
        var match = Regex.Match(
            value,
            @"\b(while|when|after|during)\s+(?<context>[^.,;]{3,60})",
            RegexOptions.IgnoreCase);

        return match.Success ? match.Groups["context"].Value.Trim() : "not stated";
    }

    private static string ExtractDuration(string value)
    {
        var match = Regex.Match(
            value,
            @"\b(for|over|past|last)\s+(?<duration>(a|an|\d+|few|several)\s+(day|days|week|weeks|month|months|hour|hours))\b",
            RegexOptions.IgnoreCase);

        return match.Success ? match.Groups["duration"].Value.Trim() : "not stated";
    }

    private static void EnsureFile(string path, string content)
    {
        if (!File.Exists(path))
        {
            File.WriteAllText(path, NormalizeContent(content));
        }
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

    private static string EscapeYaml(string value)
    {
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private static string EscapeTable(string value)
    {
        return Regex.Replace(value.Replace("|", "/").Trim(), @"\s+", " ");
    }

    private static string NormalizeContent(string content)
    {
        return content.Replace("\r\n", "\n").TrimEnd() + Environment.NewLine;
    }

    private sealed record SymptomRule(string Label, string BodySystem, string Pattern);
}
