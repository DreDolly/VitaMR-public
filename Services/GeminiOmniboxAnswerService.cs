using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using VitaMR.Models;

namespace VitaMR.Services;

public sealed class GeminiOmniboxAnswerService : IGeminiOmniboxAnswerService
{
    private const string PromptFileName = "Gemini_Omnibox_Answer.md";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly HttpClient _httpClient;
    private readonly IGeminiApiKeyStore _apiKeyStore;
    private readonly IPromptCacheService _promptCacheService;

    public GeminiOmniboxAnswerService(
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

    public async Task<GeminiOmniboxAnswerResult> AnswerAsync(
        bool isEnabled,
        string modelName,
        string scrubbedUserText,
        string conversationContext,
        VaultContextPacket contextPacket,
        CancellationToken cancellationToken = default)
    {
        var result = new GeminiOmniboxAnswerResult
        {
            WasAttempted = true,
            Status = "GEMINI_OMNIBOX_STARTED"
        };

        if (!isEnabled)
        {
            result.Status = "GEMINI_OMNIBOX_DISABLED";
            result.Answer = "The backend API is disabled, so I cannot complete that chart request yet.";
            return result;
        }

        var apiKey = _apiKeyStore.Load().ThinkingApiKey;

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            result.Status = "GEMINI_OMNIBOX_KEY_MISSING";
            result.Answer = "The backend API key is not configured, so I cannot complete that chart request yet.";
            return result;
        }

        var requestBody = BuildRequestBody(scrubbedUserText, conversationContext, contextPacket);

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
                result.Status = $"GEMINI_OMNIBOX_HTTP_{(int)response.StatusCode}";
                result.Answer = $"The backend API could not complete that chart request. Status: {result.Status}.";
                return result;
            }

            result.Answer = ExtractText(body).Trim();
            result.WasAvailable = !string.IsNullOrWhiteSpace(result.Answer);
            result.Status = result.WasAvailable
                ? "GEMINI_OMNIBOX_ANSWER_READY"
                : "GEMINI_OMNIBOX_EMPTY";

            if (string.IsNullOrWhiteSpace(result.Answer))
            {
                result.Answer = "The backend API returned no usable answer for that chart request.";
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or UriFormatException or JsonException)
        {
            result.Status = exception is TaskCanceledException
                ? "GEMINI_OMNIBOX_TIMEOUT"
                : "GEMINI_OMNIBOX_UNAVAILABLE";
            result.RawResponse = exception.Message;
            result.Answer = $"The backend API was unavailable for that chart request. Status: {result.Status}.";
        }

        return result;
    }

    public async Task<GeminiOmniboxAnswerResult> AnswerStreamingAsync(
        bool isEnabled,
        string modelName,
        string scrubbedUserText,
        string conversationContext,
        VaultContextPacket contextPacket,
        Action<string> onTextDelta,
        CancellationToken cancellationToken = default)
    {
        var result = new GeminiOmniboxAnswerResult
        {
            WasAttempted = true,
            Status = "GEMINI_OMNIBOX_STREAM_STARTED"
        };

        if (!isEnabled)
        {
            result.Status = "GEMINI_OMNIBOX_DISABLED";
            result.Answer = "The backend API is disabled, so I cannot complete that chart request yet.";
            return result;
        }

        var apiKey = _apiKeyStore.Load().ThinkingApiKey;

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            result.Status = "GEMINI_OMNIBOX_KEY_MISSING";
            result.Answer = "The backend API key is not configured, so I cannot complete that chart request yet.";
            return result;
        }

        var requestBody = BuildRequestBody(scrubbedUserText, conversationContext, contextPacket);
        var answer = new StringBuilder();
        var raw = new StringBuilder();

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, BuildStreamGenerateUri(modelName, apiKey))
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(requestBody, JsonOptions),
                    Encoding.UTF8,
                    "application/json")
            };

            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                result.RawResponse = body;
                result.Status = $"GEMINI_OMNIBOX_HTTP_{(int)response.StatusCode}";
                result.Answer = $"The backend API could not complete that chart request. Status: {result.Status}.";
                return result;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream);

            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(cancellationToken);
                if (line is null)
                {
                    break;
                }

                if (string.IsNullOrWhiteSpace(line) ||
                    !line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var data = line["data:".Length..].Trim();
                if (data.Equals("[DONE]", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                raw.AppendLine(data);
                var delta = ExtractText(data);

                if (string.IsNullOrWhiteSpace(delta))
                {
                    continue;
                }

                answer.Append(delta);
                onTextDelta(delta);
            }

            result.RawResponse = raw.ToString();
            result.Answer = answer.ToString().Trim();
            result.WasAvailable = !string.IsNullOrWhiteSpace(result.Answer);
            result.Status = result.WasAvailable
                ? "GEMINI_OMNIBOX_STREAM_READY"
                : "GEMINI_OMNIBOX_EMPTY";

            if (string.IsNullOrWhiteSpace(result.Answer))
            {
                result.Answer = "The backend API returned no usable answer for that chart request.";
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or UriFormatException or JsonException or IOException)
        {
            result.Status = exception is TaskCanceledException
                ? "GEMINI_OMNIBOX_TIMEOUT"
                : "GEMINI_OMNIBOX_UNAVAILABLE";
            result.RawResponse = exception.Message;
            result.Answer = $"The backend API was unavailable for that chart request. Status: {result.Status}.";
        }

        return result;
    }

    private object BuildRequestBody(
        string scrubbedUserText,
        string conversationContext,
        VaultContextPacket contextPacket)
    {
        return new
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
                temperature = 0.1
            }
        };
    }

    private string BuildPrompt(
        string scrubbedUserText,
        string conversationContext,
        VaultContextPacket contextPacket)
    {
        var instructions = _promptCacheService.GetPrompt(
            PromptFileName,
            "Answer the scrubbed VitaMR omnibox request using only the supplied sterile context. Return concise plain text.");

        return
            $"{instructions}\n\n" +
            $"Context status: {contextPacket.Status}\n" +
            $"Resolved sterile chart id: {contextPacket.ChartId}\n" +
            $"Resolved local display label: {contextPacket.PatientDisplayName}\n\n" +
            $"Known family-vault patients:\n{FormatLines(contextPacket.KnownPatients)}\n\n" +
            $"Available sources:\n{FormatLines(contextPacket.AvailableSources)}\n\n" +
            $"Included sterile files:\n{FormatLines(contextPacket.IncludedFiles.Select(Path.GetFileName).Where(name => !string.IsNullOrWhiteSpace(name))!)}\n\n" +
            $"Missing or pending data:\n{FormatLines(contextPacket.PendingItems.Concat(contextPacket.MissingOrUnavailableData))}\n\n" +
            $"Retrieval warnings:\n{FormatLines(contextPacket.RetrievalWarnings)}\n\n" +
            $"Recent scrubbed conversation:\n{conversationContext}\n\n" +
            $"Sterile context packet:\n{contextPacket.ContextText}\n\n" +
            $"Scrubbed user request:\n{scrubbedUserText}\n\n" +
            "Return the answer for Dolly to present. Use warm, modern plain text with short paragraphs. Do not use Markdown bullets, asterisks, tables, legal-style headings, or raw ledger formatting. Do not add clinical advice. If the supplied context is insufficient, say exactly what is missing.";
    }

    private static string FormatLines(IEnumerable<string> lines)
    {
        var lineList = lines
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Take(40)
            .ToList();

        return lineList.Count == 0
            ? "none"
            : string.Join("\n", lineList.Select(line => $"- {line}"));
    }

    private static Uri BuildGenerateUri(string modelName, string apiKey)
    {
        var safeModelName = string.IsNullOrWhiteSpace(modelName)
            ? "gemini-3-flash-preview"
            : modelName.Trim();

        return new Uri($"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(safeModelName)}:generateContent?key={Uri.EscapeDataString(apiKey)}");
    }

    private static Uri BuildStreamGenerateUri(string modelName, string apiKey)
    {
        var safeModelName = string.IsNullOrWhiteSpace(modelName)
            ? "gemini-3-flash-preview"
            : modelName.Trim();

        return new Uri($"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(safeModelName)}:streamGenerateContent?key={Uri.EscapeDataString(apiKey)}&alt=sse");
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
