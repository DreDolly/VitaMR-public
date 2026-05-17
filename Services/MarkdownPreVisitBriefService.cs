using System.IO;
using System.Text;
using VitaMR.Models;

namespace VitaMR.Services;

public sealed class MarkdownPreVisitBriefService : IPreVisitBriefService
{
    public PreVisitBriefResult GenerateBrief(string vaultRoot, ChartContext chartContext)
    {
        var result = new PreVisitBriefResult
        {
            WasRun = true,
            Status = "PRE_VISIT_BRIEF_STARTED"
        };

        var wikiFolder = Path.Combine(vaultRoot, chartContext.ChartFolderName, "wiki");
        Directory.CreateDirectory(wikiFolder);
        EnsurePreVisitBriefFile(wikiFolder, chartContext);
        var path = Path.Combine(wikiFolder, "Pre_Visit_Brief.md");
        var builder = new StringBuilder();

        builder.AppendLine("---");
        builder.AppendLine($"chart_id: \"{chartContext.ChartId}\"");
        builder.AppendLine("document_type: \"Pre_Visit_Brief\"");
        builder.AppendLine($"generated_at: \"{DateTime.Now:O}\"");
        builder.AppendLine("status: generated");
        builder.AppendLine("---");
        builder.AppendLine();
        builder.AppendLine("# Pre-Visit Brief");
        builder.AppendLine();
        builder.AppendLine("> **Prototype safety note:** This brief compiles sterile VitaMR wiki content for visit preparation. Confirm high-risk details against source documents and clinicians.");
        builder.AppendLine();

        result.SectionsIncluded += AppendFileSection(builder, wikiFolder, "Open Care Gaps", "Care_Gaps.md", 12);
        result.SectionsIncluded += AppendFileSection(builder, wikiFolder, "Active Symptom Patterns", "Symptom_Patterns.md", 24);
        result.SectionsIncluded += AppendFileSection(builder, wikiFolder, "Medication Interaction Scan", "Drug_Interactions.md", 24);
        result.SectionsIncluded += AppendFileSection(builder, wikiFolder, "Health Goals", "Health_Goals.md", 18);
        result.SectionsIncluded += AppendFileSection(builder, wikiFolder, "Emergency Card Snapshot", "Emergency_Card.md", 24);
        result.SectionsIncluded += AppendFileSection(builder, wikiFolder, "Recent Timeline", "Timeline.md", 12);
        result.SectionsIncluded += AppendSpecialtySnapshot(builder, Path.Combine(wikiFolder, "specialty"));

        File.WriteAllText(path, Normalize(builder.ToString()));
        result.OutputPath = path;
        result.Status = "PRE_VISIT_BRIEF_GENERATED";
        result.Messages.Add($"Generated Pre_Visit_Brief.md with {result.SectionsIncluded} section(s).");
        SchedulerFileService.AppendScheduleLog(
            vaultRoot,
            "TASK-009",
            "Pre-Visit Brief",
            "manual",
            result.Status,
            $"chart={chartContext.ChartId}; sections={result.SectionsIncluded}");
        return result;
    }

    public static void EnsurePreVisitBriefFile(string wikiFolder, ChartContext chartContext)
    {
        Directory.CreateDirectory(wikiFolder);
        var path = Path.Combine(wikiFolder, "Pre_Visit_Brief.md");

        if (!File.Exists(path))
        {
            File.WriteAllText(
                path,
                Normalize(
                    $"---\nchart_id: \"{chartContext.ChartId}\"\ndocument_type: \"Pre_Visit_Brief\"\nstatus: scaffold\n---\n\n# Pre-Visit Brief\n\n> Generated on demand from sterile VitaMR wiki files.\n"));
        }
    }

    private static int AppendFileSection(StringBuilder builder, string wikiFolder, string title, string fileName, int maxLines)
    {
        var path = Path.Combine(wikiFolder, fileName);
        builder.AppendLine($"## {title}");
        builder.AppendLine();

        if (!File.Exists(path))
        {
            builder.AppendLine("_No file available yet._");
            builder.AppendLine();
            return 0;
        }

        var lines = File.ReadAllLines(path)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Where(line => !line.TrimStart().StartsWith("---", StringComparison.Ordinal))
            .Where(line => !line.StartsWith('#'))
            .Take(maxLines)
            .ToList();

        if (lines.Count == 0)
        {
            builder.AppendLine("_No entries available yet._");
        }
        else
        {
            foreach (var line in lines)
            {
                builder.AppendLine(line);
            }
        }

        builder.AppendLine();
        return 1;
    }

    private static int AppendSpecialtySnapshot(StringBuilder builder, string specialtyFolder)
    {
        builder.AppendLine("## Specialty Snapshot");
        builder.AppendLine();

        if (!Directory.Exists(specialtyFolder))
        {
            builder.AppendLine("_No specialty files available yet._");
            builder.AppendLine();
            return 0;
        }

        var rows = Directory.EnumerateFiles(specialtyFolder, "*.md")
            .SelectMany(path => File.ReadLines(path)
                .Where(line => line.StartsWith('|') && !line.Contains("---", StringComparison.Ordinal))
                .Skip(2)
                .Take(2)
                .Select(line => $"- {Path.GetFileNameWithoutExtension(path)}: {line}"))
            .Take(12)
            .ToList();

        if (rows.Count == 0)
        {
            builder.AppendLine("_No specialty entries available yet._");
        }
        else
        {
            foreach (var row in rows)
            {
                builder.AppendLine(row);
            }
        }

        builder.AppendLine();
        return 1;
    }

    private static string Normalize(string content)
    {
        return content.Replace("\r\n", "\n").TrimEnd() + Environment.NewLine;
    }
}
