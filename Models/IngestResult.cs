namespace VitaMR.Models;

public sealed class IngestResult
{
    public IngestResult(
        string sourcePath,
        string rawVaultPath,
        string displayName,
        DateTime ingestedAt,
        IReadOnlyList<string>? scaffoldCreatedPaths = null)
    {
        SourcePath = sourcePath;
        RawVaultPath = rawVaultPath;
        DisplayName = displayName;
        IngestedAt = ingestedAt;
        ScaffoldCreatedPaths = scaffoldCreatedPaths ?? [];
    }

    public string SourcePath { get; }

    public string RawVaultPath { get; }

    public string DisplayName { get; }

    public DateTime IngestedAt { get; }

    public IReadOnlyList<string> ScaffoldCreatedPaths { get; }
}
