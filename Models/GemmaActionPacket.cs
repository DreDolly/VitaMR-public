namespace VitaMR.Models;

public sealed class GemmaActionPacket
{
    public string Intent { get; set; } = string.Empty;

    public string PatientDisplayName { get; set; } = string.Empty;

    public double Confidence { get; set; }

    public bool RequiresConfirmation { get; set; }

    public string Tool { get; set; } = string.Empty;

    public bool BackendNeeded { get; set; }

    public string ResponseOwner { get; set; } = "local_agent";

    public bool ClinicalPayloadMutable { get; set; } = true;

    public string DisplayMode { get; set; } = "chat";

    public string InterstitialMessage { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    public string UserFacingAnswer { get; set; } = string.Empty;

    public ChartEditActionPacket? EditAction { get; set; }

    public VaccineActionPacket? VaccineAction { get; set; }

    public DeletePatientActionPacket? DeletePatientAction { get; set; }

    public ReminderActionPacket? ReminderAction { get; set; }
}
