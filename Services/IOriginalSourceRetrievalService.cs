using VitaMR.Models;

namespace VitaMR.Services;

public interface IOriginalSourceRetrievalService
{
    OriginalSourceRetrievalResult FindOriginalSources(
        string vaultRoot,
        ChartContext chartContext,
        string userRequest,
        int maxResults = 3);
}
