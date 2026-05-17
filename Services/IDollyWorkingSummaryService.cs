using VitaMR.Models;

namespace VitaMR.Services;

public interface IDollyWorkingSummaryService
{
    DollyWorkingSummaryResult RefreshSummary(string vaultRoot, ChartContext chartContext);
}
