using VitaMR.Models;

namespace VitaMR.Services;

public sealed class MockEncounterDraftService : IEncounterDraftService
{
    private static readonly string[] AllowedRiskTiers = ["Tier 1", "Tier 2", "Tier 3"];

    public EncounterDraft CreateMockDraft(
        ChartContext chart,
        IReadOnlyList<IngestResult> ingestResults,
        IReadOnlyList<ScrubResult> scrubResults,
        string userPrompt)
    {
        var firstIngest = ingestResults.FirstOrDefault();
        var firstScrub = scrubResults.FirstOrDefault();
        var displayName = firstIngest?.DisplayName ?? "Direct conversation";
        var documentType = InferDocumentType(displayName);
        var riskTier = InferMockRiskTier(displayName, firstScrub?.Preview ?? userPrompt);

        return new EncounterDraft
        {
            DraftId = $"draft-{DateTime.Now:yyyyMMdd-HHmmss}",
            ChartId = chart.ChartId,
            DocumentDate = DateOnly.FromDateTime(firstIngest?.IngestedAt ?? DateTime.Now),
            DocumentType = documentType,
            RiskTier = riskTier,
            ClinicalGestalt = "Mock structured draft created from scrubbed local payload. This is a schema test only; no external API call has been made.",
            Diagnoses = ["Pending AI extraction"],
            Medications = ["Pending AI extraction"],
            Labs = ["Pending AI extraction"],
            Recommendations = ["Route scrubbed payload to main AI agent for structured wiki schema when API gate is enabled."],
            ProposedTopicLinks = [documentType, "Source Provenance", "Ingest Review"],
            Citations = ingestResults
                .Select(result => $"raw/{result.DisplayName}")
                .ToList(),
            IsAiGeneratedDraft = false
        };
    }

    public DraftValidationResult Validate(EncounterDraft draft)
    {
        var messages = new List<string>();

        if (string.IsNullOrWhiteSpace(draft.DraftId))
        {
            messages.Add("DraftId is required.");
        }

        if (string.IsNullOrWhiteSpace(draft.ChartId))
        {
            messages.Add("ChartId is required.");
        }

        if (!AllowedRiskTiers.Contains(draft.RiskTier))
        {
            messages.Add("RiskTier must be Tier 1, Tier 2, or Tier 3.");
        }

        if (string.IsNullOrWhiteSpace(draft.DocumentType))
        {
            messages.Add("DocumentType is required.");
        }

        if (string.IsNullOrWhiteSpace(draft.ClinicalGestalt))
        {
            messages.Add("ClinicalGestalt is required.");
        }

        if (draft.Citations.Count == 0)
        {
            messages.Add("At least one citation is required before a wiki encounter can be written.");
        }

        if (messages.Count == 0)
        {
            messages.Add("Encounter draft contract valid.");
        }

        return new DraftValidationResult(messages.Count == 1 && messages[0] == "Encounter draft contract valid.", messages);
    }

    private static string InferDocumentType(string displayName)
    {
        var lowerName = displayName.ToLowerInvariant();

        if (lowerName.Contains("lab"))
        {
            return "Lab";
        }

        if (lowerName.Contains("imaging") || lowerName.Contains("mri") || lowerName.Contains("ct"))
        {
            return "Imaging";
        }

        if (lowerName.Contains("med") || lowerName.Contains("reconcile"))
        {
            return "Medication_Reconciliation";
        }

        if (lowerName.Contains("note") || lowerName.Contains("visit"))
        {
            return "Clinical_Note";
        }

        return "Source_Document";
    }

    private static string InferMockRiskTier(string displayName, string text)
    {
        var combined = $"{displayName} {text}".ToLowerInvariant();

        if (combined.Contains("biopsy") ||
            combined.Contains("pathology") ||
            combined.Contains("mri") ||
            combined.Contains("ct ") ||
            combined.Contains("panic") ||
            combined.Contains("critical"))
        {
            return "Tier 1";
        }

        if (combined.Contains("lab") ||
            combined.Contains("med") ||
            combined.Contains("follow"))
        {
            return "Tier 2";
        }

        return "Tier 3";
    }
}
