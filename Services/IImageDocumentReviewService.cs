using VitaMR.Models;

namespace VitaMR.Services;

public interface IImageDocumentReviewService
{
    Task<IReadOnlyList<ImageDocumentReviewResult>> ReviewImageDocumentsAsync(
        string vaultRoot,
        ChartContext chartContext,
        string endpoint,
        string modelName,
        IReadOnlyList<IngestResult> ingestResults,
        CancellationToken cancellationToken = default);
}
