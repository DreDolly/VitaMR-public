using VitaMR.Models;

namespace VitaMR.Services;

public interface IConversationArchiveService
{
    string SessionId { get; }

    string RawLogPath { get; }

    string ScrubbedLogPath { get; }

    void Append(
        string vaultRoot,
        MessageAuthor author,
        string label,
        IEnumerable<string> paragraphs,
        IEnumerable<ConversationAttachmentLog> attachments,
        string linkedChartId,
        string mode,
        bool isTestData,
        IReadOnlyList<PatientIdentityRecord> knownPatients);
}
