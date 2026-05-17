using VitaMR.Models;

namespace VitaMR.Services;

public interface IVaultIngestService
{
    string VaultRoot { get; set; }

    IReadOnlyList<IngestResult> IngestRawFiles(ChartContext chartContext, IEnumerable<string> sourcePaths);

    IngestResult IngestRawText(ChartContext chartContext, string sourceText, string displayName);
}
