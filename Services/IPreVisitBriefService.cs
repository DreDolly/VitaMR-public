using VitaMR.Models;

namespace VitaMR.Services;

public interface IPreVisitBriefService
{
    PreVisitBriefResult GenerateBrief(string vaultRoot, ChartContext chartContext);
}
