using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using VitaMR.Models;

namespace VitaMR.Services;

public sealed class MarkdownSpecialtyWeaverService : ISpecialtyWeaverService
{
    public SpecialtyWeaverResult WeaveChart(string vaultRoot, ChartContext chartContext)
    {
        var result = new SpecialtyWeaverResult
        {
            WasRun = true,
            ChartId = chartContext.ChartId,
            Status = "SPECIALTY_WEAVER_STARTED"
        };

        var chartRoot = Path.Combine(vaultRoot, chartContext.ChartFolderName);
        var wikiFolder = Path.Combine(chartRoot, "wiki");
        var encountersFolder = Path.Combine(wikiFolder, "encounters");
        var specialtyFolder = Path.Combine(wikiFolder, "specialty");

        Directory.CreateDirectory(specialtyFolder);
        result.SpecialtyFilesEnsured = EnsureSpecialtyFiles(specialtyFolder, chartContext);

        if (!Directory.Exists(encountersFolder))
        {
            result.Status = "SPECIALTY_WEAVER_NO_ENCOUNTERS";
            result.Messages.Add("No encounter folder exists yet for this chart.");
            result.LogPath = AppendLog(wikiFolder, chartContext, result);
            return result;
        }

        foreach (var encounterPath in Directory.EnumerateFiles(encountersFolder, "*.md").OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            result.EncounterNodesScanned++;
            var encounter = ReadEncounter(encounterPath);

            if (encounter.ConflictStatus.Equals("pending_human", StringComparison.OrdinalIgnoreCase))
            {
                result.NodesSkipped++;
                result.Messages.Add($"Skipped {Path.GetFileName(encounterPath)} because conflict_status is pending_human.");
                continue;
            }

            var assignments = RouteEncounter(encounter);

            if (assignments.Count == 0)
            {
                assignments.Add(new SpecialtyAssignment("PrimaryCare_Preventative", "low"));
            }

            foreach (var assignment in assignments)
            {
                var specialtyPath = Path.Combine(specialtyFolder, $"{assignment.SpecialtyName}.md");
                EnsureSpecialtyFile(specialtyPath, chartContext, assignment.SpecialtyName, GetDomain(assignment.SpecialtyName));

                if (WriteSpecialtyRow(specialtyPath, chartContext, encounter, assignment))
                {
                    result.RowsWritten++;
                    AddUnique(result.UpdatedSpecialties, assignment.SpecialtyName);
                    RegisterLink(vaultRoot, chartContext, assignment.SpecialtyName, encounter);
                }
                else
                {
                    result.DuplicateRowsSkipped++;
                }
            }
        }

        result.Status = result.RowsWritten > 0
            ? "SPECIALTY_WEAVER_UPDATED"
            : "SPECIALTY_WEAVER_NO_NEW_ROWS";
        result.LogPath = AppendLog(wikiFolder, chartContext, result);
        return result;
    }

    public static int EnsureSpecialtyFiles(string specialtyFolder, ChartContext chartContext)
    {
        Directory.CreateDirectory(specialtyFolder);
        var ensured = 0;

        foreach (var lens in SpecialtyLensCatalog.Lenses)
        {
            var path = Path.Combine(specialtyFolder, $"{lens.Name}.md");
            var existed = File.Exists(path);
            EnsureSpecialtyFile(path, chartContext, lens.Name, lens.Domain);

            if (!existed)
            {
                ensured++;
            }
        }

        return ensured;
    }

    private static void EnsureSpecialtyFile(string path, ChartContext chartContext, string specialtyName, string domain)
    {
        if (File.Exists(path))
        {
            return;
        }

        var title = specialtyName.Replace('_', ' ');
        var content =
            $"""
            ---
            chart_id: "{chartContext.ChartId}"
            document_type: "Specialty_Lens"
            specialty: "{specialtyName}"
            managed_by: "Specialty Weaver v0"
            status: scaffold
            ---

            # {title}

            > **Summary:** Chronological record of {domain.ToLowerInvariant()} for {chartContext.ChartId}. Maintained by Specialty Weaver. Most recent clinical entry appears first.

            | Date (YYYY-MM-DD) | Ingested (YYYY-MM-DD HH:mm) | Event/Document Type | Key Finding / Metric | Status/Trend | Routing Confidence | Wiki Link |
            |---|---|---|---|---|---|---|

            """;

        File.WriteAllText(path, NormalizeContent(content));
    }

    private static EncounterRouteSource ReadEncounter(string path)
    {
        var content = File.ReadAllText(path);
        var fileName = Path.GetFileNameWithoutExtension(path);
        return new EncounterRouteSource(
            path,
            Path.GetFileName(path),
            ReadYamlValue(content, "date_of_service", ExtractDateFromFileName(fileName)),
            ReadYamlValue(content, "document_type", BuildTitleFromFileName(fileName)),
            ReadYamlValue(content, "risk_level", "unknown"),
            ReadYamlValue(content, "conflict_status", "none"),
            content);
    }

    private static List<SpecialtyAssignment> RouteEncounter(EncounterRouteSource encounter)
    {
        var text = $"{encounter.DocumentType}\n{encounter.Content}";
        var assignments = new List<SpecialtyAssignment>();

        AddIf(assignments, "Cardiology", text, "high", @"cardio|heart|cardiac|chest pain|chest heaviness|syncope|near-syncope|dyspnea|metoprolol|arrhythmia|echocardiogram|echo|lvef|hypertrophic|cardiomyopathy|sudden cardiac");
        AddIf(assignments, "Imaging_Radiology", text, "moderate", @"radiology|imaging|x-ray|xray|cxr|ct\b|mri\b|pet\b|ultrasound|echocardiogram|echo|formal read|official read");
        AddIf(assignments, "Labs_Master", text, "moderate", @"\blab|cbc|bmp|cmp|a1c|glucose|creatinine|egfr|troponin|hemoglobin|platelet|sodium|potassium|lft|ast|alt|bilirubin|culture");
        AddIf(assignments, "Pulmonology", text, "moderate", @"shortness of breath|dyspnea|wheeze|asthma|copd|pulmonary|oxygen|sleep apnea|cough|respiratory");
        AddIf(assignments, "Gastroenterology", text, "moderate", @"epigastric|abdominal|nausea|vomit|diarrhea|constipation|reflux|gerd|gi\b|gastro|colonoscopy|bowel");
        AddIf(assignments, "Neurology", text, "moderate", @"headache|migraine|seizure|dizziness|vertigo|syncope|weakness|numbness|cognitive|neurolog");
        AddIf(assignments, "Psychiatry_Behavioral", text, "moderate", @"depression|anxiety|bipolar|psych|behavior|substance|alcohol|sleep disturbance|adhd");
        AddIf(assignments, "Orthopedics_SportsMed", text, "moderate", @"sports|athlete|orthopedic|fracture|joint|knee|shoulder|hip|ankle|back pain|musculoskeletal|clearance");
        AddIf(assignments, "Endocrinology", text, "moderate", @"diabetes|thyroid|testosterone|steroid|endocrine|hormone|a1c|glucose|adrenal");
        AddIf(assignments, "Infectious_Disease", text, "moderate", @"infection|sepsis|fever|antibiotic|hiv|covid|influenza|pneumonia|cellulitis|culture");
        AddIf(assignments, "Oncology_Hematology", text, "moderate", @"cancer|tumor|malign|oncology|hematology|anemia|leukemia|lymphoma|chemotherapy");
        AddIf(assignments, "Pathology_Cytology", text, "high", @"pathology|cytology|biopsy|histology|margin|immunohistochemistry");
        AddIf(assignments, "Emergency_CriticalCare", text, "high", @"emergency|er visit|ed visit|icu|critical care|rapid response|discharge summary|hospital");
        AddIf(assignments, "PrimaryCare_Preventative", text, "moderate", @"primary care|annual|preventive|screening|immunization|well visit|clinical visit|routine");

        return assignments
            .GroupBy(assignment => assignment.SpecialtyName, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(assignment => ConfidenceRank(assignment.RoutingConfidence)).First())
            .ToList();
    }

    private static bool WriteSpecialtyRow(
        string specialtyPath,
        ChartContext chartContext,
        EncounterRouteSource encounter,
        SpecialtyAssignment assignment)
    {
        var content = File.ReadAllText(specialtyPath);
        var encounterWikiName = Path.GetFileNameWithoutExtension(encounter.FileName);
        var wikiLink = $"[[encounters/{encounterWikiName}]]";

        if (content.Contains(wikiLink, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var status = assignment.RoutingConfidence.Equals("high", StringComparison.OrdinalIgnoreCase)
            ? "routed"
            : "routed-review";
        var row = $"| {EscapeTable(encounter.DateOfService)} | {DateTime.Now:yyyy-MM-dd HH:mm} | {EscapeTable(encounter.DocumentType)} | ![[{encounterWikiName}#Clinical Gestalt]] | {status} | {EscapeTable(assignment.RoutingConfidence)} | {wikiLink} |";
        var lines = content.Replace("\r\n", "\n").Split('\n').ToList();
        var separatorIndex = lines.FindIndex(line => line.Trim().StartsWith("|---", StringComparison.Ordinal));

        if (separatorIndex < 0)
        {
            lines.Add("| Date (YYYY-MM-DD) | Ingested (YYYY-MM-DD HH:mm) | Event/Document Type | Key Finding / Metric | Status/Trend | Routing Confidence | Wiki Link |");
            lines.Add("|---|---|---|---|---|---|---|");
            separatorIndex = lines.Count - 1;
        }

        lines.Insert(separatorIndex + 1, row);
        File.WriteAllText(specialtyPath, NormalizeContent(string.Join('\n', lines)));
        return true;
    }

    private static void RegisterLink(string vaultRoot, ChartContext chartContext, string specialtyName, EncounterRouteSource encounter)
    {
        var path = Path.Combine(vaultRoot, "_Link_Registry.md");
        EnsureLinkRegistry(path);
        var encounterWikiName = Path.GetFileNameWithoutExtension(encounter.FileName);
        var row = $"| wiki/specialty/{specialtyName}.md | wiki/encounters/{encounter.FileName} | Clinical Gestalt | {chartContext.ChartId} | {encounter.DateOfService} | specialty weaver v0 | active |";
        var content = File.ReadAllText(path);

        if (content.Contains(row, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        File.AppendAllText(path, row + Environment.NewLine);
    }

    private static string AppendLog(string wikiFolder, ChartContext chartContext, SpecialtyWeaverResult result)
    {
        Directory.CreateDirectory(wikiFolder);
        var path = Path.Combine(wikiFolder, "Specialty_Weaver_Log.md");
        var builder = new StringBuilder();

        if (!File.Exists(path))
        {
            builder.AppendLine("# Specialty Weaver Log");
            builder.AppendLine();
        }

        builder.AppendLine($"## {DateTime.Now:yyyy-MM-dd HH:mm} - {chartContext.ChartId}");
        builder.AppendLine();
        builder.AppendLine($"- Status: {result.Status}");
        builder.AppendLine($"- Specialty files ensured: {result.SpecialtyFilesEnsured}");
        builder.AppendLine($"- Encounter nodes scanned: {result.EncounterNodesScanned}");
        builder.AppendLine($"- Rows written: {result.RowsWritten}");
        builder.AppendLine($"- Duplicate rows skipped: {result.DuplicateRowsSkipped}");
        builder.AppendLine($"- Nodes skipped: {result.NodesSkipped}");
        builder.AppendLine($"- Updated specialties: {string.Join(", ", result.UpdatedSpecialties.OrderBy(value => value))}");

        foreach (var message in result.Messages.Take(20))
        {
            builder.AppendLine($"- Note: {message}");
        }

        builder.AppendLine();
        File.AppendAllText(path, NormalizeContent(builder.ToString()));
        return path;
    }

    private static void EnsureLinkRegistry(string path)
    {
        if (File.Exists(path))
        {
            return;
        }

        File.WriteAllText(
            path,
            "# Link Registry\n\n| Source Page | Transcluded Target | Header Anchor | Chart ID | Date | Notes | Status |\n|---|---|---|---|---|---|---|\n");
    }

    private static void AddIf(List<SpecialtyAssignment> assignments, string specialtyName, string text, string confidence, string pattern)
    {
        if (Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase))
        {
            assignments.Add(new SpecialtyAssignment(specialtyName, confidence));
        }
    }

    private static string ReadYamlValue(string content, string key, string fallback)
    {
        var match = Regex.Match(content, $@"(?im)^\s*{Regex.Escape(key)}\s*:\s*""?(?<value>[^""\r\n]+)""?\s*$");
        return match.Success && !string.IsNullOrWhiteSpace(match.Groups["value"].Value)
            ? match.Groups["value"].Value.Trim()
            : fallback;
    }

    private static string ExtractDateFromFileName(string fileName)
    {
        var match = Regex.Match(fileName, @"\d{4}-\d{2}-\d{2}");
        return match.Success ? match.Value : DateTime.Today.ToString("yyyy-MM-dd");
    }

    private static string BuildTitleFromFileName(string fileName)
    {
        return Regex.Replace(fileName, @"^\d{4}-\d{2}-\d{2}_?", string.Empty)
            .Replace("_Node", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace('_', ' ')
            .Trim();
    }

    private static int ConfidenceRank(string confidence)
    {
        return confidence.ToLowerInvariant() switch
        {
            "high" => 3,
            "moderate" => 2,
            _ => 1
        };
    }

    private static string GetDomain(string specialtyName)
    {
        return SpecialtyLensCatalog.Lenses.FirstOrDefault(lens =>
            lens.Name.Equals(specialtyName, StringComparison.OrdinalIgnoreCase))?.Domain ?? specialtyName;
    }

    private static void AddUnique(ICollection<string> values, string value)
    {
        if (!values.Contains(value, StringComparer.OrdinalIgnoreCase))
        {
            values.Add(value);
        }
    }

    private static string EscapeTable(string value)
    {
        return Regex.Replace(value.Replace("|", "/").Trim(), @"\s+", " ");
    }

    private static string NormalizeContent(string content)
    {
        return content.Replace("\r\n", "\n").TrimEnd() + Environment.NewLine;
    }

    private sealed record EncounterRouteSource(
        string Path,
        string FileName,
        string DateOfService,
        string DocumentType,
        string RiskLevel,
        string ConflictStatus,
        string Content);

    private sealed record SpecialtyAssignment(string SpecialtyName, string RoutingConfidence);
}
