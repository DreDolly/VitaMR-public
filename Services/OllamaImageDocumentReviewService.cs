using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using VitaMR.Models;

namespace VitaMR.Services;

public sealed class OllamaImageDocumentReviewService : IImageDocumentReviewService
{
    private const string PromptFileName = "Local_Image_Document_Classifier.md";

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".tif",
        ".tiff",
        ".bmp",
        ".gif",
        ".webp"
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly HttpClient _httpClient;
    private readonly IPromptCacheService _promptCacheService;

    public OllamaImageDocumentReviewService(IPromptCacheService promptCacheService)
    {
        _promptCacheService = promptCacheService;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(90)
        };
    }

    public async Task<IReadOnlyList<ImageDocumentReviewResult>> ReviewImageDocumentsAsync(
        string vaultRoot,
        ChartContext chartContext,
        string endpoint,
        string modelName,
        IReadOnlyList<IngestResult> ingestResults,
        CancellationToken cancellationToken = default)
    {
        var imageResults = ingestResults
            .Where(result => ImageExtensions.Contains(Path.GetExtension(result.RawVaultPath)))
            .ToList();

        if (imageResults.Count == 0)
        {
            return [];
        }

        var results = new List<ImageDocumentReviewResult>();

        foreach (var ingestResult in imageResults)
        {
            results.Add(await ReviewOneImageAsync(
                vaultRoot,
                chartContext,
                endpoint,
                modelName,
                ingestResult,
                cancellationToken));
        }

        return results;
    }

    private async Task<ImageDocumentReviewResult> ReviewOneImageAsync(
        string vaultRoot,
        ChartContext chartContext,
        string endpoint,
        string modelName,
        IngestResult ingestResult,
        CancellationToken cancellationToken)
    {
        var result = new ImageDocumentReviewResult
        {
            SourcePath = ingestResult.RawVaultPath,
            DisplayName = ingestResult.DisplayName,
            WasAttempted = true,
            Status = "IMAGE_CLASSIFICATION_STARTED"
        };

        try
        {
            var uri = LaptopWorkerProtocol.BuildInferenceUri(endpoint);
            var imageBase64 = Convert.ToBase64String(await File.ReadAllBytesAsync(ingestResult.RawVaultPath, cancellationToken));
            var requestBody = new
            {
                model = modelName,
                prompt = BuildPrompt(ingestResult.DisplayName),
                images = new[] { imageBase64 },
                stream = false,
                format = "json",
                keep_alive = "30s",
                options = new
                {
                    temperature = 0,
                    num_predict = 450
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
                result.Status = "IMAGE_CLASSIFICATION_UNAVAILABLE";
                result.RequiredFollowup = $"Image classifier returned {(int)response.StatusCode}. Official report required before wiki use.";
                result.OfficialReadRequired = true;
                return SaveResult(vaultRoot, chartContext, result);
            }

            result.IsAvailable = true;
            ApplyOllamaResponse(result, body);
            EnforceClinicalSafety(result);
            return SaveResult(vaultRoot, chartContext, result);
        }
        catch (Exception exception) when (exception is IOException or HttpRequestException or TaskCanceledException or UriFormatException)
        {
            result.Status = "IMAGE_CLASSIFICATION_UNAVAILABLE";
            result.RequiredFollowup = exception is TaskCanceledException
                ? "Image classification timed out. Official report required before wiki use."
                : "Image classification could not connect. Official report required before wiki use.";
            result.OfficialReadRequired = true;
            result.RawResponse = exception.Message;
            return SaveResult(vaultRoot, chartContext, result);
        }
    }

    private string BuildPrompt(string displayName)
    {
        return
            $"{LoadClassifierInstructions()}\n\n" +
            "Use thinking mode internally for image/document data processing. Do not include reasoning.\n\n" +
            $"Classify this image document now. File name: {displayName}\n" +
            "Return JSON only.";
    }

    private static void ApplyOllamaResponse(ImageDocumentReviewResult result, string body)
    {
        string modelJson;

        try
        {
            modelJson = LaptopWorkerProtocol.ExtractModelResponseOrThrow(body);
        }
        catch
        {
            result.Status = "IMAGE_CLASSIFICATION_RESPONSE_INVALID";
            result.OfficialReadRequired = true;
            result.RequiredFollowup = "Image classifier response was invalid. Official report required before wiki use.";
            return;
        }

        if (string.IsNullOrWhiteSpace(modelJson))
        {
            result.Status = "IMAGE_CLASSIFICATION_RESPONSE_EMPTY";
            result.OfficialReadRequired = true;
            result.RequiredFollowup = "Image classifier response was empty. Official report required before wiki use.";
            return;
        }

        try
        {
            using var modelDocument = JsonDocument.Parse(modelJson);
            var root = modelDocument.RootElement;

            result.Status = "IMAGE_CLASSIFICATION_READY";
            result.ImageDocumentType = ReadString(root, "image_document_type", "unknown");
            result.IsRadiologyOrCardiology = ReadBoolean(root, "is_radiology_or_cardiology");
            result.OfficialReadPresent = ReadBoolean(root, "official_read_present");
            result.OfficialReadRequired = ReadBoolean(root, "official_read_required");
            result.RequiredFollowup = ReadString(root, "required_followup", string.Empty);
            result.ClinicalInterpretationAttempted = ReadBoolean(root, "clinical_interpretation_attempted");
        }
        catch (JsonException)
        {
            result.Status = "IMAGE_CLASSIFICATION_RESPONSE_NOT_JSON";
            result.OfficialReadRequired = true;
            result.RequiredFollowup = "Image classifier did not return required JSON. Official report required before wiki use.";
        }
    }

    private static ImageDocumentReviewResult SaveResult(
        string vaultRoot,
        ChartContext chartContext,
        ImageDocumentReviewResult result)
    {
        var imageReviewFolder = Path.Combine(vaultRoot, chartContext.ChartFolderName, "scrubbed", "image-review");
        Directory.CreateDirectory(imageReviewFolder);

        var metadataPath = GetNonConflictingPath(
            imageReviewFolder,
            $"{Path.GetFileNameWithoutExtension(result.DisplayName)}.image-classification.json");

        File.WriteAllText(metadataPath, JsonSerializer.Serialize(result, JsonOptions));
        result.SavedMetadataPath = metadataPath;

        if (result.OfficialReadRequired && !result.OfficialReadPresent)
        {
            AppendFutureDataNeeded(vaultRoot, chartContext, result);
        }

        return result;
    }

    private static void EnforceClinicalSafety(ImageDocumentReviewResult result)
    {
        var type = result.ImageDocumentType.Trim().ToLowerInvariant();
        var medicalImageType = type is "xray" or "ct" or "mri" or "ultrasound" or "ekg" or "echo" or "radiology_document" or "cardiology_document";

        if (medicalImageType)
        {
            result.IsRadiologyOrCardiology = true;
            result.OfficialReadRequired = !result.OfficialReadPresent;
        }

        if (result.OfficialReadRequired && string.IsNullOrWhiteSpace(result.RequiredFollowup))
        {
            result.RequiredFollowup = "Official radiologist/cardiologist report needed.";
        }

        if (result.ClinicalInterpretationAttempted)
        {
            result.Status = "IMAGE_CLASSIFICATION_BLOCKED_FOR_INTERPRETATION";
            result.OfficialReadRequired = true;
            result.RequiredFollowup = "Classifier attempted clinical interpretation. Discard result and obtain official report.";
        }
    }

    private static void AppendFutureDataNeeded(
        string vaultRoot,
        ChartContext chartContext,
        ImageDocumentReviewResult result)
    {
        var wikiFolder = Path.Combine(vaultRoot, chartContext.ChartFolderName, "wiki");
        Directory.CreateDirectory(wikiFolder);

        var path = Path.Combine(wikiFolder, "Future_Data_Needed.md");
        var line = $"- {DateTime.Now:yyyy-MM-dd HH:mm}: {result.RequiredFollowup} Source: {result.DisplayName}{Environment.NewLine}";

        if (!File.Exists(path))
        {
            File.WriteAllText(path, $"# Future Data Needed{Environment.NewLine}{Environment.NewLine}");
        }

        File.AppendAllText(path, line);
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

    private string LoadClassifierInstructions()
    {
        return _promptCacheService.GetPrompt(
            PromptFileName,
            "Classify the image type only. Do not interpret medical findings. Require official radiology/cardiology reports. Return JSON only.");
    }

    private static string GetNonConflictingPath(string folder, string fileName)
    {
        var targetPath = Path.Combine(folder, fileName);

        if (!File.Exists(targetPath))
        {
            return targetPath;
        }

        var name = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);

        for (var index = 2; ; index++)
        {
            var candidate = Path.Combine(folder, $"{name}_{index}{extension}");

            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }
    }
}
