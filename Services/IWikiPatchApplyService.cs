using VitaMR.Models;

namespace VitaMR.Services;

public interface IWikiPatchApplyService
{
    WikiPatchApplyResult Apply(
        string vaultRoot,
        ChartContext chartContext,
        GeminiWikiPatchResult patch,
        IngestResult rawManualSource);
}
