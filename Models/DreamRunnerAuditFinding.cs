namespace VitaMR.Models;

public sealed class DreamRunnerAuditFinding
{
    public string Severity { get; set; } = "info";

    public string FilePath { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;
}
