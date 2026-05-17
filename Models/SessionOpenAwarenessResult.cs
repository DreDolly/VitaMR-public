namespace VitaMR.Models;

public sealed class SessionOpenAwarenessResult
{
    public bool WasRun { get; set; }

    public string Status { get; set; } = "SESSION_OPEN_NOT_RUN";

    public string ActiveChartId { get; set; } = string.Empty;

    public string ActivePatientDisplayName { get; set; } = string.Empty;

    public List<string> Lines { get; } = [];
}
