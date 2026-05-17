namespace VitaMR.Models;

public sealed class ChartIdentity
{
    public string ChartId { get; set; } = string.Empty;

    public string DisplayLabel { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
