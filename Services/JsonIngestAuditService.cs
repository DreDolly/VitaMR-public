using System.IO;
using System.Text.Json;
using VitaMR.Models;

namespace VitaMR.Services;

public sealed class JsonIngestAuditService : IIngestAuditService
{
    private const string SystemFolderName = "_System";
    private const string AuditFileName = "Ingest_Audit_Log.jsonl";

    public string AuditLogPath { get; private set; } = string.Empty;

    public void Append(string vaultRoot, IEnumerable<IngestAuditEntry> entries)
    {
        var entryList = entries.ToList();

        if (entryList.Count == 0)
        {
            return;
        }

        var systemFolder = Path.Combine(vaultRoot, SystemFolderName);
        Directory.CreateDirectory(systemFolder);

        AuditLogPath = Path.Combine(systemFolder, AuditFileName);

        var lines = entryList.Select(entry => JsonSerializer.Serialize(entry));
        File.AppendAllLines(AuditLogPath, lines);
    }
}
