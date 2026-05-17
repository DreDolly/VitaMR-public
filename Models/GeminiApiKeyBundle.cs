namespace VitaMR.Models;

public sealed class GeminiApiKeyBundle
{
    public string FastApiKey { get; set; } = string.Empty;

    public string ThinkingApiKey { get; set; } = string.Empty;

    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
