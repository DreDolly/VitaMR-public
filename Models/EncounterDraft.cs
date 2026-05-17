namespace VitaMR.Models;

public sealed class EncounterDraft
{
    public string DraftId { get; set; } = string.Empty;

    public string ChartId { get; set; } = string.Empty;

    public DateOnly DocumentDate { get; set; }

    public string DocumentType { get; set; } = "Unknown";

    public string RiskTier { get; set; } = "Tier 3";

    public string ClinicalGestalt { get; set; } = string.Empty;

    public List<string> Diagnoses { get; set; } = [];

    public List<string> Medications { get; set; } = [];

    public List<string> Labs { get; set; } = [];

    public List<string> Recommendations { get; set; } = [];

    public List<string> ProposedTopicLinks { get; set; } = [];

    public List<string> Citations { get; set; } = [];

    public bool IsAiGeneratedDraft { get; set; } = true;
}
