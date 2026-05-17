using VitaMR.Models;

namespace VitaMR.Services;

public interface ILocalModelWarmupService
{
    Task<LocalModelWarmupResult> WarmAsync(
        string endpoint,
        string modelName,
        CancellationToken cancellationToken = default);
}
