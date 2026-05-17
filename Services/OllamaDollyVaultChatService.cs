using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using VitaMR.Models;

namespace VitaMR.Services;

public sealed class OllamaDollyVaultChatService : IDollyVaultChatService
{
    private const string PromptFileName = "Local_Dolly_Vault_Chat.md";
    private const string DollyVoiceContract =
        "Dolly voice:\n" +
        "- Sound like a calm, capable health-record partner, not a system report.\n" +
        "- Prefer 1 or 2 short paragraphs. Use bullets only when they make the answer easier to scan.\n" +
        "- Use plain words and hide internal mechanics unless the user asks for technical detail.\n" +
        "- Be warm without being cutesy. Do not over-apologize or fill space.\n" +
        "- When context is missing, say exactly what is missing and what can be checked next.\n\n";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly HttpClient _httpClient;
    private readonly IPromptCacheService _promptCacheService;

    public OllamaDollyVaultChatService(IPromptCacheService promptCacheService)
    {
        _promptCacheService = promptCacheService;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(90)
        };
    }

    public async Task<string> AnswerFromVaultAsync(
        string endpoint,
        string modelName,
        string userText,
        string conversationContext,
        VaultContextPacket vaultContext,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var requestBody = new
            {
                model = modelName,
                prompt = BuildPrompt(userText, conversationContext, vaultContext),
                stream = false,
                keep_alive = "30s",
                options = new
                {
                    temperature = 0.3,
                    num_predict = 700
                }
            };

            using var request = new StringContent(
                JsonSerializer.Serialize(requestBody, JsonOptions),
                Encoding.UTF8,
                "application/json");

            using var response = await _httpClient.PostAsync(LaptopWorkerProtocol.BuildInferenceUri(endpoint), request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return "I could not reach local Gemma to answer from the vault. I did not write or change anything.";
            }

            return CleanWorkingSummaryContext(LaptopWorkerProtocol.ExtractModelResponseOrThrow(body));
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or UriFormatException or JsonException)
        {
            return exception is TaskCanceledException
                ? "Local Gemma took too long to answer from the vault. I did not write or change anything."
                : "I could not connect to local Gemma for the vault answer. I did not write or change anything.";
        }
    }

    public async Task<string> BuildWorkingSummaryContextAsync(
        string endpoint,
        string modelName,
        string patientDisplayName,
        string workingSummaryMarkdown,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var requestBody = new
            {
                model = modelName,
                prompt =
                    BuildWorkingSummaryPrompt(patientDisplayName, workingSummaryMarkdown),
                stream = false,
                keep_alive = "30s",
                options = new
                {
                    temperature = 0.2,
                    num_predict = 500
                }
            };

            using var request = new StringContent(
                JsonSerializer.Serialize(requestBody, JsonOptions),
                Encoding.UTF8,
                "application/json");

            using var response = await _httpClient.PostAsync(LaptopWorkerProtocol.BuildInferenceUri(endpoint), request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return "LOCAL_GEMMA_WORKING_SUMMARY_UNAVAILABLE";
            }

            return CleanAnswer(LaptopWorkerProtocol.ExtractModelResponseOrThrow(body));
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or UriFormatException or JsonException)
        {
            return "LOCAL_GEMMA_WORKING_SUMMARY_UNAVAILABLE";
        }
    }

    public async Task<string> AnswerFromWorkingSummaryAsync(
        string endpoint,
        string modelName,
        string patientDisplayName,
        string userText,
        string conversationContext,
        string workingSummaryContext,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var requestBody = new
            {
                model = modelName,
                prompt = BuildWorkingSummaryAnswerPrompt(
                    patientDisplayName,
                    userText,
                    conversationContext,
                    workingSummaryContext),
                stream = false,
                keep_alive = "30s",
                options = new
                {
                    temperature = 0.25,
                    num_predict = 450
                }
            };

            using var request = new StringContent(
                JsonSerializer.Serialize(requestBody, JsonOptions),
                Encoding.UTF8,
                "application/json");

            using var response = await _httpClient.PostAsync(LaptopWorkerProtocol.BuildInferenceUri(endpoint), request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return "LOCAL_GEMMA_SUMMARY_CHAT_UNAVAILABLE";
            }

            return CleanDollyAnswer(LaptopWorkerProtocol.ExtractModelResponseOrThrow(body));
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or UriFormatException or JsonException)
        {
            return exception is TaskCanceledException
                ? "LOCAL_GEMMA_SUMMARY_CHAT_TIMEOUT"
                : "LOCAL_GEMMA_SUMMARY_CHAT_UNAVAILABLE";
        }
    }

    public async Task<string> AnswerFromLocalSessionContextAsync(
        string endpoint,
        string modelName,
        string patientDisplayName,
        string userText,
        string conversationContext,
        string localSessionContext,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var requestBody = new
            {
                model = modelName,
                prompt = BuildLocalSessionAnswerPrompt(
                    patientDisplayName,
                    userText,
                    conversationContext,
                    localSessionContext),
                stream = false,
                keep_alive = "45s",
                options = new
                {
                    temperature = 0.25,
                    num_predict = 520
                }
            };

            using var request = new StringContent(
                JsonSerializer.Serialize(requestBody, JsonOptions),
                Encoding.UTF8,
                "application/json");

            using var response = await _httpClient.PostAsync(LaptopWorkerProtocol.BuildInferenceUri(endpoint), request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return "LOCAL_GEMMA_SESSION_CHAT_UNAVAILABLE";
            }

            return CleanDollyAnswer(LaptopWorkerProtocol.ExtractModelResponseOrThrow(body));
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or UriFormatException or JsonException)
        {
            return exception is TaskCanceledException
                ? "LOCAL_GEMMA_SESSION_CHAT_TIMEOUT"
                : "LOCAL_GEMMA_SESSION_CHAT_UNAVAILABLE";
        }
    }

    private string BuildPrompt(string userText, string conversationContext, VaultContextPacket vaultContext)
    {
        return
            $"{LoadInstructions()}\n\n" +
            $"Resolved local display name: {vaultContext.PatientDisplayName}\n" +
            $"Vault context status: {vaultContext.Status}\n" +
            $"Known family-vault patients:\n{FormatKnownPatients(vaultContext.KnownPatients)}\n\n" +
            $"Known chart facts:\n{FormatLines(vaultContext.KnownFacts)}\n\n" +
            $"Available source files / encounter nodes:\n{FormatLines(vaultContext.AvailableSources)}\n\n" +
            $"Missing, pending, or blocked data:\n{FormatLines(vaultContext.PendingItems.Concat(vaultContext.MissingOrUnavailableData))}\n\n" +
            $"Retrieval warnings:\n{FormatLines(vaultContext.RetrievalWarnings)}\n\n" +
            $"Included sterile files:\n{string.Join("\n", vaultContext.IncludedFiles.Select(path => $"- {Path.GetFileName(path)}"))}\n\n" +
            $"Recent conversation bubbles:\n{conversationContext}\n\n" +
            $"Sterile vault context:\n{vaultContext.ContextText}\n\n" +
            $"User question:\n{userText}\n\n" +
            "Answer Dolly-style now.";
    }

    private static string BuildWorkingSummaryPrompt(string patientDisplayName, string workingSummaryMarkdown)
    {
        return
            "You are Gemma-L, VitaMR's laptop-local context compressor for Dolly.\n" +
            "Your task is NOT to answer a user question. Your task is to read the sterile Dolly_Working_Summary.md below and produce a compact working-memory packet Dolly can use during chat.\n\n" +
            "Hard boundaries:\n" +
            "- Use only the supplied sterile markdown.\n" +
            "- Do not claim raw-file access.\n" +
            "- Do not add diagnoses, medications, risks, care gaps, or clinical facts that are not present.\n" +
            "- Do not give medical advice, triage, treatment instructions, orders, or real-world escalation.\n" +
            "- Preserve uncertainty. Anything pending, unverified, user-reported, prototype, or conflict-marked must stay qualified.\n" +
            "- If the summary contains useful chart facts, you MUST produce the packet. Do not say there is not enough information merely because this is not a question.\n\n" +
            "Output exactly these sections, concise but useful:\n" +
            "CURRENT FOCUS:\n" +
            "- 2 to 4 bullets on the active chart situation.\n\n" +
            "KNOWN HIGH-SIGNAL FACTS:\n" +
            "- 4 to 8 bullets covering diagnoses/conditions, medications, risks, timeline highlights, or active conflicts found in the summary.\n\n" +
            "OPEN LOOPS:\n" +
            "- 3 to 6 bullets for care gaps, pending items, conflicts, missing data, or stale/unverified facts.\n\n" +
            "SAFE CONVERSATION HOOKS:\n" +
            "- 3 to 5 bullets Dolly can use for lightweight orientation questions or status-aware conversation.\n\n" +
            "DO NOT DO:\n" +
            "- 3 to 5 bullets naming actions Dolly must avoid for this chart.\n\n" +
            $"Patient display label: {patientDisplayName}\n\n" +
            "DOLLY_WORKING_SUMMARY_MD:\n" +
            "```markdown\n" +
            workingSummaryMarkdown.Trim() +
            "\n```\n";
    }

    private static string BuildWorkingSummaryAnswerPrompt(
        string patientDisplayName,
        string userText,
        string conversationContext,
        string workingSummaryContext)
    {
        return
            "You are Dolly's laptop-local chart conversation helper inside VitaMR.\n" +
            "Answer the user's question from the supplied sterile working-summary packet while a heavier background task may be running.\n" +
            "If the packet includes a C# deterministic answer draft, rewrite that draft into a warm, human-friendly Dolly answer without adding new facts.\n\n" +
            DollyVoiceContract +
            "Hard boundaries:\n" +
            "- Use only the supplied sterile working-summary packet and recent conversation.\n" +
            "- Do not add facts beyond the C# draft or source packet.\n" +
            "- Do not claim to read raw files, uploaded documents, or backend results that are still processing.\n" +
            "- Do not diagnose, triage, prescribe, order tests, or give treatment instructions.\n" +
            "- If the packet does not contain enough information, say what is missing and keep the answer short.\n" +
            "- If a background task might change the answer, say that the chart is still updating.\n" +
            "- Speak naturally as Dolly, not as a status template or data table.\n" +
            "- Hide internal file paths, chart ids, row labels, and source mechanics unless the user asks for them.\n\n" +
            $"Resolved patient label: {patientDisplayName}\n\n" +
            $"Recent conversation:\n{conversationContext}\n\n" +
            "Sterile working-summary packet:\n" +
            "```markdown\n" +
            workingSummaryContext.Trim() +
            "\n```\n\n" +
            $"User question:\n{userText.Trim()}\n\n" +
            "Return JSON only in this exact shape: {\"answer\":\"1 to 3 concise, friendly paragraphs or bullets\"}.";
    }

    private static string BuildLocalSessionAnswerPrompt(
        string patientDisplayName,
        string userText,
        string conversationContext,
        string localSessionContext)
    {
        return
            "You are Dolly, VitaMR's local front-desk medical-record companion.\n" +
            "The user is chatting while background work may be running. Interpret the user's wording naturally. Decide whether they are asking about the active patient, the family vault roster, or task progress from the supplied context.\n\n" +
            DollyVoiceContract +
            "Hard boundaries:\n" +
            "- Use only the supplied sterile local context and recent conversation.\n" +
            "- Do not claim raw-file access.\n" +
            "- Do not diagnose, triage, prescribe, order tests, or give treatment instructions.\n" +
            "- Do not expose internal chart IDs, file paths, row labels, task IDs, or source mechanics unless the user explicitly asks for technical details.\n" +
            "- If task progress is relevant, summarize it in plain language.\n" +
            "- If roster is relevant, list patient display names only.\n" +
            "- If patient context is relevant, answer warmly in normal human language.\n" +
            "- If the context is insufficient, say what is missing.\n\n" +
            $"Active patient label: {patientDisplayName}\n\n" +
            $"Recent conversation:\n{conversationContext}\n\n" +
            "LOCAL_SESSION_CONTEXT:\n" +
            "```markdown\n" +
            localSessionContext.Trim() +
            "\n```\n\n" +
            $"User message:\n{userText.Trim()}\n\n" +
            "Return JSON only in this exact shape: {\"answer\":\"1 to 3 concise, friendly paragraphs or short bullets\"}.";
    }

    private string LoadInstructions()
    {
        return _promptCacheService.GetPrompt(
            PromptFileName,
            "You are Dolly. Answer using only provided sterile vault context and recent conversation. Do not claim to inspect raw files. Be concise.");
    }

    private static string FormatKnownPatients(IReadOnlyList<string> knownPatients)
    {
        return knownPatients.Count == 0
            ? "none"
            : string.Join("\n", knownPatients.Select(patient => $"- {patient}"));
    }

    private static string FormatLines(IEnumerable<string> lines)
    {
        var lineList = lines
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Take(30)
            .ToList();

        return lineList.Count == 0
            ? "none"
            : string.Join("\n", lineList.Select(line => $"- {line}"));
    }

    private static string CleanAnswer(string value)
    {
        var answer = value.Trim();
        return string.IsNullOrWhiteSpace(answer)
            ? "I checked the allowed vault context, but I do not have enough stored information to answer that yet."
            : answer;
    }

    private static string CleanWorkingSummaryContext(string value)
    {
        var answer = value.Trim();
        return string.IsNullOrWhiteSpace(answer)
            ? "LOCAL_GEMMA_WORKING_SUMMARY_EMPTY"
            : answer;
    }

    private static string CleanSummaryChatAnswer(string value)
    {
        var answer = value.Trim();
        return string.IsNullOrWhiteSpace(answer)
            ? "LOCAL_GEMMA_SUMMARY_CHAT_EMPTY"
            : answer;
    }

    private static string CleanDollyAnswer(string value)
    {
        var answer = value.Trim();

        if (string.IsNullOrWhiteSpace(answer))
        {
            return "LOCAL_GEMMA_SUMMARY_CHAT_EMPTY";
        }

        var json = ExtractJsonObjectOrEmpty(answer);

        if (string.IsNullOrWhiteSpace(json))
        {
            return answer;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (root.TryGetProperty("answer", out var answerElement) &&
                answerElement.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(answerElement.GetString()))
            {
                return answerElement.GetString()!.Trim();
            }
        }
        catch (JsonException)
        {
            return answer;
        }

        return answer;
    }

    private static string ExtractJsonObjectOrEmpty(string value)
    {
        var trimmed = value.Trim();
        var fenceStart = trimmed.IndexOf("```", StringComparison.Ordinal);

        if (fenceStart >= 0)
        {
            var afterFence = trimmed[(fenceStart + 3)..].TrimStart();
            if (afterFence.StartsWith("json", StringComparison.OrdinalIgnoreCase))
            {
                afterFence = afterFence[4..].TrimStart();
            }

            var fenceEnd = afterFence.IndexOf("```", StringComparison.Ordinal);
            trimmed = fenceEnd >= 0 ? afterFence[..fenceEnd].Trim() : afterFence.Trim();
        }

        var start = trimmed.IndexOf('{');
        var end = trimmed.LastIndexOf('}');

        return start >= 0 && end > start
            ? trimmed[start..(end + 1)]
            : string.Empty;
    }
}
