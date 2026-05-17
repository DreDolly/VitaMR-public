namespace VitaMR.Models;

public sealed class GeminiSecretStoreRecord
{
    public string FastModelName { get; set; } = "gemini-2.5-flash-lite";

    public string ThinkingModelName { get; set; } = "gemini-3-flash-preview";

    public string FastApiKeyProtected { get; set; } = string.Empty;

    public string ThinkingApiKeyProtected { get; set; } = string.Empty;

    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
