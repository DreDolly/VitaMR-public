namespace VitaMR.Models;

public sealed class GeminiOmniboxAnswerResult
{
    public bool WasAttempted { get; set; }

    public bool WasAvailable { get; set; }

    public string Status { get; set; } = "GEMINI_OMNIBOX_NOT_RUN";

    public string Answer { get; set; } = string.Empty;

    public string RawResponse { get; set; } = string.Empty;
}
