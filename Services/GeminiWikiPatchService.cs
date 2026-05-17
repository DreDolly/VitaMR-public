using System.Net.Http;
using System.Text;
using System.Text.Json;
using VitaMR.Models;

namespace VitaMR.Services;

public sealed class GeminiWikiPatchService : IGeminiWikiPatchService
{
    private const string PromptFileName = "Gemini_Wiki_Patch.md";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly JsonSerializerOptions ResponseJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new FlexibleStringListJsonConverter() }
    };

    private readonly HttpClient _httpClient;
    private readonly IGeminiApiKeyStore _apiKeyStore;
    private readonly IPromptCacheService _promptCacheService;

    public GeminiWikiPatchService(
        IGeminiApiKeyStore apiKeyStore,
        IPromptCacheService promptCacheService)
    {
        _apiKeyStore = apiKeyStore;
        _promptCacheService = promptCacheService;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(90)
        };
    }

    public async Task<GeminiWikiPatchResult> PlanPatchAsync(
        bool isEnabled,
        string modelName,
        string scrubbedUserText,
        string conversationContext,
        VaultContextPacket contextPacket,
        CancellationToken cancellationToken = default)
    {
        var result = new GeminiWikiPatchResult
        {
            WasAttempted = true,
            Status = "GEMINI_WIKI_PATCH_STARTED"
        };

        if (!isEnabled)
        {
            result.Status = "GEMINI_WIKI_PATCH_DISABLED";
            return result;
        }

        var apiKey = _apiKeyStore.Load().ThinkingApiKey;

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            result.Status = "GEMINI_WIKI_PATCH_KEY_MISSING";
            return result;
        }

        var requestBody = new
        {
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[]
                    {
                        new { text = BuildPrompt(scrubbedUserText, conversationContext, contextPacket) }
                    }
                }
            },
            generationConfig = new
            {
                temperature = 0.05,
                responseMimeType = "application/json"
            }
        };

        try
        {
            using var request = new StringContent(
                JsonSerializer.Serialize(requestBody, JsonOptions),
                Encoding.UTF8,
                "application/json");
            using var response = await _httpClient.PostAsync(BuildGenerateUri(modelName, apiKey), request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            result.RawResponse = body;

            if (!response.IsSuccessStatusCode)
            {
                result.Status = $"GEMINI_WIKI_PATCH_HTTP_{(int)response.StatusCode}";
                return result;
            }

            var json = ExtractText(body);

            if (string.IsNullOrWhiteSpace(json))
            {
                result.Status = "GEMINI_WIKI_PATCH_EMPTY";
                return result;
            }

            var parsed = JsonSerializer.Deserialize<GeminiWikiPatchResult>(json, ResponseJsonOptions);

            if (parsed is null)
            {
                result.Status = "GEMINI_WIKI_PATCH_INVALID_JSON";
                return result;
            }

            parsed.WasAttempted = true;
            parsed.WasAvailable = true;
            parsed.Status = string.IsNullOrWhiteSpace(parsed.Status)
                ? "GEMINI_WIKI_PATCH_READY"
                : parsed.Status;
            parsed.RawResponse = body;
            return parsed;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or UriFormatException or JsonException)
        {
            result.Status = exception is TaskCanceledException
                ? "GEMINI_WIKI_PATCH_TIMEOUT"
                : "GEMINI_WIKI_PATCH_UNAVAILABLE";
            result.RawResponse = exception.Message;
            return result;
        }
    }

    private string BuildPrompt(
        string scrubbedUserText,
        string conversationContext,
        VaultContextPacket contextPacket)
    {
        var instructions = _promptCacheService.GetPrompt(
            PromptFileName,
            "Decide whether this scrubbed VitaMR request should update the wiki. Return JSON only.");

        return
            $"{instructions}\n\n" +
            $"Context status: {contextPacket.Status}\n" +
            $"Chart id: {contextPacket.ChartId}\n" +
            $"Resolved display placeholder: [PATIENT]\n\n" +
            $"Recent scrubbed conversation:\n{conversationContext}\n\n" +
            $"Sterile wiki context:\n{contextPacket.ContextText}\n\n" +
            $"Scrubbed user request:\n{scrubbedUserText}\n\n" +
            "Return the patch plan JSON now.";
    }

    private static Uri BuildGenerateUri(string modelName, string apiKey)
    {
        var safeModelName = string.IsNullOrWhiteSpace(modelName)
            ? "gemini-3-flash-preview"
            : modelName.Trim();

        return new Uri($"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(safeModelName)}:generateContent?key={Uri.EscapeDataString(apiKey)}");
    }

    private static string ExtractText(string body)
    {
        using var document = JsonDocument.Parse(body);

        if (!document.RootElement.TryGetProperty("candidates", out var candidates) ||
            candidates.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        foreach (var candidate in candidates.EnumerateArray())
        {
            if (!candidate.TryGetProperty("content", out var content) ||
                !content.TryGetProperty("parts", out var parts) ||
                parts.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var part in parts.EnumerateArray())
            {
                if (part.TryGetProperty("text", out var text) &&
                    text.ValueKind == JsonValueKind.String)
                {
                    return text.GetString() ?? string.Empty;
                }
            }
        }

        return string.Empty;
    }
}
