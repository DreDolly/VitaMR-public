using VitaMR.Models;

namespace VitaMR.Services;

public interface IGeminiEncounterWikiRewriteService
{
    Task<WikiRewriteApplyResult> RewriteEncounterFilesAsync(
        bool isEnabled,
        string modelName,
        string vaultRoot,
        ChartContext chartContext,
        EncounterExtractionResult extraction,
        EncounterNodeWriteResult writeResult,
        CancellationToken cancellationToken = default);
}
