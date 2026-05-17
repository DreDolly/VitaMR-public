using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using VitaMR.Models;

namespace VitaMR.Services;

public sealed class MarkdownTopicPageWriterService : ITopicPageWriterService
{
    private static readonly IReadOnlyDictionary<string, string[]> SectionMap = new Dictionary<string, string[]>
    {
        ["diagnosis"] = ["Diagnoses", "Assessment & Plan"],
        ["medication"] = ["Active Medications", "Assessment & Plan"],
        ["lab"] = ["Labs / Results"],
        ["imaging"] = ["Imaging"],
        ["pending"] = ["Pending Items"],
        ["general"] = ["Clinical Gestalt", "Assessment & Plan"]
    };

    public IReadOnlyList<string> WriteTopicPages(
        string vaultRoot,
        ChartContext chartContext,
        EncounterExtractionResult extraction,
        string encounterNodePath)
    {
        if (string.IsNullOrWhiteSpace(encounterNodePath) || !File.Exists(encounterNodePath))
        {
            return [];
        }

        var wikiFolder = Path.Combine(vaultRoot, chartContext.ChartFolderName, "wiki");
        Directory.CreateDirectory(wikiFolder);

        var encounterNodeName = Path.GetFileNameWithoutExtension(encounterNodePath);
        var date = NormalizeDate(extraction.DateOfService);
        var topics = BuildTopicCandidates(extraction)
            .GroupBy(topic => topic.Slug, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Take(24)
            .ToList();
        var writtenPaths = new List<string>();

        foreach (var topic in topics)
        {
            var topicPath = Path.Combine(wikiFolder, $"{topic.Slug}.md");
            EnsureTopicPage(topicPath, chartContext, topic, extraction);
            var updated = AddEncounterSection(topicPath, topic, extraction, encounterNodeName, date);

            if (updated)
            {
                RegisterLinks(vaultRoot, chartContext, topicPath, encounterNodeName, topic.Sections, date);
            }

            writtenPaths.Add(topicPath);
        }

        return writtenPaths;
    }

    private static IReadOnlyList<TopicCandidate> BuildTopicCandidates(EncounterExtractionResult extraction)
    {
        var topics = new List<TopicCandidate>();

        topics.AddRange(extraction.ProposedTopicPages.Select(topic => BuildTopic(topic, "general")));
        topics.AddRange(extraction.Diagnoses.Select(topic => BuildTopic(topic, "diagnosis")));
        topics.AddRange(extraction.ActiveMedications.Select(topic => BuildTopic(topic, "medication")));
        topics.AddRange(extraction.LabsResults.Select(topic => BuildTopic(topic, "lab")));
        topics.AddRange(extraction.Imaging.Select(topic => BuildTopic(topic, "imaging")));
        topics.AddRange(extraction.PendingItems.Select(topic => BuildTopic(topic, "pending")));

        return topics
            .Where(topic => !string.IsNullOrWhiteSpace(topic.Slug))
            .ToList();
    }

    private static TopicCandidate BuildTopic(string value, string category)
    {
        var displayName = ExtractDisplayName(value);
        var slug = BuildSafeSlug(displayName);
        var sections = SectionMap.TryGetValue(category, out var mappedSections)
            ? mappedSections
            : SectionMap["general"];

        return new TopicCandidate(displayName, slug, category, sections);
    }

    private static void EnsureTopicPage(
        string topicPath,
        ChartContext chartContext,
        TopicCandidate topic,
        EncounterExtractionResult extraction)
    {
        if (File.Exists(topicPath))
        {
            return;
        }

        var builder = new StringBuilder();
        builder.AppendLine("---");
        builder.AppendLine($"chart_id: \"{chartContext.ChartId}\"");
        builder.AppendLine("document_type: \"Topic_Page\"");
        builder.AppendLine($"topic_category: \"{topic.Category}\"");
        builder.AppendLine($"risk_level: {NormalizeRiskLevel(extraction.RiskLevel)}");
        builder.AppendLine($"dolly_directive: \"{NormalizeDirective(extraction.DollyDirective, extraction.RiskLevel)}\"");
        builder.AppendLine("conflict_status: none");
        builder.AppendLine("status: unverified");
        builder.AppendLine("last_auditor: none");
        builder.AppendLine("audit_date: none");
        builder.AppendLine("---");
        builder.AppendLine();
        builder.AppendLine($"# {topic.DisplayName}");
        builder.AppendLine();
        builder.AppendLine($"> **Summary:** Longitudinal topic page for {topic.DisplayName}. This summary is maintained from sterile encounter nodes.");
        builder.AppendLine();
        builder.AppendLine("## Encounter Links");
        builder.AppendLine();

        File.WriteAllText(topicPath, NormalizeContent(builder.ToString()));
    }

    private static bool AddEncounterSection(
        string topicPath,
        TopicCandidate topic,
        EncounterExtractionResult extraction,
        string encounterNodeName,
        string date)
    {
        var content = File.ReadAllText(topicPath);
        var marker = $"<!-- encounter:{encounterNodeName};topic:{topic.Slug} -->";

        if (content.Contains(marker, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var builder = new StringBuilder(content.TrimEnd());
        builder.AppendLine();
        builder.AppendLine();
        builder.AppendLine($"## Encounter: {date} - {extraction.DocumentType}");
        builder.AppendLine(marker);

        foreach (var section in topic.Sections)
        {
            builder.AppendLine($"![[{encounterNodeName}#{section}]]");
        }

        builder.AppendLine($"*Source: [[encounters/{encounterNodeName}]]*");

        File.WriteAllText(topicPath, NormalizeContent(builder.ToString()));
        return true;
    }

    private static void RegisterLinks(
        string vaultRoot,
        ChartContext chartContext,
        string topicPath,
        string encounterNodeName,
        IEnumerable<string> sections,
        string date)
    {
        var registryPath = Path.Combine(vaultRoot, "_Link_Registry.md");
        EnsureLinkRegistry(registryPath);
        var registryContent = File.ReadAllText(registryPath);
        var sourcePage = $"wiki/{Path.GetFileName(topicPath)}";
        var rows = new List<string>();

        foreach (var section in sections)
        {
            var rowKey = $"| {sourcePage} | wiki/encounters/{encounterNodeName}.md | {section} | {chartContext.ChartId} | {date} |";

            if (registryContent.Contains(rowKey, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            rows.Add($"{rowKey} topic transclusion | active |");
        }

        if (rows.Count > 0)
        {
            File.AppendAllLines(registryPath, rows);
        }
    }

    private static void EnsureLinkRegistry(string registryPath)
    {
        if (File.Exists(registryPath))
        {
            var content = File.ReadAllText(registryPath);

            if (content.Contains("| Source Page | Transcluded Target | Header Anchor | Chart ID | Date | Notes | Status |", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            File.AppendAllText(
                registryPath,
                Environment.NewLine +
                NormalizeContent(
                    """

                    | Source Page | Transcluded Target | Header Anchor | Chart ID | Date | Notes | Status |
                    |---|---|---|---|---|---|---|
                    """));
            return;
        }

        File.WriteAllText(
            registryPath,
            NormalizeContent(
                """
                # Link Registry

                | Source Page | Transcluded Target | Header Anchor | Chart ID | Date | Notes | Status |
                |---|---|---|---|---|---|---|
                """));
    }

    private static string ExtractDisplayName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var cleaned = Regex.Replace(value.Trim(), @"\[[^\]]+\]", string.Empty);
        cleaned = Regex.Replace(cleaned, @"\([^)]*\)", string.Empty);
        cleaned = cleaned.Split([':', ';', '|', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? cleaned;
        cleaned = Regex.Replace(cleaned, @"\b(active|new|stable|pending|possible|probable|history of|diagnosis|medication|lab|result)\b", string.Empty, RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim(' ', '.', ',', '-');

        return cleaned.Length <= 80 ? cleaned : cleaned[..80].Trim();
    }

    private static string BuildSafeSlug(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var slug = Regex.Replace(value.Trim(), @"[^A-Za-z0-9]+", "_").Trim('_');
        return string.IsNullOrWhiteSpace(slug) ? string.Empty : slug;
    }

    private static string NormalizeDate(string value)
    {
        return DateOnly.TryParse(value, out var parsed)
            ? parsed.ToString("yyyy-MM-dd")
            : DateTime.Now.ToString("yyyy-MM-dd");
    }

    private static string NormalizeRiskLevel(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        return normalized is "high" or "moderate" or "low" ? normalized : "low";
    }

    private static string NormalizeDirective(string value, string riskLevel)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value.Trim();
        }

        return NormalizeRiskLevel(riskLevel) switch
        {
            "high" => "MANDATORY_LOCAL_RAW_OR_SCRUBBED_PULL",
            "moderate" => "CONTEXTUAL_TRUST",
            _ => "WIKI_NATIVE"
        };
    }

    private static string NormalizeContent(string content)
    {
        return content.Replace("\r\n", "\n").TrimEnd() + Environment.NewLine;
    }

    private sealed record TopicCandidate(
        string DisplayName,
        string Slug,
        string Category,
        IReadOnlyList<string> Sections);
}
