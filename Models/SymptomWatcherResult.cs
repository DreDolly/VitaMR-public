namespace VitaMR.Models;

public sealed class SymptomWatcherResult
{
    public bool WasRun { get; set; }

    public bool WasCaptured { get; set; }

    public string ChartId { get; set; } = string.Empty;

    public string Status { get; set; } = "SYMPTOM_WATCHER_NOT_RUN";

    public string RawEntryPath { get; set; } = string.Empty;

    public string JournalPath { get; set; } = string.Empty;

    public List<SymptomMention> Mentions { get; } = [];

    public List<string> Messages { get; } = [];
}

public sealed class SymptomMention
{
    public string Quote { get; set; } = string.Empty;

    public string ExtractedSymptom { get; set; } = string.Empty;

    public string BodySystem { get; set; } = "General";

    public string SeverityReported { get; set; } = "not stated";

    public string ActivityContext { get; set; } = "not stated";

    public string Duration { get; set; } = "not stated";
}
