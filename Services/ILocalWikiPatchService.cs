using VitaMR.Models;

namespace VitaMR.Services;

public interface ILocalWikiPatchService
{
    Task<GeminiWikiPatchResult> PlanPatchAsync(
        string endpoint,
        string modelName,
        string scrubbedUserText,
        string conversationContext,
        VaultContextPacket contextPacket,
        CancellationToken cancellationToken = default);
}
