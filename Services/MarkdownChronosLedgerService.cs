using System.Globalization;
using System.IO;
using System.Text;
using VitaMR.Models;

namespace VitaMR.Services;

public sealed class MarkdownChronosLedgerService : IChronosLedgerService
{
    private const string LedgerFileName = "Chronos_Ledger.md";

    public void EnsureLedger(string vaultRoot)
    {
        var path = GetLedgerPath(vaultRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        if (File.Exists(path))
        {
            return;
        }

        File.WriteAllText(path, """
        # Chronos Ledger

        Append-only sterile temporal stream for Dolly's session grounding.

        | Local Time | UTC Time | Chart | Actor | Type | Source | Visibility | Summary |
        |---|---|---|---|---|---|---|---|
        """ + Environment.NewLine);
    }

    public void RecordEvent(
        string vaultRoot,
        ChartContext? chartContext,
        string eventType,
        string summary,
        string actor = "C#",
        string source = "VitaMR",
        string visibility = "sterile")
    {
        if (string.IsNullOrWhiteSpace(vaultRoot) ||
            string.IsNullOrWhiteSpace(eventType) ||
            string.IsNullOrWhiteSpace(summary))
        {
            return;
        }

        EnsureLedger(vaultRoot);
        var now = DateTimeOffset.Now;
        var chart = chartContext is null
            ? "family-vault"
            : $"{chartContext.ChartId} / {FirstName(chartContext.LocalDisplayName)}";
        var row = string.Join(
            " | ",
            [
                Escape(now.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture)),
                Escape(now.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture)),
                Escape(chart),
                Escape(actor),
                Escape(Normalize(eventType)),
                Escape(Normalize(source)),
                Escape(Normalize(visibility)),
                Escape(Normalize(summary))
            ]);

        File.AppendAllText(GetLedgerPath(vaultRoot), $"| {row} |{Environment.NewLine}");
    }

    public string BuildRecentContext(
        string vaultRoot,
        ChartContext? chartContext,
        int maxEntries = 12)
    {
        EnsureLedger(vaultRoot);
        var path = GetLedgerPath(vaultRoot);
        var chartId = chartContext?.ChartId ?? string.Empty;
        var rows = File.ReadAllLines(path)
            .Where(line => line.StartsWith('|') && !line.StartsWith("|---", StringComparison.Ordinal))
            .Skip(1)
            .Where(line => string.IsNullOrWhiteSpace(chartId) ||
                           line.Contains(chartId, StringComparison.OrdinalIgnoreCase) ||
                           line.Contains("family-vault", StringComparison.OrdinalIgnoreCase))
            .TakeLast(Math.Max(1, maxEntries))
            .ToList();

        if (rows.Count == 0)
        {
            return "No Chronos ledger events recorded yet.";
        }

        var builder = new StringBuilder();
        builder.AppendLine("Recent sterile temporal events, newest last:");

        foreach (var row in rows)
        {
            builder.AppendLine($"- {HumanizeRow(row)}");
        }

        return builder.ToString().TrimEnd();
    }

    private static string GetLedgerPath(string vaultRoot)
    {
        return Path.Combine(vaultRoot, "_System", LedgerFileName);
    }

    private static string HumanizeRow(string row)
    {
        var parts = row.Trim().Trim('|').Split('|', StringSplitOptions.TrimEntries);

        if (parts.Length < 8)
        {
            return row.Trim();
        }

        return $"{parts[0]} - {parts[4]} - {parts[7]}";
    }

    private static string FirstName(string displayName)
    {
        var parts = displayName
            .Trim()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return parts.Length == 0 ? displayName.Trim() : parts[0];
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "unknown"
            : value.ReplaceLineEndings(" ").Trim();
    }

    private static string Escape(string value)
    {
        return Normalize(value).Replace("|", "/", StringComparison.Ordinal);
    }
}
