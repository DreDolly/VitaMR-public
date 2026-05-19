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

    public async Task<string> AnswerPersonalAsync(
        bool isEnabled,
        string modelName,
        string scrubbedUserText,
        string topicHint,
        string captureType,
        CancellationToken cancellationToken = default)
    {
        if (!isEnabled)
        {
            return "Saved to Personal Mode. Gemini personal chat is not configured yet.";
        }

        var apiKey = _apiKeyStore.Load().FastApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return "Saved to Personal Mode. Add a Gemini key on desktop to enable Personal Mode replies.";
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
                        new { text = BuildPersonalPrompt(scrubbedUserText, topicHint, captureType) }
                    }
                }
            },
            generationConfig = new
            {
                temperature = 0.45,
                maxOutputTokens = 120
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
                return "Saved to Personal Mode. Gemini did not answer, so I kept the note without a cloud reply.";
            }

            var answer = ExtractText(body).Trim();
            return string.IsNullOrWhiteSpace(answer)
                ? "Saved to Personal Mode. I kept the note, but Gemini did not return a useful reply."
                : answer;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            return "Saved to Personal Mode. Gemini was not reachable, so I kept the note without a cloud reply.";
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

    private static string BuildPersonalPrompt(string scrubbedUserText, string topicHint, string captureType)
    {
        return
            "You are Dolly in VitaMR Personal Mode.\n" +
            "This is non-medical personal continuity only. Do not inspect, summarize, or update any medical chart. Do not give medical, legal, financial, password, identity, or deeply private advice.\n" +
            "Assume the user's note has already been saved locally. Reply in one or two short, practical sentences. If the note seems sensitive, tell the user to use Local Lockbox.\n" +
            "Do not mention APIs, routes, files, prompts, or internal storage.\n\n" +
            $"Topic hint: {topicHint}\n" +
            $"Capture type: {captureType}\n" +
            $"User note:\n{scrubbedUserText}";
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
