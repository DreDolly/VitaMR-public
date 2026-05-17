using VitaMR.Models;

namespace VitaMR.Services;

public interface IGeminiWikiPatchService
{
    Task<GeminiWikiPatchResult> PlanPatchAsync(
        bool isEnabled,
        string modelName,
        string scrubbedUserText,
        string conversationContext,
        VaultContextPacket contextPacket,
        CancellationToken cancellationToken = default);
}
