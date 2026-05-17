using VitaMR.Models;

namespace VitaMR.Services;

public interface IGeminiBackendPipelineService
{
    Task<GeminiBackendPipelineResult> ProcessScrubbedPayloadsAsync(
        bool isEnabled,
        string fastModelName,
        string thinkingModelName,
        string vaultRoot,
        ChartContext chartContext,
        IReadOnlyList<SecondPassScrubResult> scrubbedPayloads,
        string sourceSystem,
        string sourceFacility,
        string sourceType,
        Func<string, string, string, Task>? progressCallback = null,
        CancellationToken cancellationToken = default);
}
