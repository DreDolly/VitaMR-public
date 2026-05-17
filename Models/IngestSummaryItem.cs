namespace VitaMR.Models;

public sealed class IngestSummaryItem
{
    public string DisplayName { get; set; } = string.Empty;

    public string DocumentType { get; set; } = string.Empty;

    public string DateOfService { get; set; } = string.Empty;

    public string ClinicalGestalt { get; set; } = string.Empty;

    public List<string> MainClinicalFacts { get; set; } = [];

    public List<string> PendingItems { get; set; } = [];

    public List<string> TopicPages { get; set; } = [];

    public string EncounterNodePath { get; set; } = string.Empty;
}
