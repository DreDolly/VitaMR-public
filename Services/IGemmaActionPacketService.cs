using VitaMR.Models;

namespace VitaMR.Services;

public interface IGemmaActionPacketService
{
    Task<GemmaActionPacketResult> BuildPacketAsync(
        string endpoint,
        string modelName,
        string userText,
        string conversationContext,
        string localSessionContext,
        string activePatientDisplayName,
        IReadOnlyList<PatientIdentityRecord> knownPatients,
        CancellationToken cancellationToken = default);
}
