using VitaMR.Models;

namespace VitaMR.Services;

public interface IDrugInteractionScannerService
{
    DrugInteractionScanResult ScanChart(string vaultRoot, ChartContext chartContext, string trigger);
}
