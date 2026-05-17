namespace VitaMR.Models;

public sealed class VaccineActionPacket
{
    public List<VaccineRecordPacket> Records { get; set; } = [];

    public string SourceHint { get; set; } = string.Empty;

    public bool RequiresUserVerification { get; set; } = true;

    public string VerificationPrompt { get; set; } = string.Empty;
}
