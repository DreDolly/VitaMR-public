using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using VitaMR.Models;

namespace VitaMR.Services;

public sealed class GeminiEncounterExtractionService : IGeminiEncounterExtractionService
{
    private const string PromptFileName = "Gemini_Encounter_Extraction.md";

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

    public GeminiEncounterExtractionService(
        IGeminiApiKeyStore apiKeyStore,
        IPromptCacheService promptCacheService)
    {
        _apiKeyStore = apiKeyStore;
        _promptCacheService = promptCacheService;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(120)
        };
    }

    public async Task<EncounterExtractionResult?> ExtractEncounterAsync(
        string modelRole,
        string modelName,
        ChartContext chartContext,
        SecondPassScrubResult scrubbedPayload,
        string sourceSystem,
        string sourceFacility,
        string sourceType,
        CancellationToken cancellationToken = default)
    {
        var keys = _apiKeyStore.Load();
        var apiKey = modelRole.Equals("thinking", StringComparison.OrdinalIgnoreCase)
            ? keys.ThinkingApiKey
            : keys.FastApiKey;

        if (string.IsNullOrWhiteSpace(apiKey) ||
            string.IsNullOrWhiteSpace(scrubbedPayload.FinalScrubbedPath) ||
            !File.Exists(scrubbedPayload.FinalScrubbedPath))
        {
            return null;
        }

        var prompt = BuildPrompt(
            chartContext,
            scrubbedPayload,
            await File.ReadAllTextAsync(scrubbedPayload.FinalScrubbedPath, cancellationToken),
            sourceSystem,
            sourceFacility,
            sourceType);
        var requestBody = new
        {
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[]
                    {
                        new { text = prompt }
                    }
                }
            },
            generationConfig = new
            {
                temperature = modelRole.Equals("thinking", StringComparison.OrdinalIgnoreCase) ? 0.15 : 0.05,
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

            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            var result = JsonSerializer.Deserialize<EncounterExtractionResult>(json, ResponseJsonOptions);

            if (result is not null)
            {
                result.ChartId = chartContext.ChartId;
                result.SourceFileId = scrubbedPayload.DisplayName;
            }

            return result;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private string BuildPrompt(
        ChartContext chartContext,
        SecondPassScrubResult scrubbedPayload,
        string scrubbedText,
        string sourceSystem,
        string sourceFacility,
        string sourceType)
    {
        var instructions = _promptCacheService.GetPrompt(
            PromptFileName,
            "Extract a sterile encounter JSON object from the provided scrubbed medical payload. Return JSON only.");

        return
            $"{instructions}\n\n" +
            $"Chart ID: {chartContext.ChartId}\n" +
            $"Source file id: {scrubbedPayload.DisplayName}\n" +
            $"Source system: {NormalizeUnknown(sourceSystem)}\n" +
            $"Source facility: {NormalizeUnknown(sourceFacility)}\n" +
            $"Source type: {NormalizeUnknown(sourceType)}\n\n" +
            "Final scrubbed payload:\n" +
            scrubbedText;
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

    private static string NormalizeUnknown(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "Unknown" : value.Trim();
    }
}
