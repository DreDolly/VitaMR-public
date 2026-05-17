using System.Net.Http;
using System.Text.Json;

namespace VitaMR.Services;

public static class LaptopWorkerProtocol
{
    private const string WorkerPortSuffix = ":5055";

    public static bool IsWorkerEndpoint(string endpoint)
    {
        var normalized = NormalizeBaseEndpoint(endpoint);
        return normalized.Contains(WorkerPortSuffix, StringComparison.OrdinalIgnoreCase);
    }

    public static Uri BuildInferenceUri(string endpoint)
    {
        var normalized = NormalizeBaseEndpoint(endpoint);
        return new Uri($"{normalized}{(IsWorkerEndpoint(normalized) ? "/infer" : "/api/generate")}");
    }

    public static Uri BuildWarmupUri(string endpoint)
    {
        var normalized = NormalizeBaseEndpoint(endpoint);
        return new Uri($"{normalized}{(IsWorkerEndpoint(normalized) ? "/warmup" : "/api/generate")}");
    }

    public static Uri BuildRouteStatusUri(string endpoint)
    {
        var normalized = NormalizeBaseEndpoint(endpoint);
        return new Uri($"{normalized}/route-status");
    }

    public static string ExtractModelResponseOrThrow(string body)
    {
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        if (root.TryGetProperty("accepted", out var acceptedElement))
        {
            var accepted = acceptedElement.ValueKind == JsonValueKind.True ||
                           acceptedElement.ValueKind == JsonValueKind.False && acceptedElement.GetBoolean();

            if (!accepted)
            {
                throw new HttpRequestException(ReadString(root, "reason", "Laptop worker rejected the request."));
            }

            if (root.TryGetProperty("responseText", out var responseTextElement) &&
                responseTextElement.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(responseTextElement.GetString()))
            {
                return responseTextElement.GetString() ?? string.Empty;
            }

            if (root.TryGetProperty("data", out var dataElement) &&
                dataElement.ValueKind == JsonValueKind.Object &&
                dataElement.TryGetProperty("response", out var nestedResponseElement) &&
                nestedResponseElement.ValueKind == JsonValueKind.String)
            {
                var nestedResponse = nestedResponseElement.GetString() ?? string.Empty;

                if (!string.IsNullOrWhiteSpace(nestedResponse))
                {
                    return nestedResponse;
                }
            }

            throw new JsonException(ReadString(root, "reason", "Laptop worker returned an empty model response."));
        }

        if (root.TryGetProperty("response", out var responseElement) &&
            responseElement.ValueKind == JsonValueKind.String)
        {
            return responseElement.GetString() ?? string.Empty;
        }

        throw new JsonException("Model response did not include a readable response field.");
    }

    public static LaptopWorkerRouteStatus ParseRouteStatus(string body)
    {
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        return new LaptopWorkerRouteStatus(
            root.TryGetProperty("available", out var availableElement) &&
            availableElement.ValueKind is JsonValueKind.True or JsonValueKind.False &&
            availableElement.GetBoolean(),
            ReadString(root, "quality", string.Empty),
            ReadString(root, "reason", "Unknown route status."),
            ReadInt32(root, "ramPercent"),
            ReadInt32(root, "ramThreshold"),
            ReadInt32(root, "hardRamLimit"),
            root.TryGetProperty("ollamaReachable", out var ollamaElement) &&
            ollamaElement.ValueKind is JsonValueKind.True or JsonValueKind.False &&
            ollamaElement.GetBoolean(),
            root.TryGetProperty("modelReady", out var modelReadyElement) &&
            modelReadyElement.ValueKind is JsonValueKind.True or JsonValueKind.False &&
            modelReadyElement.GetBoolean(),
            ReadString(root, "modelName", "gemma4:e4b"),
            ReadString(root, "lastInferenceStatus", string.Empty),
            ReadString(root, "lastError", string.Empty),
            ReadString(root, "recommendedAction", string.Empty));
    }

    private static string NormalizeBaseEndpoint(string endpoint)
    {
        return string.IsNullOrWhiteSpace(endpoint)
            ? "http://localhost:11434"
            : endpoint.Trim().TrimEnd('/');
    }

    private static string ReadString(JsonElement root, string propertyName, string fallback)
    {
        return root.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? fallback
            : fallback;
    }

    private static int ReadInt32(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var value)
            ? value
            : 0;
    }
}

public sealed record LaptopWorkerRouteStatus(
    bool Available,
    string Quality,
    string Reason,
    int RamPercent,
    int RamThreshold,
    int HardRamLimit,
    bool OllamaReachable,
    bool ModelReady,
    string ModelName,
    string LastInferenceStatus,
    string LastError,
    string RecommendedAction);
