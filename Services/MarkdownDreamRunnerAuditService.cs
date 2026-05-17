using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using VitaMR.Models;

namespace VitaMR.Services;

public sealed class MarkdownDreamRunnerAuditService : IDreamRunnerAuditService
{
    private static readonly string[] RequiredWikiFiles =
    [
        "Index.md",
        "Timeline.md",
        "Care_Gaps.md",
        "Conflicts.md",
        "Emergency_Card.md"
    ];

    public DreamRunnerAuditResult AuditChart(string vaultRoot, ChartContext chartContext)
    {
        var result = new DreamRunnerAuditResult
        {
            WasRun = true,
            ChartId = chartContext.ChartId,
            Status = "DREAM_RUNNER_STARTED"
        };

        var chartRoot = Path.Combine(vaultRoot, chartContext.ChartFolderName);
        var wikiFolder = Path.Combine(chartRoot, "wiki");
        Directory.CreateDirectory(wikiFolder);

        foreach (var requiredFile in RequiredWikiFiles)
        {
            var path = Path.Combine(wikiFolder, requiredFile);

            if (!File.Exists(path))
            {
                result.MissingRequiredFiles++;
                AddFinding(result, "warning", path, $"Required wiki file is missing: {requiredFile}");
            }
        }

        var markdownFiles = Directory.Exists(wikiFolder)
            ? Directory.GetFiles(wikiFolder, "*.md", SearchOption.AllDirectories)
            : [];

        result.FilesChecked = markdownFiles.Length;

        foreach (var file in markdownFiles)
        {
            AuditFile(vaultRoot, chartRoot, wikiFolder, file, result);
        }

        result.OpenCareGaps = CountTableRowsWithStatus(Path.Combine(wikiFolder, "Care_Gaps.md"), 3, "open");
        result.ActiveConflicts = CountTableRowsWithStatus(Path.Combine(wikiFolder, "Conflicts.md"), 3, "active");
        result.Status = DetermineStatus(result);
        result.LogPath = AppendLog(wikiFolder, chartContext, result);
        return result;
    }

    private static void AuditFile(
        string vaultRoot,
        string chartRoot,
        string wikiFolder,
        string file,
        DreamRunnerAuditResult result)
    {
        var content = File.ReadAllText(file);
        var relativePath = NormalizeRelativePath(Path.GetRelativePath(chartRoot, file));

        if (content.Contains("status: unverified", StringComparison.OrdinalIgnoreCase))
        {
            result.UnverifiedPages++;
            AddFinding(result, "info", relativePath, "Page is still marked unverified.");
        }

        if (IsStructuredWikiPage(relativePath) && !content.TrimStart().StartsWith("---", StringComparison.Ordinal))
        {
            AddFinding(result, "warning", relativePath, "Structured wiki page is missing YAML frontmatter.");
        }

        foreach (var link in ExtractWikiLinks(content))
        {
            if (!CanResolveLink(vaultRoot, chartRoot, wikiFolder, link))
            {
                result.BrokenLinks++;
                AddFinding(result, "warning", relativePath, $"Possible broken Obsidian link: [[{link}]]");
            }
        }
    }

    private static IEnumerable<string> ExtractWikiLinks(string content)
    {
        foreach (Match match in Regex.Matches(content, @"\[\[(?<target>[^\]#|]+)(?:[#|][^\]]*)?\]\]"))
        {
            var target = match.Groups["target"].Value.Trim();

            if (!string.IsNullOrWhiteSpace(target))
            {
                yield return target;
            }
        }
    }

    private static bool CanResolveLink(string vaultRoot, string chartRoot, string wikiFolder, string link)
    {
        var normalized = NormalizeRelativePath(link);
        var candidates = new List<string>();

        if (normalized.StartsWith("raw/", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("scrubbed/", StringComparison.OrdinalIgnoreCase))
        {
            candidates.Add(Path.Combine(chartRoot, normalized.Replace('/', Path.DirectorySeparatorChar)));
        }
        else if (normalized.StartsWith("wiki/", StringComparison.OrdinalIgnoreCase))
        {
            candidates.Add(Path.Combine(chartRoot, normalized.Replace('/', Path.DirectorySeparatorChar)));
        }
        else if (normalized.Contains('/', StringComparison.Ordinal))
        {
            candidates.Add(Path.Combine(wikiFolder, normalized.Replace('/', Path.DirectorySeparatorChar)));
            candidates.Add(Path.Combine(chartRoot, normalized.Replace('/', Path.DirectorySeparatorChar)));
        }
        else
        {
            candidates.Add(Path.Combine(wikiFolder, normalized));
            candidates.Add(Path.Combine(vaultRoot, normalized));
        }

        return candidates.Any(ExistsWithOptionalMarkdownExtension);
    }

    private static bool ExistsWithOptionalMarkdownExtension(string path)
    {
        if (File.Exists(path) || Directory.Exists(path))
        {
            return true;
        }

        return string.IsNullOrWhiteSpace(Path.GetExtension(path)) &&
               File.Exists(path + ".md");
    }

    private static int CountTableRowsWithStatus(string path, int statusColumn, string expectedStatus)
    {
        if (!File.Exists(path))
        {
            return 0;
        }

        return File.ReadAllLines(path)
            .Select(ParseTableRow)
            .Where(row => row.Count > statusColumn &&
                          row[statusColumn].Equals(expectedStatus, StringComparison.OrdinalIgnoreCase))
            .Count();
    }

    private static IReadOnlyList<string> ParseTableRow(string line)
    {
        var trimmed = line.Trim();

        if (!trimmed.StartsWith('|') || trimmed.Contains("---", StringComparison.Ordinal))
        {
            return [];
        }

        return trimmed.Trim('|')
            .Split('|')
            .Select(cell => cell.Trim())
            .ToList();
    }

    private static string AppendLog(string wikiFolder, ChartContext chartContext, DreamRunnerAuditResult result)
    {
        var path = Path.Combine(wikiFolder, "Dream_Runner_Log.md");
        var builder = new StringBuilder();

        if (!File.Exists(path))
        {
            builder.AppendLine("# Dream Runner Log");
            builder.AppendLine();
        }

        builder.AppendLine($"## {DateTime.Now:yyyy-MM-dd HH:mm} - {chartContext.ChartId}");
        builder.AppendLine();
        builder.AppendLine($"- Status: {result.Status}");
        builder.AppendLine($"- Files checked: {result.FilesChecked}");
        builder.AppendLine($"- Missing required files: {result.MissingRequiredFiles}");
        builder.AppendLine($"- Broken links: {result.BrokenLinks}");
        builder.AppendLine($"- Unverified pages: {result.UnverifiedPages}");
        builder.AppendLine($"- Open care gaps: {result.OpenCareGaps}");
        builder.AppendLine($"- Active conflicts: {result.ActiveConflicts}");
        builder.AppendLine();
        builder.AppendLine("| Severity | File | Finding |");
        builder.AppendLine("|---|---|---|");

        foreach (var finding in result.Findings.Take(50))
        {
            builder.AppendLine($"| {EscapeTable(finding.Severity)} | {EscapeTable(finding.FilePath)} | {EscapeTable(finding.Message)} |");
        }

        if (result.Findings.Count == 0)
        {
            builder.AppendLine("| info | chart | No report-only audit findings. |");
        }

        builder.AppendLine();
        File.AppendAllText(path, NormalizeContent(builder.ToString()));
        return path;
    }

    private static bool IsStructuredWikiPage(string relativePath)
    {
        return relativePath.StartsWith("wiki/encounters/", StringComparison.OrdinalIgnoreCase) ||
               relativePath.StartsWith("wiki/manual_updates/", StringComparison.OrdinalIgnoreCase) ||
               relativePath.Equals("wiki/Emergency_Card.md", StringComparison.OrdinalIgnoreCase);
    }

    private static string DetermineStatus(DreamRunnerAuditResult result)
    {
        if (result.MissingRequiredFiles > 0 || result.BrokenLinks > 0 || result.ActiveConflicts > 0)
        {
            return "DREAM_RUNNER_ATTENTION_NEEDED";
        }

        return result.UnverifiedPages > 0 || result.OpenCareGaps > 0
            ? "DREAM_RUNNER_OPEN_ITEMS"
            : "DREAM_RUNNER_CLEAR";
    }

    private static void AddFinding(DreamRunnerAuditResult result, string severity, string filePath, string message)
    {
        result.Findings.Add(new DreamRunnerAuditFinding
        {
            Severity = severity,
            FilePath = NormalizeRelativePath(filePath),
            Message = message
        });
    }

    private static string NormalizeRelativePath(string path)
    {
        return path.Replace('\\', '/').Trim();
    }

    private static string EscapeTable(string value)
    {
        return Regex.Replace(value.Replace("|", "/").Trim(), @"\s+", " ");
    }

    private static string NormalizeContent(string content)
    {
        return content.Replace("\r\n", "\n").TrimEnd() + Environment.NewLine;
    }
}
