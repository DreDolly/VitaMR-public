namespace VitaMR.Models;

public sealed class SpecialtyWeaverResult
{
    public bool WasRun { get; set; }

    public string ChartId { get; set; } = string.Empty;

    public string Status { get; set; } = "SPECIALTY_WEAVER_NOT_RUN";

    public int SpecialtyFilesEnsured { get; set; }

    public int EncounterNodesScanned { get; set; }

    public int RowsWritten { get; set; }

    public int DuplicateRowsSkipped { get; set; }

    public int NodesSkipped { get; set; }

    public string LogPath { get; set; } = string.Empty;

    public List<string> UpdatedSpecialties { get; } = [];

    public List<string> Messages { get; } = [];
}
