namespace VitaMR.Models;

public sealed class DollyWorkingSummaryResult
{
    public bool WasWritten { get; set; }

    public string Status { get; set; } = "DOLLY_WORKING_SUMMARY_NOT_RUN";

    public string ChartId { get; set; } = string.Empty;

    public string PatientDisplayName { get; set; } = string.Empty;

    public string SummaryPath { get; set; } = string.Empty;

    public int SectionsIncluded { get; set; }

    public List<string> ConversationHooks { get; } = [];

    public List<string> Messages { get; } = [];
}
