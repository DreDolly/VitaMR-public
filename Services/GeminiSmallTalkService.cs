using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace VitaMR.Services;

public sealed class GeminiSmallTalkService : IGeminiSmallTalkService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(25)
    };
    private readonly IGeminiApiKeyStore _apiKeyStore;

    public GeminiSmallTalkService(IGeminiApiKeyStore apiKeyStore)
    {
        _apiKeyStore = apiKeyStore;
    }

    public async Task<string> AnswerAsync(
        bool isEnabled,
        string modelName,
        string scrubbedUserText,
        string scrubbedConversationContext,
        CancellationToken cancellationToken = default)
    {
        if (!isEnabled)
        {
            return "Good day. I am here, the vault is idle, and I am ready when you are.";
        }

        var apiKey = _apiKeyStore.Load().FastApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return "Good day. I am here, the vault is idle, and I am ready when you are.";
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
                        new { text = BuildPrompt(scrubbedUserText, scrubbedConversationContext) }
                    }
                }
            },
            generationConfig = new
            {
                temperature = 0.4,
                maxOutputTokens = 80
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

            if (!response.IsSuccessStatusCode)
            {
                return "Good day. I am here, the vault is idle, and I am ready when you are.";
            }

            var answer = ExtractText(body).Trim();
            return string.IsNullOrWhiteSpace(answer)
                ? "Good day. I am here, the vault is idle, and I am ready when you are."
                : answer;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            return "Good day. I am here, the vault is idle, and I am ready when you are.";
        }
    }

    private static string BuildPrompt(string scrubbedUserText, string scrubbedConversationContext)
    {
        return
            "You are Dolly, VitaMR's warm front-desk companion.\n" +
            "This is casual conversation only. Do not inspect, summarize, or update any chart. Do not give medical advice.\n" +
            "Reply naturally in one short sentence. Keep internal routing, APIs, privacy gates, and files out of the answer.\n\n" +
            $"Recent scrubbed conversation:\n{scrubbedConversationContext}\n\n" +
            $"Scrubbed user message:\n{scrubbedUserText}";
    }

    private static Uri BuildGenerateUri(string modelName, string apiKey)
    {
        var safeModelName = string.IsNullOrWhiteSpace(modelName)
            ? "gemini-2.5-flash-lite"
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
