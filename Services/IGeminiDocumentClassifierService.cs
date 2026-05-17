using VitaMR.Models;

namespace VitaMR.Services;

public interface IGeminiDocumentClassifierService
{
    Task<ScrubbedDocumentClassificationResult?> ClassifyAsync(
        string modelName,
        ChartContext chartContext,
        SecondPassScrubResult scrubbedPayload,
        CancellationToken cancellationToken = default);
}
