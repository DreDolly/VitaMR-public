namespace VitaMR.Models;

public sealed class ImageDocumentReviewResult
{
    public string SourcePath { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public bool WasAttempted { get; set; }

    public bool IsAvailable { get; set; }

    public string Status { get; set; } = "IMAGE_REVIEW_NOT_RUN";

    public string ImageDocumentType { get; set; } = "unknown";

    public bool IsRadiologyOrCardiology { get; set; }

    public bool OfficialReadPresent { get; set; }

    public bool OfficialReadRequired { get; set; }

    public string RequiredFollowup { get; set; } = string.Empty;

    public bool ClinicalInterpretationAttempted { get; set; }

    public string SavedMetadataPath { get; set; } = string.Empty;

    public string RawResponse { get; set; } = string.Empty;
}
