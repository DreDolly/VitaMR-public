namespace VitaMR.Models;

public sealed class GeminiBackendPipelineResult
{
    public bool WasAttempted { get; set; }

    public bool WasEnabled { get; set; }

    public bool WroteAnyEncounterNodes { get; set; }

    public List<GeminiBackendPipelineStep> Steps { get; set; } = [];

    public List<EncounterNodeWriteResult> WriteResults { get; set; } = [];

    public List<string> ApiSubmittedSourcePaths { get; set; } = [];

    public List<string> WikiWrittenSourcePaths { get; set; } = [];

    public List<string> DuplicateSkippedSourcePaths { get; set; } = [];

    public List<IngestSummaryItem> SummaryItems { get; set; } = [];

    public List<string> Messages { get; set; } = [];
}
