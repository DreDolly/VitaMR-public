using VitaMR.Models;

namespace VitaMR.Services;

public interface ISessionOpenAwarenessService
{
    SessionOpenAwarenessResult BuildSessionOpenAwareness(string vaultRoot, ChartContext activeChart);
}
