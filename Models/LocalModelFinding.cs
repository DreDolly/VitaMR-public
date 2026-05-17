namespace VitaMR.Models;

public sealed class LocalModelFinding
{
    public string Type { get; set; } = "other";

    public string Text { get; set; } = string.Empty;

    public string SuggestedReplacement { get; set; } = "[TOKEN]";
}
