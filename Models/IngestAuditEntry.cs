namespace VitaMR.Models;

public sealed class IngestAuditEntry
{
    public DateTime Timestamp { get; set; }

    public string ChartId { get; set; } = string.Empty;

    public string SourcePath { get; set; } = string.Empty;

    public string RawVaultPath { get; set; } = string.Empty;

    public string RawDisplayName { get; set; } = string.Empty;

    public string SourceSystem { get; set; } = string.Empty;

    public string SourceFacility { get; set; } = string.Empty;

    public string SourceType { get; set; } = string.Empty;

    public string ScrubStatus { get; set; } = string.Empty;

    public int ScrubFindingCount { get; set; }

    public string LocalModelReviewStatus { get; set; } = string.Empty;

    public string LocalModelRoute { get; set; } = string.Empty;

    public string LocalModelEndpointUsed { get; set; } = string.Empty;

    public string LocalModelNameUsed { get; set; } = string.Empty;

    public bool LocalModelFailoverUsed { get; set; }

    public string LocalModelRouteStatus { get; set; } = string.Empty;

    public string OmniboxPreflightRoute { get; set; } = string.Empty;

    public string OmniboxPreflightEndpointUsed { get; set; } = string.Empty;

    public string OmniboxPreflightModelNameUsed { get; set; } = string.Empty;

    public bool OmniboxPreflightFailoverUsed { get; set; }

    public string OmniboxPreflightRouteStatus { get; set; } = string.Empty;

    public string PrivacyGateStatus { get; set; } = string.Empty;

    public string FinalScrubbedPath { get; set; } = string.Empty;

    public int SecondPassReplacementCount { get; set; }

    public string ImageDocumentType { get; set; } = string.Empty;

    public string ImageReviewStatus { get; set; } = string.Empty;

    public bool OfficialReadRequired { get; set; }

    public bool OfficialReadPresent { get; set; }

    public string ImageReviewMetadataPath { get; set; } = string.Empty;

    public bool ApiSent { get; set; }

    public bool WikiWritten { get; set; }
}
