namespace VitaMR.Models;

public sealed class LocalModelReviewedPayload
{
    public string SourcePath { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string IntegrityStatus { get; set; } = "not_run";

    public string IntegrityRecommendation { get; set; } = string.Empty;

    public List<string> IntegrityIssues { get; set; } = [];

    public List<LocalModelFinding> Findings { get; set; } = [];
}
