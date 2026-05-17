using VitaMR.Models;

namespace VitaMR.Services;

public interface IGeminiEncounterExtractionService
{
    Task<EncounterExtractionResult?> ExtractEncounterAsync(
        string modelRole,
        string modelName,
        ChartContext chartContext,
        SecondPassScrubResult scrubbedPayload,
        string sourceSystem,
        string sourceFacility,
        string sourceType,
        CancellationToken cancellationToken = default);
}
