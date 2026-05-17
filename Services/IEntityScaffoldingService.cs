using VitaMR.Models;

namespace VitaMR.Services;

public interface IEntityScaffoldingService
{
    IReadOnlyList<string> EnsureScaffold(string vaultRoot, ChartContext chartContext);
}
