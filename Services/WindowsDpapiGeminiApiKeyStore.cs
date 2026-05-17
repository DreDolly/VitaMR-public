using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using VitaMR.Models;

namespace VitaMR.Services;

public sealed class WindowsDpapiGeminiApiKeyStore : IGeminiApiKeyStore, IProviderApiKeyStore
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("VitaMR.GeminiSecrets.v1");
    private const string GeminiProviderId = "gemini";

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public WindowsDpapiGeminiApiKeyStore()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VitaMR",
            "Secrets");

        StorePath = Path.Combine(folder, "Provider_Secrets.v1.json");
        GeminiStorePath = Path.Combine(folder, "Gemini_Secrets.v1.json");
    }

    public string StorePath { get; }

    private string GeminiStorePath { get; }

    public bool HasConfiguredKeys()
    {
        var keys = Load();
        return !string.IsNullOrWhiteSpace(keys.FastApiKey) &&
               !string.IsNullOrWhiteSpace(keys.ThinkingApiKey);
    }

    public GeminiApiKeyBundle Load()
    {
        var providerKey = LoadProviderKey(GeminiProviderId).ApiKey;
        if (!string.IsNullOrWhiteSpace(providerKey))
        {
            return new GeminiApiKeyBundle
            {
                FastApiKey = providerKey,
                ThinkingApiKey = providerKey,
                UpdatedAt = LoadProviderKey(GeminiProviderId).UpdatedAt
            };
        }

        if (!File.Exists(GeminiStorePath))
        {
            return new GeminiApiKeyBundle();
        }

        try
        {
            var json = File.ReadAllText(GeminiStorePath);
            var record = JsonSerializer.Deserialize<GeminiSecretStoreRecord>(json, _jsonOptions) ?? new GeminiSecretStoreRecord();

            return new GeminiApiKeyBundle
            {
                FastApiKey = Unprotect(record.FastApiKeyProtected),
                ThinkingApiKey = Unprotect(record.ThinkingApiKeyProtected),
                UpdatedAt = record.UpdatedAt
            };
        }
        catch
        {
            return new GeminiApiKeyBundle();
        }
    }

    public void Save(GeminiApiKeyBundle keys)
    {
        SaveProviderKey(new ProviderApiKeyBundle
        {
            ProviderId = GeminiProviderId,
            ApiKey = string.IsNullOrWhiteSpace(keys.ThinkingApiKey) ? keys.FastApiKey : keys.ThinkingApiKey
        });

        var folder = Path.GetDirectoryName(GeminiStorePath);

        if (!string.IsNullOrWhiteSpace(folder))
        {
            Directory.CreateDirectory(folder);
        }

        var record = new GeminiSecretStoreRecord
        {
            FastApiKeyProtected = Protect(keys.FastApiKey),
            ThinkingApiKeyProtected = Protect(keys.ThinkingApiKey),
            UpdatedAt = DateTime.Now
        };

        File.WriteAllText(GeminiStorePath, JsonSerializer.Serialize(record, _jsonOptions));
    }

    public bool HasProviderKey(string providerId)
    {
        return !string.IsNullOrWhiteSpace(LoadProviderKey(providerId).ApiKey);
    }

    public ProviderApiKeyBundle LoadProviderKey(string providerId)
    {
        var normalizedProviderId = NormalizeProviderId(providerId);
        if (string.IsNullOrWhiteSpace(normalizedProviderId) || !File.Exists(StorePath))
        {
            return new ProviderApiKeyBundle { ProviderId = normalizedProviderId };
        }

        try
        {
            var json = File.ReadAllText(StorePath);
            var store = JsonSerializer.Deserialize<ProviderSecretStoreRecord>(json, _jsonOptions) ?? new ProviderSecretStoreRecord();
            if (!store.Providers.TryGetValue(normalizedProviderId, out var record))
            {
                return new ProviderApiKeyBundle { ProviderId = normalizedProviderId };
            }

            return new ProviderApiKeyBundle
            {
                ProviderId = normalizedProviderId,
                ApiKey = Unprotect(record.ApiKeyProtected),
                UpdatedAt = record.UpdatedAt
            };
        }
        catch
        {
            return new ProviderApiKeyBundle { ProviderId = normalizedProviderId };
        }
    }

    public void SaveProviderKey(ProviderApiKeyBundle key)
    {
        var normalizedProviderId = NormalizeProviderId(key.ProviderId);
        if (string.IsNullOrWhiteSpace(normalizedProviderId) || string.IsNullOrWhiteSpace(key.ApiKey))
        {
            return;
        }

        var store = LoadProviderStore();
        store.Providers[normalizedProviderId] = new ProviderSecretRecord
        {
            ApiKeyProtected = Protect(key.ApiKey),
            UpdatedAt = DateTime.Now
        };

        SaveProviderStore(store);
    }

    public void ClearProviderKey(string providerId)
    {
        var normalizedProviderId = NormalizeProviderId(providerId);
        if (string.IsNullOrWhiteSpace(normalizedProviderId))
        {
            return;
        }

        var store = LoadProviderStore();
        if (store.Providers.Remove(normalizedProviderId))
        {
            SaveProviderStore(store);
        }
    }

    private ProviderSecretStoreRecord LoadProviderStore()
    {
        if (!File.Exists(StorePath))
        {
            return new ProviderSecretStoreRecord();
        }

        try
        {
            var json = File.ReadAllText(StorePath);
            return JsonSerializer.Deserialize<ProviderSecretStoreRecord>(json, _jsonOptions) ?? new ProviderSecretStoreRecord();
        }
        catch
        {
            return new ProviderSecretStoreRecord();
        }
    }

    private void SaveProviderStore(ProviderSecretStoreRecord store)
    {
        var folder = Path.GetDirectoryName(StorePath);
        if (!string.IsNullOrWhiteSpace(folder))
        {
            Directory.CreateDirectory(folder);
        }

        File.WriteAllText(StorePath, JsonSerializer.Serialize(store, _jsonOptions));
    }

    private static string NormalizeProviderId(string providerId)
    {
        return providerId.Trim().ToLowerInvariant() switch
        {
            "google" or "gemini" => "gemini",
            "openai" => "openai",
            "anthropic" or "claude" => "anthropic",
            "xai" or "x.ai" or "grok" => "xai",
            "local" or "local model" => "local",
            var value => value
        };
    }

    private static string Protect(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var protectedBytes = ProtectBytes(Encoding.UTF8.GetBytes(value.Trim()), Entropy);
        return Convert.ToBase64String(protectedBytes);
    }

    private static string Unprotect(string protectedValue)
    {
        if (string.IsNullOrWhiteSpace(protectedValue))
        {
            return string.Empty;
        }

        var bytes = Convert.FromBase64String(protectedValue);
        return Encoding.UTF8.GetString(UnprotectBytes(bytes, Entropy));
    }

    private static byte[] ProtectBytes(byte[] data, byte[] entropy)
    {
        return CryptData(data, entropy, protect: true);
    }

    private static byte[] UnprotectBytes(byte[] data, byte[] entropy)
    {
        return CryptData(data, entropy, protect: false);
    }

    private static byte[] CryptData(byte[] data, byte[] entropy, bool protect)
    {
        using var dataBlob = DataBlob.FromBytes(data);
        using var entropyBlob = DataBlob.FromBytes(entropy);
        var outputBlob = new NativeDataBlob();

        var success = protect
            ? CryptProtectData(ref dataBlob.Blob, null, ref entropyBlob.Blob, IntPtr.Zero, IntPtr.Zero, 0, ref outputBlob)
            : CryptUnprotectData(ref dataBlob.Blob, null, ref entropyBlob.Blob, IntPtr.Zero, IntPtr.Zero, 0, ref outputBlob);

        if (!success)
        {
            throw new InvalidOperationException("Windows could not protect or unprotect the Gemini API key store.");
        }

        try
        {
            var output = new byte[outputBlob.Count];
            Marshal.Copy(outputBlob.Data, output, 0, outputBlob.Count);
            return output;
        }
        finally
        {
            LocalFree(outputBlob.Data);
        }
    }

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptProtectData(
        ref NativeDataBlob dataIn,
        string? dataDescription,
        ref NativeDataBlob optionalEntropy,
        IntPtr reserved,
        IntPtr promptStruct,
        int flags,
        ref NativeDataBlob dataOut);

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptUnprotectData(
        ref NativeDataBlob dataIn,
        string? dataDescription,
        ref NativeDataBlob optionalEntropy,
        IntPtr reserved,
        IntPtr promptStruct,
        int flags,
        ref NativeDataBlob dataOut);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr handle);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeDataBlob
    {
        public int Count;
        public IntPtr Data;
    }

    private sealed class DataBlob : IDisposable
    {
        private DataBlob(byte[] bytes)
        {
            Blob = new NativeDataBlob
            {
                Count = bytes.Length,
                Data = Marshal.AllocHGlobal(bytes.Length)
            };

            Marshal.Copy(bytes, 0, Blob.Data, bytes.Length);
        }

        public NativeDataBlob Blob;

        public static DataBlob FromBytes(byte[] bytes)
        {
            return new DataBlob(bytes);
        }

        public void Dispose()
        {
            if (Blob.Data != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(Blob.Data);
                Blob.Data = IntPtr.Zero;
            }
        }
    }
}
