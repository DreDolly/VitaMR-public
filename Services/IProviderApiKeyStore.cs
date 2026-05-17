using VitaMR.Models;

namespace VitaMR.Services;

public interface IProviderApiKeyStore
{
    string StorePath { get; }

    bool HasProviderKey(string providerId);

    ProviderApiKeyBundle LoadProviderKey(string providerId);

    void SaveProviderKey(ProviderApiKeyBundle key);

    void ClearProviderKey(string providerId);
}
