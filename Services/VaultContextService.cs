using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using VitaMR.Models;

namespace VitaMR.Services;

public sealed class VaultContextService : IVaultContextService
{
    private const int MaxFileCharacters = 6000;
    private const int MaxTotalCharacters = 22000;

    public VaultContextPacket BuildSterileContext(
        string vaultRoot,
        ChartContext chartContext,
        string userQuestion = "")
    {
        var chartFolder = Path.Combine(vaultRoot, chartContext.ChartFolderName);
        var wikiFolder = Path.Combine(chartFolder, "wiki");
        var scrubbedFolder = Path.Combine(chartFolder, "scrubbed");
        var includedFiles = new List<string>();
        var sources = new List<VaultSourceSummary>();
        var knownFacts = new List<string>();
        var availableSources = new List<string>();
        var pendingItems = new List<string>();
        var missingOrUnavailable = new List<string>();
        var retrievalWarnings = new List<string>();
        var builder = new StringBuilder();

        AddWikiFile(
            builder,
            includedFiles,
            sources,
            knownFacts,
            pendingItems,
            Path.Combine(chartFolder, $"_Chart_{chartContext.ChartId}.md"),
            "chart_file");
        AddWikiFile(
            builder,
            includedFiles,
            sources,
            knownFacts,
            pendingItems,
            Path.Combine(chartFolder, $"_Persona_{chartContext.ChartId}.md"),
            "patient_persona");

        AddWikiFile(
            builder,
            includedFiles,
            sources,
            knownFacts,
            pendingItems,
            Path.Combine(wikiFolder, "Index.md"),
            "index");
        AddWikiFile(
            builder,
            includedFiles,
            sources,
            knownFacts,
            pendingItems,
            Path.Combine(wikiFolder, "Timeline.md"),
            "timeline");
        AddWikiFile(
            builder,
            includedFiles,
            sources,
            knownFacts,
            pendingItems,
            Path.Combine(wikiFolder, "Future_Data_Needed.md"),
            "future_data_needed");
        AddHighSignalLivingChartFiles(
            builder,
            includedFiles,
            sources,
            knownFacts,
            pendingItems,
            retrievalWarnings,
            wikiFolder);

        AddEncounterNodes(
            builder,
            includedFiles,
            sources,
            knownFacts,
            pendingItems,
            retrievalWarnings,
            Path.Combine(wikiFolder, "encounters"),
            userQuestion);
        AddManualUpdates(
            builder,
            includedFiles,
            sources,
            knownFacts,
            wikiFolder,
            userQuestion);
        AddTopicPages(
            builder,
            includedFiles,
            sources,
            knownFacts,
            wikiFolder,
            userQuestion);
        AddFinalScrubbedPayloadSummaries(
            builder,
            includedFiles,
            sources,
            availableSources,
            scrubbedFolder);

        if (Directory.Exists(Path.Combine(chartFolder, "raw")))
        {
            missingOrUnavailable.Add("Raw files exist but are not included in normal Dolly conversation context.");
        }

        if (sources.Count == 0)
        {
            missingOrUnavailable.Add("No sterile wiki, encounter, or final scrubbed files are available for this chart yet.");
        }

        if (!sources.Any(source => source.SourceType.Equals("encounter", StringComparison.OrdinalIgnoreCase)))
        {
            missingOrUnavailable.Add("No encounter nodes have been written yet.");
        }

        var contextText = BuildStructuredContextText(
            chartContext,
            sources,
            knownFacts,
            availableSources,
            pendingItems,
            missingOrUnavailable,
            retrievalWarnings,
            builder.ToString());
        var apiContextPath = WriteApiContextOutline(wikiFolder, chartContext, contextText);

        if (!string.IsNullOrWhiteSpace(apiContextPath))
        {
            includedFiles.Add(apiContextPath);
        }

        return new VaultContextPacket
        {
            ChartId = chartContext.ChartId,
            PatientDisplayName = chartContext.LocalDisplayName,
            Status = sources.Count == 0
                ? "VAULT_CONTEXT_EMPTY"
                : "VAULT_CONTEXT_READY",
            KnownPatients = [chartContext.LocalDisplayName],
            IncludedFiles = includedFiles,
            Sources = sources,
            KnownFacts = RankContextLines(knownFacts.Distinct(StringComparer.OrdinalIgnoreCase), userQuestion).Take(20).ToList(),
            AvailableSources = availableSources.Distinct(StringComparer.OrdinalIgnoreCase).Take(20).ToList(),
            PendingItems = RankContextLines(pendingItems.Distinct(StringComparer.OrdinalIgnoreCase), userQuestion).Take(20).ToList(),
            MissingOrUnavailableData = missingOrUnavailable.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            RetrievalWarnings = retrievalWarnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            ContextText = Truncate(contextText, MaxTotalCharacters)
        };
    }

    private static string WriteApiContextOutline(string wikiFolder, ChartContext chartContext, string contextText)
    {
        try
        {
            Directory.CreateDirectory(wikiFolder);
            var path = Path.Combine(wikiFolder, "Dolly_API_Context.md");
            File.WriteAllText(
                path,
                $"# Dolly API Context: {chartContext.ChartId}{Environment.NewLine}{Environment.NewLine}" +
                "> C#-built sterile context outline for API chart answers. Raw source files are not included." +
                $"{Environment.NewLine}{Environment.NewLine}" +
                contextText);
            return path;
        }
        catch (IOException)
        {
            return string.Empty;
        }
        catch (UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }

    public VaultContextPacket BuildFamilyVaultRosterContext(
        string vaultRoot,
        IReadOnlyList<PatientIdentityRecord> knownPatients)
    {
        var builder = new StringBuilder();
        var includedFiles = new List<string>();
        var sources = new List<VaultSourceSummary>();
        var activePatients = knownPatients
            .Where(patient => !string.IsNullOrWhiteSpace(patient.PatientDisplayName))
            .Where(patient => Directory.Exists(Path.Combine(vaultRoot, patient.ChartId)))
            .ToList();
        var patientNames = activePatients
            .OrderBy(patient => patient.PatientDisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(patient => patient.PatientDisplayName.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        builder.AppendLine("Family vault roster from the sealed C# registry.");
        builder.AppendLine("Internal chart IDs are provided for routing only. Do not reveal chart IDs to the user.");
        builder.AppendLine();

        if (activePatients.Count == 0)
        {
            builder.AppendLine("No patients are registered yet.");
        }

        foreach (var patient in activePatients.OrderBy(patient => patient.PatientDisplayName, StringComparer.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(patient.PatientDisplayName))
            {
                continue;
            }

            var chartFolder = Path.Combine(vaultRoot, patient.ChartId);
            var wikiFolder = Path.Combine(chartFolder, "wiki");
            var scrubbedFolder = Path.Combine(chartFolder, "scrubbed");
            var rawFolder = Path.Combine(chartFolder, "raw");
            var availableParts = new List<string>();

            if (Directory.Exists(wikiFolder))
            {
                availableParts.Add("wiki");
                AddIfExists(includedFiles, sources, Path.Combine(wikiFolder, "Index.md"), "index");
                AddIfExists(includedFiles, sources, Path.Combine(wikiFolder, "Timeline.md"), "timeline");
                AddIfExists(includedFiles, sources, Path.Combine(wikiFolder, "Future_Data_Needed.md"), "future_data_needed");
            }

            if (Directory.Exists(scrubbedFolder))
            {
                availableParts.Add("scrubbed");
            }

            if (Directory.Exists(rawFolder))
            {
                availableParts.Add("raw present but not allowed for Dolly conversation");
            }

            var availableText = availableParts.Count == 0
                ? "no chart folders found yet"
                : string.Join(", ", availableParts);

            builder.AppendLine($"- Patient display name: {patient.PatientDisplayName}");
            builder.AppendLine($"  Internal routing id: {patient.ChartId}");
            builder.AppendLine($"  Available chart areas: {availableText}");
        }

        return new VaultContextPacket
        {
            ChartId = "FAMILY_VAULT",
            PatientDisplayName = "Family vault",
            Status = patientNames.Count == 0
                ? "FAMILY_VAULT_ROSTER_EMPTY"
                : "FAMILY_VAULT_ROSTER_READY",
            KnownPatients = patientNames,
            IncludedFiles = includedFiles,
            Sources = sources,
            AvailableSources = sources.Select(source => $"{source.SourceType}: {source.Citation}").ToList(),
            ContextText = Truncate(builder.ToString(), MaxTotalCharacters)
        };
    }

    private static void AddWikiFile(
        StringBuilder builder,
        ICollection<string> includedFiles,
        ICollection<VaultSourceSummary> sources,
        ICollection<string> knownFacts,
        ICollection<string> pendingItems,
        string path,
        string sourceType)
    {
        if (!File.Exists(path))
        {
            return;
        }

        var text = Truncate(File.ReadAllText(path), MaxFileCharacters);

        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        includedFiles.Add(path);
        sources.Add(BuildSource(path, sourceType, ReadFrontmatter(text)));
        AddSummaryFacts(knownFacts, text, Path.GetFileName(path));
        AddPendingItems(pendingItems, text, Path.GetFileName(path));
        AppendSourceBlock(builder, path, text);
    }

    private static void AddEncounterNodes(
        StringBuilder builder,
        ICollection<string> includedFiles,
        ICollection<VaultSourceSummary> sources,
        ICollection<string> knownFacts,
        ICollection<string> pendingItems,
        ICollection<string> retrievalWarnings,
        string encountersFolder,
        string userQuestion)
    {
        if (!Directory.Exists(encountersFolder))
        {
            return;
        }

        foreach (var path in RankFiles(Directory.EnumerateFiles(encountersFolder, "*.md"), userQuestion).Take(8))
        {
            var text = Truncate(File.ReadAllText(path), MaxFileCharacters);
            var frontmatter = ReadFrontmatter(text);
            var source = BuildSource(path, "encounter", frontmatter);

            includedFiles.Add(path);
            sources.Add(source);
            AddSummaryFacts(knownFacts, text, Path.GetFileName(path));
            AddPendingItems(pendingItems, text, Path.GetFileName(path));
            AddRetrievalWarning(retrievalWarnings, source);
            AppendSourceBlock(builder, path, text);
        }
    }

    private static void AddHighSignalLivingChartFiles(
        StringBuilder builder,
        ICollection<string> includedFiles,
        ICollection<VaultSourceSummary> sources,
        ICollection<string> knownFacts,
        ICollection<string> pendingItems,
        ICollection<string> retrievalWarnings,
        string wikiFolder)
    {
        var files = new[]
        {
            ("Emergency_Card.md", "emergency_card"),
            ("Care_Gaps.md", "care_gaps"),
            ("Vaccines.md", "vaccines"),
            ("Conflicts.md", "conflicts"),
            ("Health_Goals.md", "health_goals"),
            ("Symptom_Patterns.md", "symptom_patterns"),
            ("Symptom_Journal.md", "symptom_journal"),
            ("Drug_Interactions.md", "drug_interactions"),
            ("Pre_Visit_Brief.md", "pre_visit_brief")
        };

        foreach (var (fileName, sourceType) in files)
        {
            var path = Path.Combine(wikiFolder, fileName);

            if (!File.Exists(path))
            {
                continue;
            }

            var text = Truncate(File.ReadAllText(path), MaxFileCharacters);

            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            var source = BuildSource(path, sourceType, ReadFrontmatter(text));
            includedFiles.Add(path);
            sources.Add(source);
            AddSummaryFacts(knownFacts, text, Path.GetFileName(path));
            AddHighSignalRows(knownFacts, pendingItems, text, Path.GetFileName(path));
            AddRetrievalWarning(retrievalWarnings, source);
            AppendSourceBlock(builder, path, text);
        }
    }

    private static void AddTopicPages(
        StringBuilder builder,
        ICollection<string> includedFiles,
        ICollection<VaultSourceSummary> sources,
        ICollection<string> knownFacts,
        string wikiFolder,
        string userQuestion)
    {
        if (!Directory.Exists(wikiFolder))
        {
            return;
        }

        var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Index.md",
            "Timeline.md",
            "Vaccines.md",
            "Future_Data_Needed.md"
        };

        var paths = Directory
            .EnumerateFiles(wikiFolder, "*.md")
            .Where(path => !excluded.Contains(Path.GetFileName(path)));

        foreach (var path in RankFiles(paths, userQuestion).Take(6))
        {
            var text = Truncate(File.ReadAllText(path), MaxFileCharacters);

            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            includedFiles.Add(path);
            sources.Add(BuildSource(path, "topic", ReadFrontmatter(text)));
            AddSummaryFacts(knownFacts, text, Path.GetFileName(path));
            AppendSourceBlock(builder, path, text);
        }
    }

    private static void AddManualUpdates(
        StringBuilder builder,
        ICollection<string> includedFiles,
        ICollection<VaultSourceSummary> sources,
        ICollection<string> knownFacts,
        string wikiFolder,
        string userQuestion)
    {
        var manualUpdatesFolder = Path.Combine(wikiFolder, "manual_updates");

        if (!Directory.Exists(manualUpdatesFolder))
        {
            return;
        }

        foreach (var path in RankFiles(Directory.EnumerateFiles(manualUpdatesFolder, "*.md"), userQuestion).Take(8))
        {
            var text = Truncate(File.ReadAllText(path), MaxFileCharacters);

            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            includedFiles.Add(path);
            sources.Add(BuildSource(path, "manual_update", ReadFrontmatter(text)));
            AddSummaryFacts(knownFacts, text, Path.GetFileName(path));
            AppendSourceBlock(builder, path, text);
        }
    }

    private static void AddFinalScrubbedPayloadSummaries(
        StringBuilder builder,
        ICollection<string> includedFiles,
        ICollection<VaultSourceSummary> sources,
        ICollection<string> availableSources,
        string scrubbedFolder)
    {
        if (!Directory.Exists(scrubbedFolder))
        {
            return;
        }

        foreach (var path in Directory.EnumerateFiles(scrubbedFolder, "*.txt").OrderByDescending(File.GetLastWriteTime).Take(3))
        {
            includedFiles.Add(path);
            sources.Add(BuildSource(path, "final_scrubbed_payload", EmptyFrontmatter));
            availableSources.Add($"Final scrubbed payload available: {Path.GetFileName(path)}");
            AppendSourceBlock(builder, path, Truncate(File.ReadAllText(path), 2500));
        }
    }

    private static string BuildStructuredContextText(
        ChartContext chartContext,
        IReadOnlyList<VaultSourceSummary> sources,
        IReadOnlyCollection<string> knownFacts,
        IReadOnlyCollection<string> availableSources,
        IReadOnlyCollection<string> pendingItems,
        IReadOnlyCollection<string> missingOrUnavailable,
        IReadOnlyCollection<string> retrievalWarnings,
        string sourceText)
    {
        var builder = new StringBuilder();
        builder.AppendLine("C# sterile vault retrieval packet.");
        builder.AppendLine("Use this packet only. Do not claim to read raw files unless the packet explicitly says raw local verification was performed.");
        builder.AppendLine($"Chart ID for routing only: {chartContext.ChartId}");
        builder.AppendLine($"Local display name for user response: {chartContext.LocalDisplayName}");
        builder.AppendLine();
        AppendSection(builder, "Known Chart Facts", knownFacts);
        AppendSection(builder, "Available Source Files / Encounter Nodes", sources.Select(FormatSource));
        AppendSection(builder, "Available Scrubbed Payloads", availableSources);
        AppendSection(builder, "Missing / Pending / Blocked Data", pendingItems.Concat(missingOrUnavailable));
        AppendSection(builder, "Retrieval Warnings", retrievalWarnings);
        builder.AppendLine("## Source Excerpts");
        builder.AppendLine(sourceText);
        return builder.ToString();
    }

    private static VaultSourceSummary BuildSource(
        string path,
        string sourceType,
        IReadOnlyDictionary<string, string> frontmatter)
    {
        return new VaultSourceSummary
        {
            SourceType = sourceType,
            DisplayName = Path.GetFileName(path),
            Path = path,
            Citation = BuildCitation(path, sourceType),
            RiskLevel = ReadFrontmatterValue(frontmatter, "risk_level"),
            DollyDirective = ReadFrontmatterValue(frontmatter, "dolly_directive"),
            ConflictStatus = ReadFrontmatterValue(frontmatter, "conflict_status"),
            ReadingStatus = ReadFrontmatterValue(frontmatter, "reading_status"),
            Status = ReadFrontmatterValue(frontmatter, "status")
        };
    }

    private static string FormatSource(VaultSourceSummary source)
    {
        var parts = new List<string> { $"{source.SourceType}: {source.Citation}" };

        if (!string.IsNullOrWhiteSpace(source.RiskLevel))
        {
            parts.Add($"risk={source.RiskLevel}");
        }

        if (!string.IsNullOrWhiteSpace(source.DollyDirective))
        {
            parts.Add($"directive={source.DollyDirective}");
        }

        if (!string.IsNullOrWhiteSpace(source.ConflictStatus))
        {
            parts.Add($"conflict={source.ConflictStatus}");
        }

        if (!string.IsNullOrWhiteSpace(source.ReadingStatus))
        {
            parts.Add($"reading={source.ReadingStatus}");
        }

        return string.Join(" | ", parts);
    }

    private static string BuildCitation(string path, string sourceType)
    {
        var fileName = Path.GetFileNameWithoutExtension(path);
        return sourceType switch
        {
            "encounter" => $"[[encounters/{fileName}]]",
            "topic" => $"[[wiki/{fileName}]]",
            "index" => "[[wiki/Index]]",
            "timeline" => "[[wiki/Timeline]]",
            "future_data_needed" => "[[wiki/Future_Data_Needed]]",
            "final_scrubbed_payload" => $"[[scrubbed/{Path.GetFileName(path)}]]",
            _ => Path.GetFileName(path)
        };
    }

    private static void AddRetrievalWarning(ICollection<string> warnings, VaultSourceSummary source)
    {
        if (source.ConflictStatus.Equals("active", StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add($"Active conflict present in {source.Citation}. Surface conflict before synthesis.");
        }

        if (source.ConflictStatus.Equals("pending_human", StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add($"Human resolution pending in {source.Citation}. Do not synthesize from this node.");
        }

        if (source.RiskLevel.Equals("high", StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add($"{source.Citation} is high risk. Treat wiki text as a locator/summary; exact verification requires local raw or scrubbed source review.");
        }

        if (source.ReadingStatus.Equals("ai_preliminary_pending_formal", StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add($"{source.Citation} has an AI preliminary read pending a formal report.");
        }
    }

    private static void AddSummaryFacts(ICollection<string> knownFacts, string text, string sourceName)
    {
        foreach (var line in text.Replace("\r\n", "\n").Split('\n'))
        {
            var trimmed = line.Trim();

            if (trimmed.StartsWith("> **Summary:**", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("> **Clinical Summary:**", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("> **Clinical Gestalt:**", StringComparison.OrdinalIgnoreCase))
            {
                knownFacts.Add($"{sourceName}: {StripMarkdown(trimmed)}");
            }
        }
    }

    private static void AddPendingItems(ICollection<string> pendingItems, string text, string sourceName)
    {
        var inPendingSection = false;

        foreach (var line in text.Replace("\r\n", "\n").Split('\n'))
        {
            var trimmed = line.Trim();

            if (trimmed.StartsWith("## Pending Items", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("### Pending Items", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("# Future Data Needed", StringComparison.OrdinalIgnoreCase))
            {
                inPendingSection = true;
                continue;
            }

            if (inPendingSection && trimmed.StartsWith('#'))
            {
                inPendingSection = false;
            }

            if (!inPendingSection || string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("|---", StringComparison.Ordinal))
            {
                continue;
            }

            if (trimmed.StartsWith('-') || trimmed.StartsWith('|'))
            {
                pendingItems.Add($"{sourceName}: {StripMarkdown(trimmed)}");
            }
        }
    }

    private static void AddHighSignalRows(
        ICollection<string> knownFacts,
        ICollection<string> pendingItems,
        string text,
        string sourceName)
    {
        foreach (var row in text.Replace("\r\n", "\n").Split('\n').Select(ParseTableRow))
        {
            if (row.Count < 2 || row[0].Contains("ID", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var joined = string.Join(" | ", row);

            if (joined.Contains("---", StringComparison.Ordinal))
            {
                continue;
            }

            if (sourceName.Equals("Care_Gaps.md", StringComparison.OrdinalIgnoreCase) &&
                row.Count >= 4 &&
                row[3].Equals("open", StringComparison.OrdinalIgnoreCase))
            {
                pendingItems.Add($"{sourceName}: open care gap - {StripMarkdown(joined)}");
                continue;
            }

            if (sourceName.Equals("Conflicts.md", StringComparison.OrdinalIgnoreCase) &&
                joined.Contains("active", StringComparison.OrdinalIgnoreCase))
            {
                pendingItems.Add($"{sourceName}: active conflict - {StripMarkdown(joined)}");
                continue;
            }

            if (sourceName is "Health_Goals.md" or "Symptom_Patterns.md" or "Drug_Interactions.md")
            {
                knownFacts.Add($"{sourceName}: {StripMarkdown(joined)}");
            }
        }
    }

    private static IReadOnlyDictionary<string, string> ReadFrontmatter(string text)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var normalized = text.Replace("\r\n", "\n");

        if (!normalized.StartsWith("---\n", StringComparison.Ordinal))
        {
            return values;
        }

        var end = normalized.IndexOf("\n---", 4, StringComparison.Ordinal);

        if (end < 0)
        {
            return values;
        }

        foreach (var line in normalized[4..end].Split('\n'))
        {
            var separator = line.IndexOf(':');

            if (separator <= 0)
            {
                continue;
            }

            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim().Trim('"');

            if (!string.IsNullOrWhiteSpace(key))
            {
                values[key] = value;
            }
        }

        return values;
    }

    private static string ReadFrontmatterValue(IReadOnlyDictionary<string, string> frontmatter, string key)
    {
        return frontmatter.TryGetValue(key, out var value) ? value : string.Empty;
    }

    private static IReadOnlyDictionary<string, string> EmptyFrontmatter { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    private static IEnumerable<string> RankFiles(IEnumerable<string> paths, string userQuestion)
    {
        var queryTokens = Tokenize(userQuestion).ToHashSet(StringComparer.OrdinalIgnoreCase);

        return paths
            .Select(path => new
            {
                Path = path,
                Score = ScorePath(path, queryTokens),
                LastWrite = File.GetLastWriteTime(path)
            })
            .OrderByDescending(item => item.Score)
            .ThenByDescending(item => item.LastWrite)
            .Select(item => item.Path);
    }

    private static IEnumerable<string> RankContextLines(IEnumerable<string> lines, string userQuestion)
    {
        var queryTokens = Tokenize(userQuestion).ToHashSet(StringComparer.OrdinalIgnoreCase);

        return lines
            .Select((line, index) => new
            {
                Line = line,
                Score = ScoreContextLine(line, queryTokens),
                Index = index
            })
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Index)
            .Select(item => item.Line);
    }

    private static int ScoreContextLine(string line, ISet<string> queryTokens)
    {
        var score = 0;

        if (line.Contains("Emergency_Card", StringComparison.OrdinalIgnoreCase))
        {
            score += 10;
        }

        if (line.Contains("Care_Gaps", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("Conflicts", StringComparison.OrdinalIgnoreCase))
        {
            score += 8;
        }

        if (line.Contains("active", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("open", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("high", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("urgent", StringComparison.OrdinalIgnoreCase))
        {
            score += 5;
        }

        if (queryTokens.Count > 0)
        {
            score += Tokenize(line).Count(queryTokens.Contains) * 4;
        }

        return score;
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
            .Where(cell => !string.IsNullOrWhiteSpace(cell))
            .ToList();
    }

    private static int ScorePath(string path, ISet<string> queryTokens)
    {
        if (queryTokens.Count == 0)
        {
            return 0;
        }

        var fileTokens = Tokenize(Path.GetFileNameWithoutExtension(path)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var score = fileTokens.Count(queryTokens.Contains) * 3;

        try
        {
            var preview = File.ReadAllText(path);
            score += Tokenize(preview.Length > 3000 ? preview[..3000] : preview).Count(queryTokens.Contains);
        }
        catch
        {
            return score;
        }

        return score;
    }

    private static IEnumerable<string> Tokenize(string value)
    {
        return Regex
            .Matches(value.ToLowerInvariant(), @"[a-z0-9]{3,}")
            .Select(match => match.Value);
    }

    private static void AppendSection(StringBuilder builder, string title, IEnumerable<string> values)
    {
        builder.AppendLine($"## {title}");
        var valueList = values.Where(value => !string.IsNullOrWhiteSpace(value)).Take(30).ToList();

        if (valueList.Count == 0)
        {
            builder.AppendLine("- None found in allowed context.");
        }
        else
        {
            foreach (var value in valueList)
            {
                builder.AppendLine($"- {value}");
            }
        }

        builder.AppendLine();
    }

    private static void AppendSourceBlock(StringBuilder builder, string path, string text)
    {
        builder.AppendLine($"--- Source: {Path.GetFileName(path)} ---");
        builder.AppendLine(text);
        builder.AppendLine();
    }

    private static void AddIfExists(
        ICollection<string> includedFiles,
        ICollection<VaultSourceSummary> sources,
        string path,
        string sourceType)
    {
        if (File.Exists(path))
        {
            includedFiles.Add(path);
            sources.Add(BuildSource(path, sourceType, ReadFrontmatter(File.ReadAllText(path))));
        }
    }

    private static string StripMarkdown(string value)
    {
        return Regex.Replace(value, @"[*_`>#]", string.Empty).Trim();
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength
            ? value
            : value[..maxLength] + "\n[Context truncated]";
    }
}
