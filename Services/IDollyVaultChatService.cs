using VitaMR.Models;

namespace VitaMR.Services;

public interface IDollyVaultChatService
{
    Task<string> AnswerFromVaultAsync(
        string endpoint,
        string modelName,
        string userText,
        string conversationContext,
        VaultContextPacket vaultContext,
        CancellationToken cancellationToken = default);

    Task<string> BuildWorkingSummaryContextAsync(
        string endpoint,
        string modelName,
        string patientDisplayName,
        string workingSummaryMarkdown,
        CancellationToken cancellationToken = default);

    Task<string> AnswerFromWorkingSummaryAsync(
        string endpoint,
        string modelName,
        string patientDisplayName,
        string userText,
        string conversationContext,
        string workingSummaryContext,
        CancellationToken cancellationToken = default);

    Task<string> AnswerFromLocalSessionContextAsync(
        string endpoint,
        string modelName,
        string patientDisplayName,
        string userText,
        string conversationContext,
        string localSessionContext,
        CancellationToken cancellationToken = default);
}
