namespace VitaMR.Models;

public sealed class ChartEditActionPacket
{
    public string Operation { get; set; } = string.Empty;

    public string TargetType { get; set; } = string.Empty;

    public string TargetId { get; set; } = string.Empty;

    public string TargetHint { get; set; } = string.Empty;

    public string NewStatus { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    public bool RequiresUserVerification { get; set; } = true;

    public string VerificationPrompt { get; set; } = string.Empty;
}
