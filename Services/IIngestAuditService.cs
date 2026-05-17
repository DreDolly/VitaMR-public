using VitaMR.Models;

namespace VitaMR.Services;

public interface IIngestAuditService
{
    string AuditLogPath { get; }

    void Append(string vaultRoot, IEnumerable<IngestAuditEntry> entries);
}
