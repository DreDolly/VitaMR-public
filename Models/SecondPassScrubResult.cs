namespace VitaMR.Models;

public sealed class SecondPassScrubResult
{
    public SecondPassScrubResult(
        string sourcePath,
        string displayName,
        string status,
        string finalScrubbedPath,
        int replacementCount,
        string preview)
    {
        SourcePath = sourcePath;
        DisplayName = displayName;
        Status = status;
        FinalScrubbedPath = finalScrubbedPath;
        ReplacementCount = replacementCount;
        Preview = preview;
    }

    public string SourcePath { get; }

    public string DisplayName { get; }

    public string Status { get; }

    public string FinalScrubbedPath { get; }

    public int ReplacementCount { get; }

    public string Preview { get; }
}
