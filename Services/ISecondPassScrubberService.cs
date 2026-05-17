using VitaMR.Models;

namespace VitaMR.Services;

public interface ISecondPassScrubberService
{
    IReadOnlyList<SecondPassScrubResult> SaveFinalScrubbedPayloads(
        string vaultRoot,
        ChartContext chartContext,
        IReadOnlyList<ScrubResult> scrubResults,
        LocalModelReviewResult localModelReview);
}
