using VitaMR.Models;

namespace VitaMR.Services;

public interface IGeminiWikiRewriteService
{
    Task<WikiRewriteApplyResult> RewriteAndApplyAsync(
        bool isEnabled,
        string modelName,
        string vaultRoot,
        ChartContext chartContext,
        GeminiWikiPatchResult patch,
        IngestResult rawManualSource,
        string scrubbedUserText,
        string conversationContext,
        CancellationToken cancellationToken = default);
}
