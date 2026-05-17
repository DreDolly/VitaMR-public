using VitaMR.Models;

namespace VitaMR.Services;

public interface IGeminiApiKeyStore
{
    string StorePath { get; }

    bool HasConfiguredKeys();

    GeminiApiKeyBundle Load();

    void Save(GeminiApiKeyBundle keys);
}
