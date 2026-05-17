namespace VitaMR.Models;

public sealed class LocalModelReviewResult
{
    public bool WasAttempted { get; set; }

    public bool IsAvailable { get; set; }

    public string Status { get; set; } = "LOCAL_MODEL_NOT_RUN";

    public string Route { get; set; } = string.Empty;

    public string EndpointUsed { get; set; } = string.Empty;

    public string ModelNameUsed { get; set; } = string.Empty;

    public bool FailoverUsed { get; set; }

    public string RouteStatus { get; set; } = string.Empty;

    public string RemainingPhiRisk { get; set; } = "unknown";

    public string Recommendation { get; set; } = "Local model review has not run.";

    public List<string> Findings { get; set; } = [];

    public List<LocalModelFinding> StructuredFindings { get; set; } = [];

    public string RewrittenPayload { get; set; } = string.Empty;

    public List<LocalModelReviewedPayload> ReviewedPayloads { get; set; } = [];

    public string RawResponse { get; set; } = string.Empty;
}
