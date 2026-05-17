using VitaMR.Models;

namespace VitaMR.Services;

public interface IGeminiOmniboxAnswerService
{
    Task<GeminiOmniboxAnswerResult> AnswerAsync(
        bool isEnabled,
        string modelName,
        string scrubbedUserText,
        string conversationContext,
        VaultContextPacket contextPacket,
        CancellationToken cancellationToken = default);

    Task<GeminiOmniboxAnswerResult> AnswerStreamingAsync(
        bool isEnabled,
        string modelName,
        string scrubbedUserText,
        string conversationContext,
        VaultContextPacket contextPacket,
        Action<string> onTextDelta,
        CancellationToken cancellationToken = default);
}
