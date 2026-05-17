using VitaMR.Models;
using VitaMR.ViewModels;

namespace VitaMR.Services;

public interface IOmniboxPreflightService
{
    Task<OmniboxPreflightResult> ReviewAsync(
        string endpoint,
        string modelName,
        string userText,
        string activeDisplayName,
        IReadOnlyList<AttachmentViewModel> attachments,
        IReadOnlyList<PatientIdentityRecord> knownPatients,
        string conversationContext,
        CancellationToken cancellationToken = default);
}
