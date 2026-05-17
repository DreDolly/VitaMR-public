namespace VitaMR.Models;

public sealed class GeminiWikiRewriteResult
{
    public bool WasAttempted { get; set; }

    public bool WasAvailable { get; set; }

    public string Status { get; set; } = "GEMINI_WIKI_REWRITE_NOT_RUN";

    public string Summary { get; set; } = string.Empty;

    public List<WikiFileRewrite> Files { get; set; } = [];

    public string RawResponse { get; set; } = string.Empty;
}
