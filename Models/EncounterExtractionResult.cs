namespace VitaMR.Models;

public sealed class EncounterExtractionResult
{
    public string ChartId { get; set; } = string.Empty;

    public string SourceFileId { get; set; } = string.Empty;

    public string DocumentType { get; set; } = "Source_Document";

    public string DateOfService { get; set; } = string.Empty;

    public string RiskLevel { get; set; } = "low";

    public string DollyDirective { get; set; } = "WIKI_NATIVE";

    public string SourceSystem { get; set; } = "Unknown";

    public string SourceFacility { get; set; } = "Unknown";

    public string SourceType { get; set; } = "Unknown";

    public string ReadingStatus { get; set; } = "formal";

    public string ClinicalGestalt { get; set; } = string.Empty;

    public List<string> VitalSigns { get; set; } = [];

    public string ChiefComplaint { get; set; } = string.Empty;

    public List<string> ActiveMedications { get; set; } = [];

    public List<string> Diagnoses { get; set; } = [];

    public List<string> LabsResults { get; set; } = [];

    public List<string> Imaging { get; set; } = [];

    public List<string> AssessmentPlan { get; set; } = [];

    public List<string> PendingItems { get; set; } = [];

    public List<string> ProposedTopicPages { get; set; } = [];

    public List<string> Citations { get; set; } = [];

    public List<string> SafetyFlags { get; set; } = [];
}
