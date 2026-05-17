using VitaMR.Models;

namespace VitaMR.Services;

public interface ILocalModelReviewService
{
    Task<LocalModelReviewResult> ReviewScrubbedPayloadAsync(
        string endpoint,
        string modelName,
        IReadOnlyList<ScrubResult> scrubResults,
        CancellationToken cancellationToken = default);
}
