namespace VitaMR.Models;

public sealed class PatternLinkerResult
{
    public bool WasRun { get; set; }

    public string Status { get; set; } = "PATTERN_LINKER_NOT_RUN";

    public int ChartsScanned { get; set; }

    public int SymptomRowsScanned { get; set; }

    public int PatternsWritten { get; set; }

    public int DuplicatePatternsSkipped { get; set; }

    public int CareGapsWritten { get; set; }

    public string ScheduleLogPath { get; set; } = string.Empty;

    public List<string> Messages { get; } = [];
}
