namespace VitaMR.Models;

public sealed class WikiFactUpdate
{
    public string Topic { get; set; } = string.Empty;

    public string Category { get; set; } = "general";

    public string NewValue { get; set; } = string.Empty;

    public string Unit { get; set; } = string.Empty;

    public string Date { get; set; } = string.Empty;

    public string Status { get; set; } = "user_reported";

    public string VerificationStatus { get; set; } = "user_reported_unverified";

    public string Summary { get; set; } = string.Empty;

    public string ReplacesPendingText { get; set; } = string.Empty;
}
