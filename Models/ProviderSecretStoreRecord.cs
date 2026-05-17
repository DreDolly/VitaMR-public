namespace VitaMR.Models;

public sealed class ProviderSecretStoreRecord
{
    public Dictionary<string, ProviderSecretRecord> Providers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class ProviderSecretRecord
{
    public string ApiKeyProtected { get; set; } = string.Empty;

    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
