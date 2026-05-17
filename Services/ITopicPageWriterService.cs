using VitaMR.Models;

namespace VitaMR.Services;

public interface ITopicPageWriterService
{
    IReadOnlyList<string> WriteTopicPages(
        string vaultRoot,
        ChartContext chartContext,
        EncounterExtractionResult extraction,
        string encounterNodePath);
}
