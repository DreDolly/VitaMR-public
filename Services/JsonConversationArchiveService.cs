using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using VitaMR.Models;

namespace VitaMR.Services;

public sealed class JsonConversationArchiveService : IConversationArchiveService
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = false
    };

    private int _turn;

    public JsonConversationArchiveService()
    {
        SessionId = DateTime.Now.ToString("yyyyMMdd-HHmmss");
    }

    public string SessionId { get; }

    public string RawLogPath { get; private set; } = string.Empty;

    public string ScrubbedLogPath { get; private set; } = string.Empty;

    public void Append(
        string vaultRoot,
        MessageAuthor author,
        string label,
        IEnumerable<string> paragraphs,
        IEnumerable<ConversationAttachmentLog> attachments,
        string linkedChartId,
        string mode,
        bool isTestData,
        IReadOnlyList<PatientIdentityRecord> knownPatients)
    {
        if (string.IsNullOrWhiteSpace(vaultRoot))
        {
            return;
        }

        EnsurePaths(vaultRoot, isTestData);

        var attachmentList = attachments.ToList();
        var paragraphList = paragraphs.ToList();
        var entry = new ConversationLogEntry
        {
            Timestamp = DateTime.Now,
            SessionId = SessionId,
            Turn = ++_turn,
            Author = author.ToString(),
            Label = label,
            Paragraphs = paragraphList,
            Attachments = attachmentList,
            LinkedChartId = linkedChartId,
            Mode = mode,
            IsTestData = isTestData
        };

        File.AppendAllLines(RawLogPath, [JsonSerializer.Serialize(entry, _jsonOptions)]);

        var scrubbedEntry = new ConversationLogEntry
        {
            Timestamp = entry.Timestamp,
            SessionId = entry.SessionId,
            Turn = entry.Turn,
            Author = entry.Author,
            Label = Deidentify(entry.Label, knownPatients),
            Paragraphs = entry.Paragraphs.Select(paragraph => Deidentify(paragraph, knownPatients)).ToList(),
            Attachments = entry.Attachments.Select(attachment => new ConversationAttachmentLog
            {
                DisplayName = Deidentify(attachment.DisplayName, knownPatients),
                Path = "[local attachment path]",
                Kind = attachment.Kind
            }).ToList(),
            LinkedChartId = entry.LinkedChartId,
            Mode = entry.Mode,
            IsTestData = entry.IsTestData
        };

        File.AppendAllLines(ScrubbedLogPath, [JsonSerializer.Serialize(scrubbedEntry, _jsonOptions)]);
    }

    private void EnsurePaths(string vaultRoot, bool isTestData)
    {
        var conversationFolder = Path.Combine(vaultRoot, "_System", "conversations");
        var rawFolder = Path.Combine(conversationFolder, "raw");
        var scrubbedFolder = Path.Combine(conversationFolder, "scrubbed");
        Directory.CreateDirectory(rawFolder);
        Directory.CreateDirectory(scrubbedFolder);

        RawLogPath = Path.Combine(rawFolder, $"{SessionId}.raw.jsonl");
        ScrubbedLogPath = Path.Combine(scrubbedFolder, $"{SessionId}.scrubbed.jsonl");

        EnsureIndex(conversationFolder);
        RegisterManifestEntry(vaultRoot, RawLogPath, "raw_conversation_log", isTestData);
        RegisterManifestEntry(vaultRoot, ScrubbedLogPath, "scrubbed_conversation_log", isTestData);
    }

    private static void EnsureIndex(string conversationFolder)
    {
        var indexPath = Path.Combine(conversationFolder, "index.md");

        if (!File.Exists(indexPath))
        {
            File.WriteAllText(
                indexPath,
                "# Conversation Archive\n\nRaw logs are pilot/build artifacts. Scrubbed logs are de-identified timelines for future Dolly context.\n");
        }
    }

    private static void RegisterManifestEntry(string vaultRoot, string path, string artifactType, bool isTestData)
    {
        var manifestPath = Path.Combine(vaultRoot, "_System", "Test_Data_Manifest.json");

        if (!isTestData || !File.Exists(manifestPath))
        {
            return;
        }

        try
        {
            var root = JsonNode.Parse(File.ReadAllText(manifestPath)) as JsonObject ?? new JsonObject();
            var entries = root["entries"] as JsonArray ?? new JsonArray();
            root["entries"] = entries;

            var relativePath = Path.GetRelativePath(vaultRoot, path).Replace('\\', '/');
            var alreadyRegistered = entries
                .OfType<JsonObject>()
                .Any(entry => string.Equals(entry["path"]?.GetValue<string>(), relativePath, StringComparison.OrdinalIgnoreCase));

            if (alreadyRegistered)
            {
                return;
            }

            entries.Add(new JsonObject
            {
                ["path"] = relativePath,
                ["artifactType"] = artifactType,
                ["isTestData"] = true,
                ["deleteEligible"] = true,
                ["createdAt"] = DateTime.Now.ToString("O")
            });

            File.WriteAllText(manifestPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // Conversation logging must not block the app if the build-mode manifest is malformed.
        }
    }

    private static string Deidentify(string value, IReadOnlyList<PatientIdentityRecord> knownPatients)
    {
        var scrubbed = value;

        foreach (var patient in knownPatients)
        {
            if (!string.IsNullOrWhiteSpace(patient.PatientDisplayName))
            {
                scrubbed = Regex.Replace(
                    scrubbed,
                    Regex.Escape(patient.PatientDisplayName),
                    "[PATIENT]",
                    RegexOptions.IgnoreCase);
            }
        }

        scrubbed = Regex.Replace(scrubbed, @"\bVITA-\d{4,6}\b", "[CHART_ID]", RegexOptions.IgnoreCase);
        scrubbed = Regex.Replace(scrubbed, @"\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b", "[EMAIL]", RegexOptions.IgnoreCase);
        scrubbed = Regex.Replace(scrubbed, @"\b\d{3}[-.)\s]*\d{3}[-.\s]*\d{4}\b", "[PHONE]");

        return scrubbed;
    }
}
