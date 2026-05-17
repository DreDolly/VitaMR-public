namespace VitaMR.Models;

public sealed class ProviderApiKeyBundle
{
    public string ProviderId { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
