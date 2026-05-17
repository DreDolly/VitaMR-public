namespace VitaMR.Models;

public sealed class PreVisitBriefResult
{
    public bool WasRun { get; set; }

    public string Status { get; set; } = "PRE_VISIT_BRIEF_NOT_RUN";

    public string OutputPath { get; set; } = string.Empty;

    public int SectionsIncluded { get; set; }

    public List<string> Messages { get; } = [];
}
