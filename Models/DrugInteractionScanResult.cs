namespace VitaMR.Models;

public sealed class DrugInteractionScanResult
{
    public bool WasRun { get; set; }

    public string Status { get; set; } = "DRUG_INTERACTION_SCAN_NOT_RUN";

    public int ActiveMedicationsReviewed { get; set; }

    public int FindingsWritten { get; set; }

    public string OutputPath { get; set; } = string.Empty;

    public List<string> Findings { get; } = [];
}
