namespace VitaMR.Models;

public sealed class OmniboxPreflightResult
{
    public bool WasAttempted { get; set; }

    public bool IsAvailable { get; set; }

    public string Status { get; set; } = "OMNIBOX_PREFLIGHT_NOT_RUN";

    public string Route { get; set; } = string.Empty;

    public string EndpointUsed { get; set; } = string.Empty;

    public string ModelNameUsed { get; set; } = string.Empty;

    public bool FailoverUsed { get; set; }

    public string RouteStatus { get; set; } = string.Empty;

    public bool PatientReferencePresent { get; set; }

    public string PatientReference { get; set; } = string.Empty;

    public string PatientDisplayName { get; set; } = string.Empty;

    public string ChartId { get; set; } = string.Empty;

    public string Confidence { get; set; } = "unknown";

    public bool NeedsClarification { get; set; }

    public string ClarifyingQuestion { get; set; } = string.Empty;

    public string IntendedAction { get; set; } = "unknown";

    public string RecommendedMode { get; set; } = "fast";

    public bool ProcessingRequired { get; set; }

    public string DollyReply { get; set; } = string.Empty;

    public string RawResponse { get; set; } = string.Empty;
}
