using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using VitaMR.Models;

namespace VitaMR.Services;

public sealed class GeminiDocumentClassifierService : IGeminiDocumentClassifierService
{
    private const string PromptFileName = "Gemini_Document_Classify.md";

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

    public GeminiDocumentClassifierService(
        IGeminiApiKeyStore apiKeyStore,
        IPromptCacheService promptCacheService)
    {
        _apiKeyStore = apiKeyStore;
        _promptCacheService = promptCacheService;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(60)
        };
    }

    public async Task<ScrubbedDocumentClassificationResult?> ClassifyAsync(
        string modelName,
        ChartContext chartContext,
        SecondPassScrubResult scrubbedPayload,
        CancellationToken cancellationToken = default)
    {
        var apiKey = _apiKeyStore.Load().FastApiKey;

        if (string.IsNullOrWhiteSpace(apiKey) ||
            string.IsNullOrWhiteSpace(scrubbedPayload.FinalScrubbedPath) ||
            !File.Exists(scrubbedPayload.FinalScrubbedPath))
        {
            return null;
        }

        var scrubbedText = await File.ReadAllTextAsync(scrubbedPayload.FinalScrubbedPath, cancellationToken);
        var requestBody = new
        {
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[]
                    {
                        new { text = BuildPrompt(chartContext, scrubbedPayload, scrubbedText) }
                    }
                }
            },
            generationConfig = new
            {
                temperature = 0.05,
                responseMimeType = "application/json"
            }
        };

        using var request = new StringContent(
            JsonSerializer.Serialize(requestBody, JsonOptions),
            Encoding.UTF8,
            "application/json");
        using var response = await _httpClient.PostAsync(BuildGenerateUri(modelName, apiKey), request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        try
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var json = ExtractText(body);

            return string.IsNullOrWhiteSpace(json)
                ? null
                : JsonSerializer.Deserialize<ScrubbedDocumentClassificationResult>(json, ResponseJsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private string BuildPrompt(
        ChartContext chartContext,
        SecondPassScrubResult scrubbedPayload,
        string scrubbedText)
    {
        var instructions = _promptCacheService.GetPrompt(
            PromptFileName,
            "Classify this final scrubbed medical payload for VitaMR backend routing. Return JSON only.");

        return
            $"{instructions}\n\n" +
            $"Chart ID: {chartContext.ChartId}\n" +
            $"Source file id: {scrubbedPayload.DisplayName}\n\n" +
            "Final scrubbed payload:\n" +
            scrubbedText;
    }

    private static Uri BuildGenerateUri(string modelName, string apiKey)
    {
        var safeModelName = string.IsNullOrWhiteSpace(modelName)
            ? "gemini-3.1-flash-lite-preview"
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
