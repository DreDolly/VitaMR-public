namespace VitaMR.Models;

public sealed class WikiRewriteApplyResult
{
    public bool WasAttempted { get; set; }

    public bool WasApplied { get; set; }

    public bool VerificationPassed { get; set; }

    public string Status { get; set; } = "WIKI_REWRITE_NOT_RUN";

    public string Summary { get; set; } = string.Empty;

    public List<string> UpdatedFiles { get; set; } = [];

    public List<string> Messages { get; set; } = [];
}
