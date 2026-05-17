using System.Net.Http;
using System.Text;
using System.Text.Json;
using VitaMR.Models;

namespace VitaMR.Services;

public sealed class OllamaPatientIntakeExtractionService : IPatientIntakeExtractionService
{
    private static readonly string[] CandidateModels =
    [
        "phi4-mini:latest",
        "llama3.2:3b",
        "gemma4:e2b"
    ];

    private static readonly JsonSerializerOptions RequestJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly JsonSerializerOptions ResponseJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(35)
    };

    public async Task<PatientIntakeExtractionResult> ExtractAsync(
        string endpoint,
        string inputText,
        CancellationToken cancellationToken = default)
    {
        var result = new PatientIntakeExtractionResult
        {
            WasAttempted = true,
            Status = "LOCAL_INTAKE_STARTED"
        };

        if (string.IsNullOrWhiteSpace(inputText))
        {
            result.Status = "LOCAL_INTAKE_EMPTY";
            return result;
        }

        foreach (var modelName in CandidateModels)
        {
            var attempt = await TryExtractWithModelAsync(endpoint, modelName, inputText, cancellationToken);
            if (attempt.WasAvailable)
            {
                return attempt;
            }

            result = attempt;
        }

        return result;
    }

    private async Task<PatientIntakeExtractionResult> TryExtractWithModelAsync(
        string endpoint,
        string modelName,
        string inputText,
        CancellationToken cancellationToken)
    {
        var result = new PatientIntakeExtractionResult
        {
            WasAttempted = true,
            ModelName = modelName,
            Status = "LOCAL_INTAKE_MODEL_STARTED"
        };

        try
        {
            var requestBody = new
            {
                model = modelName,
                prompt = BuildPrompt(inputText),
                stream = false,
                format = "json",
                keep_alive = "45s",
                options = new
                {
                    temperature = 0.0,
                    num_predict = 500
                }
            };

            using var request = new StringContent(
                JsonSerializer.Serialize(requestBody, RequestJsonOptions),
                Encoding.UTF8,
                "application/json");
            using var response = await _httpClient.PostAsync(LaptopWorkerProtocol.BuildInferenceUri(endpoint), request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            result.RawResponse = body;

            if (!response.IsSuccessStatusCode)
            {
                result.Status = $"LOCAL_INTAKE_HTTP_{(int)response.StatusCode}";
                return result;
            }

            var modelJson = LaptopWorkerProtocol.ExtractModelResponseOrThrow(body);
            result.RawResponse = modelJson;
            var extracted = JsonSerializer.Deserialize<PatientIntakeModelPayload>(
                ExtractJsonObject(modelJson),
                ResponseJsonOptions);

            if (extracted is null)
            {
                result.Status = "LOCAL_INTAKE_INVALID_JSON";
                return result;
            }

            result.FullName = Normalize(extracted.FullName);
            result.DateOfBirth = Normalize(extracted.DateOfBirth);
            result.Address = Normalize(extracted.Address);
            result.PhoneNumber = Normalize(extracted.PhoneNumber);
            result.Email = Normalize(extracted.Email);
            result.SocialSecurityNumber = Normalize(extracted.SocialSecurityNumber);
            result.RelationshipNotes = Normalize(extracted.RelationshipNotes);
            result.PhotoPath = Normalize(extracted.PhotoPath);
            result.CandidateNames = extracted.CandidateNames
                .Select(Normalize)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            result.WasAvailable = HasAnyField(result);
            result.Status = result.WasAvailable ? "LOCAL_INTAKE_READY" : "LOCAL_INTAKE_NO_FIELDS";
            return result;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException or UriFormatException)
        {
            result.Status = exception is TaskCanceledException ? "LOCAL_INTAKE_TIMEOUT" : "LOCAL_INTAKE_UNAVAILABLE";
            result.RawResponse = exception.Message;
            return result;
        }
    }

    private static string BuildPrompt(string inputText)
    {
        return
            "You are a private local patient-demographics extractor running inside VitaMR. Raw identifying text stays local.\n" +
            "Extract only explicit patient identity/contact fields from the text. Do not infer, invent, normalize beyond trimming, or include clinical facts.\n" +
            "If multiple people are named, include each possible person name in candidate_names. Do not choose one unless the text explicitly labels the patient.\n" +
            "If a field is not explicitly present, return an empty string.\n" +
            "Return JSON only with exactly this shape:\n" +
            "{\"full_name\":\"\",\"date_of_birth\":\"\",\"address\":\"\",\"phone_number\":\"\",\"email\":\"\",\"social_security_number\":\"\",\"relationship_notes\":\"\",\"photo_path\":\"\",\"candidate_names\":[]}\n\n" +
            "Input text:\n" +
            inputText.Trim();
    }

    private static string ExtractJsonObject(string raw)
    {
        var trimmed = raw.Trim();
        var start = trimmed.IndexOf('{');
        var end = trimmed.LastIndexOf('}');

        if (start < 0 || end <= start)
        {
            throw new JsonException("No JSON object found.");
        }

        return trimmed[start..(end + 1)];
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.ReplaceLineEndings(" ").Trim();
    }

    private static bool HasAnyField(PatientIntakeExtractionResult result)
    {
        return !string.IsNullOrWhiteSpace(result.FullName) ||
               !string.IsNullOrWhiteSpace(result.DateOfBirth) ||
               !string.IsNullOrWhiteSpace(result.Address) ||
               !string.IsNullOrWhiteSpace(result.PhoneNumber) ||
               !string.IsNullOrWhiteSpace(result.Email) ||
               result.CandidateNames.Count > 0;
    }

    private sealed class PatientIntakeModelPayload
    {
        [System.Text.Json.Serialization.JsonPropertyName("full_name")]
        public string FullName { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("date_of_birth")]
        public string DateOfBirth { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("address")]
        public string Address { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("phone_number")]
        public string PhoneNumber { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("social_security_number")]
        public string SocialSecurityNumber { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("relationship_notes")]
        public string RelationshipNotes { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("photo_path")]
        public string PhotoPath { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("candidate_names")]
        public List<string> CandidateNames { get; set; } = [];
    }
}
