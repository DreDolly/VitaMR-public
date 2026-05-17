namespace VitaMR.Models;

public sealed class ScrubbedDocumentClassificationResult
{
    public string DocumentType { get; set; } = "Source_Document";

    public string DateOfService { get; set; } = string.Empty;

    public string RiskLevel { get; set; } = "low";

    public bool NeedsDeepExtraction { get; set; } = true;

    public string RecommendedModelRole { get; set; } = "thinking";

    public List<string> Reasons { get; set; } = [];

    public List<string> MissingData { get; set; } = [];
}
