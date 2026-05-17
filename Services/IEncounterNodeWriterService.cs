using VitaMR.Models;

namespace VitaMR.Services;

public interface IEncounterNodeWriterService
{
    EncounterNodeWriteResult WriteEncounterNode(
        string vaultRoot,
        ChartContext chartContext,
        EncounterExtractionResult extraction);
}
