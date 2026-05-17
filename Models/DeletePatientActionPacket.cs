namespace VitaMR.Models;

public sealed class DeletePatientActionPacket
{
    public string PatientDisplayName { get; set; } = string.Empty;

    public string TargetHint { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    public bool RequiresUserVerification { get; set; } = true;

    public string VerificationPrompt { get; set; } = string.Empty;
}
