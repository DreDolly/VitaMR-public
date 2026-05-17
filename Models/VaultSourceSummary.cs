namespace VitaMR.Models;

public sealed class VaultSourceSummary
{
    public string SourceType { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Path { get; set; } = string.Empty;

    public string Citation { get; set; } = string.Empty;

    public string RiskLevel { get; set; } = string.Empty;

    public string DollyDirective { get; set; } = string.Empty;

    public string ConflictStatus { get; set; } = string.Empty;

    public string ReadingStatus { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;
}
