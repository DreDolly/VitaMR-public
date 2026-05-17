using VitaMR.Models;

namespace VitaMR.Services;

public interface IChronosLedgerService
{
    void EnsureLedger(string vaultRoot);

    void RecordEvent(
        string vaultRoot,
        ChartContext? chartContext,
        string eventType,
        string summary,
        string actor = "C#",
        string source = "VitaMR",
        string visibility = "sterile");

    string BuildRecentContext(
        string vaultRoot,
        ChartContext? chartContext,
        int maxEntries = 12);
}
