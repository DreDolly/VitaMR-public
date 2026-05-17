namespace VitaMR.Models;

public sealed class DreamRunnerAuditResult
{
    public bool WasRun { get; set; }

    public string Status { get; set; } = "DREAM_RUNNER_NOT_RUN";

    public string ChartId { get; set; } = string.Empty;

    public string LogPath { get; set; } = string.Empty;

    public int FilesChecked { get; set; }

    public int MissingRequiredFiles { get; set; }

    public int BrokenLinks { get; set; }

    public int UnverifiedPages { get; set; }

    public int OpenCareGaps { get; set; }

    public int ActiveConflicts { get; set; }

    public List<DreamRunnerAuditFinding> Findings { get; set; } = [];
}
