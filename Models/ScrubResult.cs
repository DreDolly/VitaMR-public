namespace VitaMR.Models;

public sealed class ScrubResult
{
    public ScrubResult(
        string sourcePath,
        string displayName,
        string status,
        string preview,
        IReadOnlyList<string> findings,
        string fullText = "")
    {
        SourcePath = sourcePath;
        DisplayName = displayName;
        Status = status;
        Preview = preview;
        Findings = findings;
        FullText = fullText;
    }

    public string SourcePath { get; }

    public string DisplayName { get; }

    public string Status { get; }

    public string Preview { get; }

    public IReadOnlyList<string> Findings { get; }

    public string FullText { get; }
}
