using VitaMR.Models;

namespace VitaMR.Services;

public interface ISpecialtyWeaverService
{
    SpecialtyWeaverResult WeaveChart(string vaultRoot, ChartContext chartContext);
}
