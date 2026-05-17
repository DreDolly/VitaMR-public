namespace VitaMR.Models;

public sealed class GeminiWikiRewriteVerificationResult
{
    public bool WasAttempted { get; set; }

    public bool WasAvailable { get; set; }

    public bool Approved { get; set; }

    public string Status { get; set; } = "GEMINI_WIKI_REWRITE_VERIFY_NOT_RUN";

    public string Reason { get; set; } = string.Empty;

    public List<string> Issues { get; set; } = [];

    public string RawResponse { get; set; } = string.Empty;
}
