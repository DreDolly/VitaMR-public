namespace VitaMR.Models;

public sealed class ChartContext
{
    public ChartContext(string chartId, string localDisplayName)
    {
        ChartId = string.IsNullOrWhiteSpace(chartId)
            ? "VITA-0000"
            : chartId.Trim();
        LocalDisplayName = string.IsNullOrWhiteSpace(localDisplayName)
            ? ChartId
            : localDisplayName.Trim();
    }

    public string ChartId { get; }

    public string ChartFolderName => ChartId;

    public string LocalDisplayName { get; }
}
