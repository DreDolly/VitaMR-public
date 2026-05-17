using System.Net.Http;
using System.IO;
using System.Text;
using System.Text.Json;
using VitaMR.Models;
using VitaMR.ViewModels;

namespace VitaMR.Services;

public sealed class OllamaOmniboxPreflightService : IOmniboxPreflightService
{
    private const string PromptFileName = "Local_Omnibox_Preflight.md";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly HttpClient _httpClient;
    private readonly IPromptCacheService _promptCacheService;

    public OllamaOmniboxPreflightService(IPromptCacheService promptCacheService)
    {
        _promptCacheService = promptCacheService;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(45)
        };
    }

    public async Task<OmniboxPreflightResult> ReviewAsync(
        string endpoint,
        string modelName,
        string userText,
        string activeDisplayName,
        IReadOnlyList<AttachmentViewModel> attachments,
        IReadOnlyList<PatientIdentityRecord> knownPatients,
        string conversationContext,
        CancellationToken cancellationToken = default)
    {
        var result = new OmniboxPreflightResult
        {
            WasAttempted = true,
            Status = "OMNIBOX_PREFLIGHT_STARTED"
        };

        try
        {
            var uri = LaptopWorkerProtocol.BuildInferenceUri(endpoint);
            var requestBody = new
            {
                model = modelName,
                prompt = BuildPrompt(userText, activeDisplayName, attachments, knownPatients, conversationContext),
                stream = false,
                format = "json",
                keep_alive = "30s",
                options = new
                {
                    temperature = 0.2,
                    num_predict = 250
                }
            };

            using var request = new StringContent(
                JsonSerializer.Serialize(requestBody, JsonOptions),
                Encoding.UTF8,
                "application/json");

            using var response = await _httpClient.PostAsync(uri, request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            result.RawResponse = body;

            if (!response.IsSuccessStatusCode)
            {
                result.Status = "OMNIBOX_PREFLIGHT_UNAVAILABLE";
                result.NeedsClarification = HasImageAttachment(attachments);
                result.ClarifyingQuestion = result.NeedsClarification
                    ? "Please provide the patient name for this image before I store it."
                    : string.Empty;
                return result;
            }

            result.IsAvailable = true;
            ApplyOllamaResponse(result, body);
            return result;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or UriFormatException or JsonException)
        {
            result.Status = "OMNIBOX_PREFLIGHT_UNAVAILABLE";
            result.RawResponse = exception.Message;
            result.NeedsClarification = HasImageAttachment(attachments);
            result.ClarifyingQuestion = result.NeedsClarification
                ? "Please provide the patient name for this image before I store it."
                : string.Empty;
            return result;
        }
    }

    private string BuildPrompt(
        string userText,
        string activeDisplayName,
        IReadOnlyList<AttachmentViewModel> attachments,
        IReadOnlyList<PatientIdentityRecord> knownPatients,
        string conversationContext)
    {
        var patientLines = knownPatients.Count == 0
            ? "none"
            : string.Join("\n", knownPatients.Select(patient => $"- {patient.PatientDisplayName} | chart_id: {patient.ChartId}"));
        var attachmentLines = attachments.Count == 0
            ? "none"
            : string.Join("\n", attachments.Select(attachment => $"- {attachment.DisplayName} ({attachment.Kind})"));

        return
            $"{LoadInstructions()}\n\n" +
            $"Current active local display name: {activeDisplayName}\n\n" +
            $"Known family-vault patients:\n{patientLines}\n\n" +
            $"Attachments:\n{attachmentLines}\n\n" +
            $"Recent conversation bubbles:\n{conversationContext}\n\n" +
            $"User message:\n{userText}\n\n" +
            "Review this omnibox input now. Return JSON only.";
    }

    private static void ApplyOllamaResponse(OmniboxPreflightResult result, string body)
    {
        string modelJson;

        try
        {
            modelJson = LaptopWorkerProtocol.ExtractModelResponseOrThrow(body);
        }
        catch
        {
            result.Status = "OMNIBOX_PREFLIGHT_RESPONSE_INVALID";
            return;
        }

        if (string.IsNullOrWhiteSpace(modelJson))
        {
            result.Status = "OMNIBOX_PREFLIGHT_RESPONSE_EMPTY";
            return;
        }

        using var modelDocument = JsonDocument.Parse(modelJson);
        var root = modelDocument.RootElement;

        result.Status = ReadString(root, "status", "ready");
        result.PatientReferencePresent = ReadBoolean(root, "patient_reference_present");
        result.PatientReference = ReadString(root, "patient_reference", string.Empty);
        result.PatientDisplayName = ReadString(root, "patient_display_name", string.Empty);
        result.ChartId = ReadString(root, "chart_id", string.Empty);
        result.Confidence = ReadString(root, "confidence", "unknown");
        result.NeedsClarification = ReadBoolean(root, "needs_clarification");
        result.ClarifyingQuestion = ReadString(root, "clarifying_question", string.Empty);
        result.IntendedAction = ReadString(root, "intended_action", "unknown");
        result.RecommendedMode = ReadString(root, "recommended_mode", "fast");
        result.ProcessingRequired = ReadBoolean(root, "processing_required");
        result.DollyReply = ReadString(root, "dolly_reply", string.Empty);
    }

    private static string ReadString(JsonElement root, string propertyName, string fallback)
    {
        return root.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? fallback
            : fallback;
    }

    private static bool ReadBoolean(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var property) &&
               property.ValueKind is JsonValueKind.True or JsonValueKind.False &&
               property.GetBoolean();
    }

    private static bool HasImageAttachment(IEnumerable<AttachmentViewModel> attachments)
    {
        return attachments.Any(attachment =>
        {
            var extension = Path.GetExtension(attachment.Path);
            return extension.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".tif", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".tiff", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".bmp", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".gif", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".webp", StringComparison.OrdinalIgnoreCase);
        });
    }

    private string LoadInstructions()
    {
        return _promptCacheService.GetPrompt(
            PromptFileName,
            "Read the user's omnibox message, identify patient reference if present, ask for clarification if needed, and return JSON only.");
    }
}
