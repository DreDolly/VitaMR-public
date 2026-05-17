using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using VitaMR.Models;

namespace VitaMR.Services;

public sealed class MarkdownEncounterNodeWriterService : IEncounterNodeWriterService
{
    private readonly ITopicPageWriterService _topicPageWriterService;

    public MarkdownEncounterNodeWriterService(ITopicPageWriterService topicPageWriterService)
    {
        _topicPageWriterService = topicPageWriterService;
    }

    public EncounterNodeWriteResult WriteEncounterNode(
        string vaultRoot,
        ChartContext chartContext,
        EncounterExtractionResult extraction)
    {
        var result = new EncounterNodeWriteResult();
        var wikiFolder = Path.Combine(vaultRoot, chartContext.ChartFolderName, "wiki");
        var encountersFolder = Path.Combine(wikiFolder, "encounters");
        Directory.CreateDirectory(encountersFolder);

        var date = NormalizeDate(extraction.DateOfService);
        var documentType = BuildSafeSlug(extraction.DocumentType);
        var fileName = $"{date}_{documentType}_Node.md";
        var encounterPath = GetNonConflictingPath(encountersFolder, fileName);

        File.WriteAllText(encounterPath, BuildEncounterNode(chartContext, extraction, Path.GetFileName(encounterPath)));
        result.WasWritten = true;
        result.EncounterNodePath = encounterPath;
        result.Messages.Add($"Encounter node written: {Path.GetFileName(encounterPath)}");

        result.IndexPath = UpdateIndex(wikiFolder, extraction, Path.GetFileNameWithoutExtension(encounterPath), date);
        result.TimelinePath = UpdateTimeline(wikiFolder, extraction, Path.GetFileNameWithoutExtension(encounterPath), date);
        var vaccinesPath = UpdateVaccines(wikiFolder, extraction, Path.GetFileNameWithoutExtension(encounterPath), date);
        result.CareGapsPath = UpdateCareGaps(wikiFolder, extraction, Path.GetFileNameWithoutExtension(encounterPath), date);
        result.ConflictsPath = UpdateConflicts(wikiFolder, extraction, Path.GetFileNameWithoutExtension(encounterPath), date);
        result.EmergencyCardPath = EmergencyCardService.WriteEmergencyCard(wikiFolder, chartContext);
        result.Messages.Add($"Care gap tracker updated: {Path.GetFileName(result.CareGapsPath)}");
        result.Messages.Add($"Conflict ledger updated: {Path.GetFileName(result.ConflictsPath)}");
        result.Messages.Add($"Emergency card regenerated: {Path.GetFileName(result.EmergencyCardPath)}");
        result.Messages.Add($"Vaccine ledger checked: {Path.GetFileName(vaccinesPath)}");
        result.Messages.Add("Index, Timeline, Care Gaps, Conflicts, and Emergency Card updated.");
        result.TopicPagePaths.AddRange(_topicPageWriterService.WriteTopicPages(
            vaultRoot,
            chartContext,
            extraction,
            encounterPath));
        result.LinkRegistryPath = Path.Combine(vaultRoot, "_Link_Registry.md");

        if (result.TopicPagePaths.Count > 0)
        {
            result.Messages.Add($"Topic pages updated: {result.TopicPagePaths.Count}.");
        }

        return result;
    }

    private static string BuildEncounterNode(
        ChartContext chartContext,
        EncounterExtractionResult extraction,
        string encounterFileName)
    {
        var date = NormalizeDate(extraction.DateOfService);
        var riskLevel = NormalizeRiskLevel(extraction.RiskLevel);
        var dollyDirective = NormalizeDirective(extraction.DollyDirective, riskLevel);
        var rawSource = extraction.Citations.FirstOrDefault() ?? extraction.SourceFileId;
        var builder = new StringBuilder();

        builder.AppendLine("---");
        builder.AppendLine($"chart_id: \"{chartContext.ChartId}\"");
        builder.AppendLine($"document_type: \"{EscapeYaml(extraction.DocumentType)}\"");
        builder.AppendLine($"date_of_service: {date}");
        builder.AppendLine($"risk_level: {riskLevel}");
        builder.AppendLine($"dolly_directive: \"{dollyDirective}\"");
        builder.AppendLine($"raw_source: \"{EscapeYaml(rawSource)}\"");
        builder.AppendLine($"source_system: \"{EscapeYaml(NormalizeUnknown(extraction.SourceSystem))}\"");
        builder.AppendLine($"source_facility: \"{EscapeYaml(NormalizeUnknown(extraction.SourceFacility))}\"");
        builder.AppendLine($"source_type: \"{EscapeYaml(NormalizeUnknown(extraction.SourceType))}\"");
        builder.AppendLine("conflict_status: none");
        builder.AppendLine("encounter_version: 1");
        builder.AppendLine("status: unverified");
        builder.AppendLine($"reading_status: \"{EscapeYaml(NormalizeUnknown(extraction.ReadingStatus))}\"");
        builder.AppendLine("last_auditor: none");
        builder.AppendLine("audit_date: none");
        builder.AppendLine("---");
        builder.AppendLine();
        builder.AppendLine($"# {date} {extraction.DocumentType}");
        builder.AppendLine();
        builder.AppendLine($"> **Clinical Gestalt:** {NormalizeParagraph(extraction.ClinicalGestalt)}");
        builder.AppendLine();
        AppendSection(builder, "Vital Signs", extraction.VitalSigns);
        AppendSection(builder, "Chief Complaint", [extraction.ChiefComplaint]);
        AppendSection(builder, "Active Medications", extraction.ActiveMedications);
        AppendSection(builder, "Diagnoses", extraction.Diagnoses);
        AppendSection(builder, "Labs / Results", extraction.LabsResults);
        AppendSection(builder, "Imaging", extraction.Imaging);
        AppendSection(builder, "Assessment & Plan", extraction.AssessmentPlan);
        AppendSection(builder, "Pending Items", extraction.PendingItems);
        AppendSection(builder, "Safety Flags", extraction.SafetyFlags);
        builder.AppendLine("## Sources");
        AppendList(builder, extraction.Citations.Count == 0 ? [encounterFileName] : extraction.Citations);
        builder.AppendLine();
        builder.AppendLine("## Amendment Log");
        builder.AppendLine();
        builder.AppendLine("| Date | Version | Change | Model |");
        builder.AppendLine("|---|---:|---|---|");
        builder.AppendLine($"| {DateTime.Now:yyyy-MM-dd} | 1 | Node created from sterile extraction result. | Gemini backend |");

        return NormalizeContent(builder.ToString());
    }

    private static string UpdateIndex(
        string wikiFolder,
        EncounterExtractionResult extraction,
        string encounterNodeName,
        string date)
    {
        var path = Path.Combine(wikiFolder, "Index.md");
        EnsureIndex(path);
        var content = File.ReadAllText(path);
        var source = $"[[encounters/{encounterNodeName}]]";
        var summary = NormalizeTableCell(extraction.ClinicalGestalt);
        var marker = $"<!-- encounter:{encounterNodeName} -->";

        if (content.Contains(marker, StringComparison.OrdinalIgnoreCase))
        {
            return path;
        }

        content = InsertAfterTableHeader(
            content,
            "## Visit Notes",
            $"| {date} | {summary} | {NormalizeRiskLevel(extraction.RiskLevel)} | {source} {marker} |");

        foreach (var diagnosis in extraction.Diagnoses.Take(8))
        {
            content = InsertAfterTableHeader(
                content,
                "## Diagnoses / Conditions",
                $"| {NormalizeTableCell(diagnosis)} | active | {NormalizeRiskLevel(extraction.RiskLevel)} | {source} |");
        }

        foreach (var medication in extraction.ActiveMedications.Take(8))
        {
            content = InsertAfterTableHeader(
                content,
                "## Medications",
                $"| {NormalizeTableCell(medication)} | Unknown | active | {source} |");
        }

        foreach (var pendingItem in extraction.PendingItems.Take(8))
        {
            content = InsertAfterTableHeader(
                content,
                "## Pending Items",
                $"| {NormalizeTableCell(pendingItem)} | routine | open | {source} |");
        }

        File.WriteAllText(path, NormalizeContent(content));
        return path;
    }

    private static string UpdateTimeline(
        string wikiFolder,
        EncounterExtractionResult extraction,
        string encounterNodeName,
        string date)
    {
        var path = Path.Combine(wikiFolder, "Timeline.md");
        EnsureTimeline(path);
        var content = File.ReadAllText(path);
        var marker = $"<!-- encounter:{encounterNodeName} -->";

        if (content.Contains(marker, StringComparison.OrdinalIgnoreCase))
        {
            return path;
        }

        var row = $"| {date} | {NormalizeTableCell(extraction.DocumentType)} | {NormalizeTableCell(extraction.ClinicalGestalt)} | {NormalizeRiskLevel(extraction.RiskLevel)} | [[encounters/{encounterNodeName}]] {marker} |";
        content = InsertAfterTableHeader(content, string.Empty, row);
        File.WriteAllText(path, NormalizeContent(content));
        return path;
    }

    private static string UpdateCareGaps(
        string wikiFolder,
        EncounterExtractionResult extraction,
        string encounterNodeName,
        string date)
    {
        var path = Path.Combine(wikiFolder, "Care_Gaps.md");
        EnsureCareGaps(path);

        var content = File.ReadAllText(path);
        var source = $"[[encounters/{encounterNodeName}]]";
        var gapCandidates = BuildCareGapCandidates(extraction).ToList();

        foreach (var gap in gapCandidates)
        {
            if (content.Contains(gap.Title, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var row = $"| {NextSequentialId(content, "GAP")} | {NormalizeTableCell(gap.Title)} | {gap.Priority} | open | {date} | none | ingest | {NormalizeTableCell(gap.Notes)} {source} | none | 0 |";
            content = InsertAfterTableHeader(content, string.Empty, row);
        }

        File.WriteAllText(path, NormalizeContent(content));
        return path;
    }

    private static string UpdateVaccines(
        string wikiFolder,
        EncounterExtractionResult extraction,
        string encounterNodeName,
        string date)
    {
        var path = Path.Combine(wikiFolder, "Vaccines.md");
        EnsureVaccines(path);

        var content = File.ReadAllText(path);
        var source = $"[[encounters/{encounterNodeName}]]";
        foreach (var vaccine in ExtractVaccineMentions(extraction, date))
        {
            var marker = BuildVaccineMarker(vaccine.Name, vaccine.DateGiven);
            if (content.Contains(marker, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var row = $"| {vaccine.DateGiven} | {NormalizeTableCell(vaccine.Name)} | Unknown | Unknown | {source} {marker} | {NormalizeTableCell(vaccine.Notes)} |";
            content = InsertAfterTableHeader(content, string.Empty, row);
        }

        File.WriteAllText(path, NormalizeContent(content));
        return path;
    }

    private static string UpdateConflicts(
        string wikiFolder,
        EncounterExtractionResult extraction,
        string encounterNodeName,
        string date)
    {
        var path = Path.Combine(wikiFolder, "Conflicts.md");
        EnsureConflicts(path);

        var content = File.ReadAllText(path);
        var source = $"[[encounters/{encounterNodeName}]]";
        var conflictFlags = extraction.SafetyFlags
            .Where(flag => ContainsConflictSignal(flag))
            .Select(flag => NormalizeParagraph(flag))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var flag in conflictFlags)
        {
            if (content.Contains(flag, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var row = $"| {NextSequentialId(content, "CONFLICT")} | {date} | possible_clinical_conflict | active | {NormalizeTableCell(flag)} | {source} | pending_human_review |";
            content = InsertAfterTableHeader(content, string.Empty, row);
        }

        File.WriteAllText(path, NormalizeContent(content));
        return path;
    }

    private static void AppendSection(StringBuilder builder, string title, IEnumerable<string> items)
    {
        builder.AppendLine($"### {title}");
        builder.AppendLine();
        AppendList(builder, items);
        builder.AppendLine();
    }

    private static void AppendList(StringBuilder builder, IEnumerable<string> items)
    {
        var itemList = items
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .ToList();

        if (itemList.Count == 0)
        {
            builder.AppendLine("- Not documented in the provided sterile payload.");
            return;
        }

        foreach (var item in itemList)
        {
            builder.AppendLine($"- {item}");
        }
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

    private static void EnsureIndex(string path)
    {
        if (!File.Exists(path))
        {
            File.WriteAllText(
                path,
                "# Index\n\n## Visit Notes\n| Date | Summary | Risk Level | Source |\n|---|---|---|---|\n\n## Diagnoses / Conditions\n| Condition | Status | Risk Level | Source |\n|---|---|---|---|\n\n## Medications\n| Medication | Dose | Status | Source |\n|---|---|---|---|\n\n## Pending Items\n| Item | Priority | Status | Source |\n|---|---|---|---|\n");
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

    private static void EnsureConflicts(string path)
    {
        if (!File.Exists(path))
        {
            File.WriteAllText(
                path,
                "# Conflicts\n\n| Conflict ID | Date | Type | Status | Description | Source | Resolution |\n|---|---|---|---|---|---|---|\n");
        }
    }

    private static void EnsureVaccines(string path)
    {
        if (!File.Exists(path))
        {
            File.WriteAllText(
                path,
                "# Vaccines\n\n| Date Given | Vaccine | Dose / Series | Location / Provider | Source | Notes |\n|---|---|---|---|---|---|\n");
        }
    }

    private static IEnumerable<(string Name, string DateGiven, string Notes)> ExtractVaccineMentions(
        EncounterExtractionResult extraction,
        string fallbackDate)
    {
        var text = string.Join(
            " ",
            new[]
            {
                extraction.DocumentType,
                extraction.ClinicalGestalt,
                extraction.ChiefComplaint
            }
            .Concat(extraction.AssessmentPlan)
            .Concat(extraction.PendingItems)
            .Concat(extraction.SafetyFlags)
            .Concat(extraction.LabsResults));

        if (!Regex.IsMatch(text, @"\b(vaccine|vaccination|immunization|immunisation|tdap|influenza|flu|covid|rsv|shingles|zoster|pneumococcal|mmr|varicella|hepatitis|hpv|meningococcal)\b", RegexOptions.IgnoreCase))
        {
            yield break;
        }

        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["tdap"] = "Tdap",
            ["influenza"] = "Influenza",
            ["flu"] = "Influenza",
            ["covid"] = "COVID",
            ["rsv"] = "RSV",
            ["shingles"] = "Shingles",
            ["zoster"] = "Shingles",
            ["pneumococcal"] = "Pneumococcal",
            ["mmr"] = "MMR",
            ["varicella"] = "Varicella",
            ["hepatitis"] = "Hepatitis",
            ["hpv"] = "HPV",
            ["meningococcal"] = "Meningococcal"
        };

        var date = FindFirstDate(text, fallbackDate);
        foreach (var pair in names)
        {
            if (Regex.IsMatch(text, $@"\b{Regex.Escape(pair.Key)}\b", RegexOptions.IgnoreCase))
            {
                yield return (pair.Value, date, "Extracted from sterile encounter text.");
            }
        }
    }

    private static string FindFirstDate(string text, string fallbackDate)
    {
        var match = Regex.Match(text, @"\b\d{1,2}[/-]\d{1,2}[/-]\d{2,4}\b");
        return match.Success && DateOnly.TryParse(match.Value, out var parsed)
            ? parsed.ToString("yyyy-MM-dd")
            : fallbackDate;
    }

    private static string BuildVaccineMarker(string vaccineName, string dateGiven)
    {
        var key = Regex.Replace($"{vaccineName}-{dateGiven}".ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
        return $"<!-- vaccine:{key} -->";
    }

    private static string NormalizeDate(string value)
    {
        return DateOnly.TryParse(value, out var parsed)
            ? parsed.ToString("yyyy-MM-dd")
            : DateTime.Now.ToString("yyyy-MM-dd");
    }

    private static string NormalizeRiskLevel(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        return normalized is "high" or "moderate" or "low" ? normalized : "low";
    }

    private static string NormalizeDirective(string value, string riskLevel)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value.Trim();
        }

        return riskLevel switch
        {
            "high" => "MANDATORY_LOCAL_RAW_OR_SCRUBBED_PULL",
            "moderate" => "CONTEXTUAL_TRUST",
            _ => "WIKI_NATIVE"
        };
    }

    private static string NormalizeUnknown(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "Unknown" : value.Trim();
    }

    private static string NormalizeParagraph(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "No clinical gestalt was extracted from the provided sterile payload."
            : Regex.Replace(value.Trim(), @"\s+", " ");
    }

    private static string NormalizeTableCell(string value)
    {
        var normalized = NormalizeParagraph(value)
            .Replace("|", "/")
            .Replace("\r", " ")
            .Replace("\n", " ");

        return normalized.Length <= 220 ? normalized : normalized[..220] + "...";
    }

    private static string BuildSafeSlug(string value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? "Source_Document" : value.Trim();
        normalized = Regex.Replace(normalized, @"[^A-Za-z0-9_-]+", "_").Trim('_');
        return string.IsNullOrWhiteSpace(normalized) ? "Source_Document" : normalized;
    }

    private static IEnumerable<CareGapCandidate> BuildCareGapCandidates(EncounterExtractionResult extraction)
    {
        foreach (var pendingItem in extraction.PendingItems.Where(item => !string.IsNullOrWhiteSpace(item)))
        {
            yield return new CareGapCandidate(
                pendingItem.Trim(),
                DetermineCareGapPriority(pendingItem, extraction.RiskLevel),
                "Pending item extracted from the encounter.");
        }

        foreach (var planItem in extraction.AssessmentPlan.Where(ContainsCareGapSignal))
        {
            yield return new CareGapCandidate(
                planItem.Trim(),
                DetermineCareGapPriority(planItem, extraction.RiskLevel),
                "Follow-up action detected in the assessment and plan.");
        }
    }

    private static bool ContainsCareGapSignal(string value)
    {
        return value.Contains("follow", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("pending", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("schedule", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("ordered", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("referral", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("surveillance", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("await", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("formal report", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("contacted", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsConflictSignal(string value)
    {
        return value.Contains("conflict", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("contradict", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("discrepanc", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("inconsistent", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("mismatch", StringComparison.OrdinalIgnoreCase);
    }

    private static string DetermineCareGapPriority(string value, string riskLevel)
    {
        if (value.Contains("immediate", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("urgent", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("critical", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("panic", StringComparison.OrdinalIgnoreCase))
        {
            return "urgent";
        }

        return NormalizeRiskLevel(riskLevel) switch
        {
            "high" => "high",
            "moderate" => "moderate",
            _ => "routine"
        };
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

    private static string EscapeYaml(string value)
    {
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
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

    private static string NormalizeContent(string content)
    {
        return content.Replace("\r\n", "\n").TrimEnd() + Environment.NewLine;
    }

    private sealed record CareGapCandidate(string Title, string Priority, string Notes);
}
