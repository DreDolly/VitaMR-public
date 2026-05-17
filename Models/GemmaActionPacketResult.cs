namespace VitaMR.Models;

public sealed class GemmaActionPacketResult
{
    public bool WasValid { get; set; }

    public string Status { get; set; } = string.Empty;

    public GemmaActionPacket? Packet { get; set; }

    public List<string> Errors { get; set; } = [];

    public string RawResponse { get; set; } = string.Empty;

    public int Attempts { get; set; }
}
