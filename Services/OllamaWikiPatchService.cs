using System.Net.Http;
using System.Text;
using System.Text.Json;
using VitaMR.Models;

namespace VitaMR.Services;

public sealed class OllamaWikiPatchService : ILocalWikiPatchService
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
    private readonly IPromptCacheService _promptCacheService;

    public OllamaWikiPatchService(IPromptCacheService promptCacheService)
    {
        _promptCacheService = promptCacheService;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(90)
        };
    }

    public async Task<GeminiWikiPatchResult> PlanPatchAsync(
        string endpoint,
        string modelName,
        string scrubbedUserText,
        string conversationContext,
        VaultContextPacket contextPacket,
        CancellationToken cancellationToken = default)
    {
        var result = new GeminiWikiPatchResult
        {
            WasAttempted = true,
            Status = "LOCAL_GEMMA_WIKI_PATCH_STARTED"
        };

        var requestBody = new
        {
            model = modelName,
            prompt = BuildPrompt(scrubbedUserText, conversationContext, contextPacket),
            stream = false,
            format = "json",
            keep_alive = "30s",
            options = new
            {
                temperature = 0.05,
                num_predict = 900
            }
        };

        try
        {
            using var request = new StringContent(
                JsonSerializer.Serialize(requestBody, JsonOptions),
                Encoding.UTF8,
                "application/json");
            using var response = await _httpClient.PostAsync(LaptopWorkerProtocol.BuildInferenceUri(endpoint), request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            result.RawResponse = body;

            if (!response.IsSuccessStatusCode)
            {
                result.Status = $"LOCAL_GEMMA_WIKI_PATCH_HTTP_{(int)response.StatusCode}";
                return result;
            }

            var modelJson = LaptopWorkerProtocol.ExtractModelResponseOrThrow(body);

            if (string.IsNullOrWhiteSpace(modelJson))
            {
                result.Status = "LOCAL_GEMMA_WIKI_PATCH_RESPONSE_EMPTY";
                return result;
            }

            var parsed = JsonSerializer.Deserialize<GeminiWikiPatchResult>(modelJson, ResponseJsonOptions);

            if (parsed is null)
            {
                result.Status = "LOCAL_GEMMA_WIKI_PATCH_INVALID_JSON";
                return result;
            }

            parsed.WasAttempted = true;
            parsed.WasAvailable = true;
            parsed.Status = "LOCAL_GEMMA_WIKI_PATCH_READY";
            parsed.RawResponse = body;
            parsed.PatchSummary = string.IsNullOrWhiteSpace(parsed.PatchSummary)
                ? "Local Gemma fallback planned this wiki update because the backend API was unavailable."
                : $"Local Gemma fallback planned this update: {parsed.PatchSummary}";
            return parsed;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or UriFormatException or JsonException)
        {
            result.Status = exception is TaskCanceledException
                ? "LOCAL_GEMMA_WIKI_PATCH_TIMEOUT"
                : "LOCAL_GEMMA_WIKI_PATCH_UNAVAILABLE";
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
            "You are running as LOCAL GEMMA FALLBACK because the backend API is unavailable or rate-limited.\n" +
            "If you propose a patch, make the summary say it was planned by local Gemma fallback.\n\n" +
            $"Context status: {contextPacket.Status}\n" +
            $"Chart id: {contextPacket.ChartId}\n" +
            $"Resolved display placeholder: [PATIENT]\n\n" +
            $"Recent scrubbed conversation:\n{conversationContext}\n\n" +
            $"Sterile wiki context:\n{contextPacket.ContextText}\n\n" +
            $"Scrubbed user request:\n{scrubbedUserText}\n\n" +
            "Return the patch plan JSON now.";
    }
}
