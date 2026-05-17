namespace VitaMR.Models;

public sealed class GeminiWikiPatchResult
{
    public bool WasAttempted { get; set; }

    public bool WasAvailable { get; set; }

    public bool ShouldApply { get; set; }

    public string Status { get; set; } = "GEMINI_WIKI_PATCH_NOT_RUN";

    public string PatchSummary { get; set; } = string.Empty;

    public string AnswerIfNoPatch { get; set; } = string.Empty;

    public List<WikiFactUpdate> Updates { get; set; } = [];

    public string RawResponse { get; set; } = string.Empty;
}
