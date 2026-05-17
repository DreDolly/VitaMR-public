namespace VitaMR.Models;

public sealed class OriginalSourceRetrievalResult
{
    public bool WasFound { get; set; }

    public string Status { get; set; } = "ORIGINAL_SOURCE_NOT_RUN";

    public List<string> SourcePaths { get; } = [];

    public List<string> Messages { get; } = [];
}
