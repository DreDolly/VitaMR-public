using VitaMR.Models;

namespace VitaMR.Services;

public interface IEncounterDraftService
{
    EncounterDraft CreateMockDraft(
        ChartContext chart,
        IReadOnlyList<IngestResult> ingestResults,
        IReadOnlyList<ScrubResult> scrubResults,
        string userPrompt);

    DraftValidationResult Validate(EncounterDraft draft);
}
