using VitaMR.Models;

namespace VitaMR.Services;

public interface IDreamRunnerAuditService
{
    DreamRunnerAuditResult AuditChart(string vaultRoot, ChartContext chartContext);
}
