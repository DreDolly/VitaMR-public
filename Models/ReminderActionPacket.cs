namespace VitaMR.Models;

public sealed class ReminderActionPacket
{
    public List<ReminderRecordPacket> Records { get; set; } = [];

    public bool RequiresUserVerification { get; set; } = true;

    public string VerificationPrompt { get; set; } = string.Empty;
}
