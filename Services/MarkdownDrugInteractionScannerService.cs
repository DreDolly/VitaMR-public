using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using VitaMR.Models;

namespace VitaMR.Services;

public sealed class MarkdownDrugInteractionScannerService : IDrugInteractionScannerService
{
    public DrugInteractionScanResult ScanChart(string vaultRoot, ChartContext chartContext, string trigger)
    {
        var result = new DrugInteractionScanResult
        {
            WasRun = true,
            Status = "DRUG_INTERACTION_SCAN_STARTED"
        };

        var wikiFolder = Path.Combine(vaultRoot, chartContext.ChartFolderName, "wiki");
        Directory.CreateDirectory(wikiFolder);
        EnsureDrugInteractionFile(wikiFolder, chartContext);

        var indexText = ReadIfExists(Path.Combine(wikiFolder, "Index.md"));
        var meds = ExtractMedicationNames(indexText).ToList();
        result.ActiveMedicationsReviewed = meds.Count;

        foreach (var finding in RunKnownRuleChecks(meds, indexText))
        {
            result.Findings.Add(finding);
        }

        result.OutputPath = AppendScan(wikiFolder, chartContext, trigger, meds, result.Findings);
        result.FindingsWritten = result.Findings.Count;
        result.Status = result.Findings.Count > 0
            ? "DRUG_INTERACTION_SCAN_FLAGS_FOUND"
            : "DRUG_INTERACTION_SCAN_NO_V0_FLAGS";
        SchedulerFileService.AppendScheduleLog(
            vaultRoot,
            "TASK-008",
            "Drug Interaction Scan",
            "manual",
            result.Status,
            $"chart={chartContext.ChartId}; meds={meds.Count}; findings={result.Findings.Count}");
        return result;
    }

    public static void EnsureDrugInteractionFile(string wikiFolder, ChartContext chartContext)
    {
        Directory.CreateDirectory(wikiFolder);
        var path = Path.Combine(wikiFolder, "Drug_Interactions.md");

        if (!File.Exists(path))
        {
            File.WriteAllText(
                path,
                Normalize(
                    $"---\nchart_id: \"{chartContext.ChartId}\"\ndocument_type: \"Drug_Interactions\"\nrisk_level: moderate\ndolly_directive: \"CONTEXTUAL_TRUST\"\nstatus: scaffold\n---\n\n# Drug Interactions\n\n> **Summary:** Prototype known-rule medication scan. This is not a complete drug database or pharmacist review.\n\n| Date | Trigger | Medication Count | Findings | Status |\n|---|---|---|---|---|\n"));
        }
    }

    private static IEnumerable<string> RunKnownRuleChecks(IReadOnlyList<string> meds, string indexText)
    {
        var all = string.Join(" ", meds);
        var hasAntiplatelet = ContainsAny(all, "aspirin", "clopidogrel", "prasugrel", "ticagrelor");
        var hasP2Y12 = ContainsAny(all, "clopidogrel", "prasugrel", "ticagrelor");
        var hasNsaid = ContainsAny(all, "ibuprofen", "naproxen", "diclofenac", "meloxicam", "celecoxib");
        var hasAnticoagulant = ContainsAny(all, "warfarin", "apixaban", "rivaroxaban", "dabigatran", "edoxaban", "heparin", "enoxaparin");
        var hasBetaBlocker = ContainsAny(all, "metoprolol", "atenolol", "carvedilol", "propranolol", "bisoprolol");
        var hasPulmonaryRisk = ContainsAny(indexText, "asthma", "copd", "reactive airway");
        var hasRenalOrBpRisk = ContainsAny(indexText, "chronic kidney", "ckd", "renal insufficiency", "hypertension");

        if (hasP2Y12 && hasNsaid)
        {
            yield return "S1 prototype flag: P2Y12 antiplatelet plus NSAID-like medication. DAPT standing rule requires clinician/pharmacist review.";
        }

        if (hasAnticoagulant && hasAntiplatelet)
        {
            yield return "S2 prototype flag: anticoagulant plus antiplatelet medication may increase bleeding risk.";
        }

        if (hasBetaBlocker && hasPulmonaryRisk)
        {
            yield return "S3 prototype flag: beta-blocker with asthma/COPD/reactive-airway history may warrant clinician review.";
        }

        if (hasNsaid && hasRenalOrBpRisk)
        {
            yield return "S3 prototype flag: NSAID-like medication with kidney disease or hypertension risk may warrant monitoring/review.";
        }
    }

    private static string AppendScan(string wikiFolder, ChartContext chartContext, string trigger, IReadOnlyList<string> meds, IReadOnlyList<string> findings)
    {
        var path = Path.Combine(wikiFolder, "Drug_Interactions.md");
        EnsureDrugInteractionFile(wikiFolder, chartContext);
        var builder = new StringBuilder();
        var status = findings.Count > 0 ? "flags_found" : "no_v0_flags";
        var summary = findings.Count > 0
            ? string.Join("<br>", findings.Select(EscapeTable))
            : "No v0 known-rule flags found. This does not mean no interactions exist.";

        builder.AppendLine($"| {DateTime.Now:yyyy-MM-dd HH:mm} | {EscapeTable(trigger)} | {meds.Count} | {summary} | {status} |");
        builder.AppendLine();
        builder.AppendLine($"## Scan: {DateTime.Now:yyyy-MM-dd HH:mm} - {trigger}");
        builder.AppendLine();
        builder.AppendLine($"**Medication list reviewed:** {(meds.Count == 0 ? "No active medication rows found in Index.md." : string.Join(", ", meds))}");
        builder.AppendLine();
        builder.AppendLine("**Prototype scope:** Known-rule scan only; not a complete interaction database.");
        builder.AppendLine();

        foreach (var finding in findings)
        {
            builder.AppendLine($"- {finding}");
        }

        if (findings.Count == 0)
        {
            builder.AppendLine("- No v0 known-rule flags found.");
        }

        builder.AppendLine();
        File.AppendAllText(path, Normalize(builder.ToString()));
        return path;
    }

    private static IEnumerable<string> ExtractMedicationNames(string indexText)
    {
        var inMedicationSection = false;

        foreach (var line in indexText.Split('\n'))
        {
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                inMedicationSection = line.Contains("Medications", StringComparison.OrdinalIgnoreCase);
                continue;
            }

            if (!inMedicationSection || !line.TrimStart().StartsWith('|') || line.Contains("---", StringComparison.Ordinal))
            {
                continue;
            }

            var cells = line.Trim().Trim('|').Split('|').Select(cell => cell.Trim()).ToList();

            if (cells.Count > 0 && !cells[0].Equals("Medication", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(cells[0]))
            {
                yield return cells[0];
            }
        }
    }

    private static bool ContainsAny(string value, params string[] terms)
    {
        return terms.Any(term => Regex.IsMatch(value, $@"\b{Regex.Escape(term)}\b", RegexOptions.IgnoreCase));
    }

    private static string ReadIfExists(string path)
    {
        return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
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
