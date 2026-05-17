using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using VitaMR.Models;

namespace VitaMR.Services;

public sealed class GeminiBackendPipelineService : IGeminiBackendPipelineService
{
    private readonly IGeminiApiKeyStore _apiKeyStore;
    private readonly IGeminiDocumentClassifierService _classifierService;
    private readonly IGeminiEncounterExtractionService _extractionService;
    private readonly IEncounterNodeWriterService _encounterNodeWriterService;
    private readonly IGeminiEncounterWikiRewriteService _encounterWikiRewriteService;

    public GeminiBackendPipelineService(
        IGeminiApiKeyStore apiKeyStore,
        IGeminiDocumentClassifierService classifierService,
        IGeminiEncounterExtractionService extractionService,
        IEncounterNodeWriterService encounterNodeWriterService,
        IGeminiEncounterWikiRewriteService encounterWikiRewriteService)
    {
        _apiKeyStore = apiKeyStore;
        _classifierService = classifierService;
        _extractionService = extractionService;
        _encounterNodeWriterService = encounterNodeWriterService;
        _encounterWikiRewriteService = encounterWikiRewriteService;
    }

    public async Task<GeminiBackendPipelineResult> ProcessScrubbedPayloadsAsync(
        bool isEnabled,
        string fastModelName,
        string thinkingModelName,
        string vaultRoot,
        ChartContext chartContext,
        IReadOnlyList<SecondPassScrubResult> scrubbedPayloads,
        string sourceSystem,
        string sourceFacility,
        string sourceType,
        Func<string, string, string, Task>? progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        var result = new GeminiBackendPipelineResult
        {
            WasAttempted = true,
            WasEnabled = isEnabled
        };

        if (!isEnabled)
        {
            AddStep(result, "API gate", "skipped", "Gemini backend writer is disabled.");
            result.Messages.Add("Final scrubbed payloads are saved, but API wiki writing is disabled.");
            return result;
        }

        if (!_apiKeyStore.HasConfiguredKeys())
        {
            AddStep(result, "API keys", "blocked", "Gemini API keys are not configured.");
            result.Messages.Add("Gemini backend writer could not run because keys are missing.");
            return result;
        }

        if (scrubbedPayloads.Count == 0)
        {
            AddStep(result, "Scrubbed payloads", "skipped", "No final scrubbed payloads were available.");
            result.Messages.Add("No scrubbed payload was available for API extraction.");
            return result;
        }

        AddStep(result, "API gate", "ready", "Gemini backend writer enabled.");
        await ReportProgressAsync(progressCallback, "running", "API gate ready", "Gemini backend writer enabled.");

        foreach (var payload in scrubbedPayloads)
        {
            result.ApiSubmittedSourcePaths.Add(payload.SourcePath);
            var rawHash = ComputeFileHash(payload.SourcePath);
            var exactDuplicate = FindDuplicateFingerprint(vaultRoot, chartContext, "raw_sha256", rawHash);

            if (exactDuplicate is not null)
            {
                result.DuplicateSkippedSourcePaths.Add(payload.SourcePath);
                AddStep(result, "Duplicate gate", "skipped", $"{payload.DisplayName}: exact duplicate of {exactDuplicate.EncounterNode}.");
                result.Messages.Add($"{payload.DisplayName}: exact duplicate skipped. First seen {exactDuplicate.FirstSeen}; encounter {exactDuplicate.EncounterNode}.");
                AppendDuplicateSeen(vaultRoot, chartContext, exactDuplicate, payload.DisplayName);
                continue;
            }

            ScrubbedDocumentClassificationResult? classification;

            try
            {
                await ReportProgressAsync(progressCallback, "running", "Gemini fast classifier", payload.DisplayName);
                classification = await _classifierService.ClassifyAsync(
                    fastModelName,
                    chartContext,
                    payload,
                    cancellationToken);
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or InvalidOperationException)
            {
                classification = null;
                AddStep(result, "Fast classifier", "failed", $"{payload.DisplayName}: {exception.GetType().Name}.");
            }

            if (classification is null)
            {
                AddStep(result, "Fast classifier", "failed", $"{payload.DisplayName}: no classification returned.");
            }
            else
            {
                AddStep(
                    result,
                    "Fast classifier",
                    "passed",
                    $"{payload.DisplayName}: {classification.DocumentType}, risk {classification.RiskLevel}, deep extraction {classification.NeedsDeepExtraction}.");
                await ReportProgressAsync(progressCallback, "running", "Gemini classification complete", $"{classification.DocumentType}; extraction {classification.NeedsDeepExtraction}.");
            }

            if (classification is { NeedsDeepExtraction: false })
            {
                result.Messages.Add($"{payload.DisplayName}: classifier said deep extraction was not needed.");
                continue;
            }

            EncounterExtractionResult? extraction;

            try
            {
                await ReportProgressAsync(progressCallback, "running", "Gemini encounter extraction", payload.DisplayName);
                extraction = await _extractionService.ExtractEncounterAsync(
                    "thinking",
                    thinkingModelName,
                    chartContext,
                    payload,
                    sourceSystem,
                    sourceFacility,
                    sourceType,
                    cancellationToken);
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or InvalidOperationException)
            {
                extraction = null;
                AddStep(result, "Deep extraction", "failed", $"{payload.DisplayName}: {exception.GetType().Name}.");
            }

            if (extraction is null)
            {
                AddStep(result, "Deep extraction", "failed", $"{payload.DisplayName}: no encounter JSON returned.");
                result.Messages.Add($"{payload.DisplayName}: Gemini did not return a usable extraction result.");
                continue;
            }

            AddStep(result, "Deep extraction", "passed", $"{payload.DisplayName}: encounter JSON returned.");
            await ReportProgressAsync(progressCallback, "running", "Gemini extraction complete", "Encounter JSON returned.");

            var clinicalHash = ComputeClinicalFingerprint(extraction);
            var clinicalDuplicate = FindDuplicateFingerprint(vaultRoot, chartContext, "clinical_sha256", clinicalHash);

            if (clinicalDuplicate is not null)
            {
                result.DuplicateSkippedSourcePaths.Add(payload.SourcePath);
                AddStep(result, "Duplicate gate", "skipped", $"{payload.DisplayName}: clinical duplicate of {clinicalDuplicate.EncounterNode}.");
                result.Messages.Add($"{payload.DisplayName}: likely duplicate skipped by clinical fingerprint. First seen {clinicalDuplicate.FirstSeen}; encounter {clinicalDuplicate.EncounterNode}.");
                AppendDuplicateSeen(vaultRoot, chartContext, clinicalDuplicate, payload.DisplayName);
                continue;
            }

            AddStep(
                result,
                "LLM wiki writer",
                "skipped",
                "Longitudinal wiki files are C#-owned by default; Gemini may extract facts but does not rewrite Index/Timeline/Care Gaps.");
            result.Messages.Add("LLM encounter authoring skipped by policy: C# owns trusted longitudinal wiki writes.");

            await ReportProgressAsync(progressCallback, "running", "C# trusted wiki writer", "Writing encounter, Index, Timeline, Care Gaps, Conflicts, and topic pages.");
            var writeResult = _encounterNodeWriterService.WriteEncounterNode(
                vaultRoot,
                chartContext,
                extraction);
            var validationMessages = ValidateEncounterWriteResult(vaultRoot, chartContext, writeResult);

            if (validationMessages.Count == 0)
            {
                result.WriteResults.Add(writeResult);
                result.WikiWrittenSourcePaths.Add(payload.SourcePath);
                result.SummaryItems.Add(BuildIngestSummaryItem(payload.DisplayName, extraction, writeResult));
                AppendFingerprint(
                    vaultRoot,
                    chartContext,
                    payload,
                    writeResult,
                    rawHash,
                    clinicalHash);
            }
            else
            {
                result.Messages.AddRange(validationMessages.Take(3));
            }

            AddStep(
                result,
                "C# trusted wiki writer",
                writeResult.WasWritten && validationMessages.Count == 0 ? "passed" : "failed",
                writeResult.WasWritten && validationMessages.Count == 0
                    ? $"{Path.GetFileName(writeResult.EncounterNodePath)} written; {writeResult.TopicPagePaths.Count} topic page(s)."
                    : $"{payload.DisplayName}: C# writer did not create a validated encounter node.");
        }

        result.WroteAnyEncounterNodes = result.WriteResults.Any(write => write.WasWritten);

        if (result.WroteAnyEncounterNodes)
        {
            result.Messages.Add($"Backend pipeline wrote {result.WriteResults.Count(write => write.WasWritten)} encounter node(s) through the C# trusted writer.");
        }
        else
        {
            result.Messages.Add(result.DuplicateSkippedSourcePaths.Count > 0
                ? "Backend pipeline skipped duplicate source(s); no new encounter node was needed."
                : "Gemini backend did not write any encounter nodes.");
        }

        return result;
    }

    private static Task ReportProgressAsync(
        Func<string, string, string, Task>? progressCallback,
        string status,
        string currentStep,
        string resultSummary)
    {
        return progressCallback is null
            ? Task.CompletedTask
            : progressCallback(status, currentStep, resultSummary);
    }

    private static string ComputeFileHash(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return string.Empty;
        }

        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static string ComputeClinicalFingerprint(EncounterExtractionResult extraction)
    {
        var parts = new[]
        {
            extraction.DateOfService,
            extraction.DocumentType,
            extraction.ClinicalGestalt,
            string.Join(" ", extraction.Diagnoses.Take(8)),
            string.Join(" ", extraction.ActiveMedications.Take(8)),
            string.Join(" ", extraction.PendingItems.Take(8)),
            string.Join(" ", extraction.Imaging.Take(8)),
            string.Join(" ", extraction.LabsResults.Take(8))
        };
        var normalized = NormalizeFingerprintText(string.Join("|", parts));

        return string.IsNullOrWhiteSpace(normalized)
            ? string.Empty
            : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized))).ToLowerInvariant();
    }

    private static string NormalizeFingerprintText(string value)
    {
        var normalized = Regex.Replace(value.ToLowerInvariant(), @"\[\[[^\]]+\]\]", " ");
        normalized = Regex.Replace(normalized, @"[^a-z0-9.]+", " ");
        normalized = Regex.Replace(normalized, @"\b(unknown|none|not|documented|provided|sterile|payload|the|and|or|with|for|from|patient)\b", " ");
        return Regex.Replace(normalized, @"\s+", " ").Trim();
    }

    private static IngestSummaryItem BuildIngestSummaryItem(
        string displayName,
        EncounterExtractionResult extraction,
        EncounterNodeWriteResult writeResult)
    {
        var facts = new List<string>();
        AddRange(facts, extraction.Diagnoses, 2);
        AddRange(facts, extraction.Imaging, 2);
        AddRange(facts, extraction.LabsResults, 2);
        AddRange(facts, extraction.AssessmentPlan, 3);
        AddRange(facts, extraction.SafetyFlags, 2);

        return new IngestSummaryItem
        {
            DisplayName = displayName,
            DocumentType = extraction.DocumentType,
            DateOfService = extraction.DateOfService,
            ClinicalGestalt = extraction.ClinicalGestalt,
            MainClinicalFacts = facts
                .Where(fact => !string.IsNullOrWhiteSpace(fact))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(5)
                .ToList(),
            PendingItems = extraction.PendingItems
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(5)
                .ToList(),
            TopicPages = extraction.ProposedTopicPages
                .Where(page => !string.IsNullOrWhiteSpace(page))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(5)
                .ToList(),
            EncounterNodePath = writeResult.EncounterNodePath
        };
    }

    private static void AddRange(ICollection<string> target, IEnumerable<string> source, int take)
    {
        foreach (var value in source.Where(item => !string.IsNullOrWhiteSpace(item)).Take(take))
        {
            target.Add(value);
        }
    }

    private static FingerprintRow? FindDuplicateFingerprint(
        string vaultRoot,
        ChartContext chartContext,
        string hashKind,
        string hash)
    {
        if (string.IsNullOrWhiteSpace(hash))
        {
            return null;
        }

        var path = GetFingerprintPath(vaultRoot, chartContext);

        if (!File.Exists(path))
        {
            return null;
        }

        return File.ReadLines(path)
            .Select(ParseFingerprintRow)
            .Where(row => row is not null)
            .Cast<FingerprintRow>()
            .FirstOrDefault(row =>
                row.HashKind.Equals(hashKind, StringComparison.OrdinalIgnoreCase) &&
                row.Hash.Equals(hash, StringComparison.OrdinalIgnoreCase));
    }

    private static void AppendFingerprint(
        string vaultRoot,
        ChartContext chartContext,
        SecondPassScrubResult payload,
        EncounterNodeWriteResult writeResult,
        string rawHash,
        string clinicalHash)
    {
        var encounterName = string.IsNullOrWhiteSpace(writeResult.EncounterNodePath)
            ? "none"
            : Path.GetFileName(writeResult.EncounterNodePath);

        AppendFingerprintRow(vaultRoot, chartContext, new FingerprintRow(
            "raw_sha256",
            rawHash,
            payload.DisplayName,
            payload.SourcePath,
            payload.FinalScrubbedPath,
            encounterName,
            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            1));

        AppendFingerprintRow(vaultRoot, chartContext, new FingerprintRow(
            "clinical_sha256",
            clinicalHash,
            payload.DisplayName,
            payload.SourcePath,
            payload.FinalScrubbedPath,
            encounterName,
            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            1));
    }

    private static void AppendDuplicateSeen(
        string vaultRoot,
        ChartContext chartContext,
        FingerprintRow duplicate,
        string displayName)
    {
        var rows = ReadFingerprintRows(vaultRoot, chartContext).ToList();
        var index = rows.FindIndex(row =>
            row.HashKind.Equals(duplicate.HashKind, StringComparison.OrdinalIgnoreCase) &&
            row.Hash.Equals(duplicate.Hash, StringComparison.OrdinalIgnoreCase));

        if (index < 0)
        {
            return;
        }

        rows[index] = duplicate with
        {
            SourceName = string.IsNullOrWhiteSpace(displayName) ? duplicate.SourceName : displayName,
            LastSeen = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            Count = duplicate.Count + 1
        };

        WriteFingerprintRows(vaultRoot, chartContext, rows);
    }

    private static void AppendFingerprintRow(string vaultRoot, ChartContext chartContext, FingerprintRow row)
    {
        var rows = ReadFingerprintRows(vaultRoot, chartContext).ToList();

        if (string.IsNullOrWhiteSpace(row.Hash) ||
            rows.Any(existing =>
                existing.HashKind.Equals(row.HashKind, StringComparison.OrdinalIgnoreCase) &&
                existing.Hash.Equals(row.Hash, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        rows.Add(row);
        WriteFingerprintRows(vaultRoot, chartContext, rows);
    }

    private static IReadOnlyList<FingerprintRow> ReadFingerprintRows(string vaultRoot, ChartContext chartContext)
    {
        var path = GetFingerprintPath(vaultRoot, chartContext);

        if (!File.Exists(path))
        {
            return [];
        }

        return File.ReadLines(path)
            .Select(ParseFingerprintRow)
            .Where(row => row is not null)
            .Cast<FingerprintRow>()
            .ToList();
    }

    private static void WriteFingerprintRows(string vaultRoot, ChartContext chartContext, IEnumerable<FingerprintRow> rows)
    {
        var path = GetFingerprintPath(vaultRoot, chartContext);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var builder = new StringBuilder();

        builder.AppendLine("# Ingest Fingerprints");
        builder.AppendLine();
        builder.AppendLine("| Hash Kind | Hash | Source Name | Raw Vault Path | Final Scrubbed Path | Encounter Node | First Seen | Last Seen | Count |");
        builder.AppendLine("|---|---|---|---|---|---|---|---|---|");

        foreach (var row in rows)
        {
            builder.AppendLine($"| {EscapeCell(row.HashKind)} | {EscapeCell(row.Hash)} | {EscapeCell(row.SourceName)} | {EscapeCell(row.RawVaultPath)} | {EscapeCell(row.FinalScrubbedPath)} | {EscapeCell(row.EncounterNode)} | {EscapeCell(row.FirstSeen)} | {EscapeCell(row.LastSeen)} | {row.Count} |");
        }

        File.WriteAllText(path, builder.ToString());
    }

    private static FingerprintRow? ParseFingerprintRow(string line)
    {
        var trimmed = line.Trim();

        if (!trimmed.StartsWith('|') ||
            trimmed.Contains("---", StringComparison.Ordinal) ||
            trimmed.Contains("Hash Kind", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var cells = trimmed.Trim('|').Split('|').Select(cell => cell.Trim()).ToArray();

        if (cells.Length < 9 || !int.TryParse(cells[8], out var count))
        {
            return null;
        }

        return new FingerprintRow(
            cells[0],
            cells[1],
            cells[2],
            cells[3],
            cells[4],
            cells[5],
            cells[6],
            cells[7],
            count);
    }

    private static string GetFingerprintPath(string vaultRoot, ChartContext chartContext)
    {
        return Path.Combine(vaultRoot, chartContext.ChartFolderName, "wiki", "_Ingest_Fingerprints.md");
    }

    private static string EscapeCell(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "none"
            : value.Replace("|", "/", StringComparison.Ordinal)
                .Replace("\r", " ", StringComparison.Ordinal)
                .Replace("\n", " ", StringComparison.Ordinal)
                .Trim();
    }

    private static IReadOnlyList<string> ValidateEncounterWriteResult(
        string vaultRoot,
        ChartContext chartContext,
        EncounterNodeWriteResult writeResult)
    {
        var messages = new List<string>();

        if (!writeResult.WasWritten)
        {
            messages.Add("Encounter writer did not report a successful write.");
            return messages;
        }

        foreach (var path in EnumerateEncounterWikiPaths(writeResult))
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            if (!IsSafeChartWikiPath(vaultRoot, chartContext, path))
            {
                messages.Add($"Rejected unsafe wiki write path: {path}");
                continue;
            }

            if (!File.Exists(path))
            {
                messages.Add($"Expected wiki output was missing: {path}");
                continue;
            }

            var text = File.ReadAllText(path);
            var trimmed = text.TrimStart();

            if (trimmed.StartsWith("{", StringComparison.Ordinal) ||
                trimmed.StartsWith("[", StringComparison.Ordinal))
            {
                messages.Add($"Rejected JSON-shaped markdown output: {path}");
            }
        }

        return messages;
    }

    private static IEnumerable<string> EnumerateEncounterWikiPaths(EncounterNodeWriteResult writeResult)
    {
        yield return writeResult.EncounterNodePath;
        yield return writeResult.IndexPath;
        yield return writeResult.TimelinePath;
        yield return writeResult.CareGapsPath;
        yield return writeResult.ConflictsPath;
        yield return writeResult.EmergencyCardPath;

        foreach (var path in writeResult.TopicPagePaths)
        {
            yield return path;
        }
    }

    private static bool IsSafeChartWikiPath(string vaultRoot, ChartContext chartContext, string path)
    {
        var chartRoot = Path.GetFullPath(Path.Combine(vaultRoot, chartContext.ChartFolderName));
        var wikiRoot = Path.GetFullPath(Path.Combine(chartRoot, "wiki"));
        var safeWikiRoot = wikiRoot.EndsWith(Path.DirectorySeparatorChar)
            ? wikiRoot
            : wikiRoot + Path.DirectorySeparatorChar;
        var safePath = Path.GetFullPath(path);

        return safePath.StartsWith(safeWikiRoot, StringComparison.OrdinalIgnoreCase) &&
               safePath.EndsWith(".md", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<(EncounterNodeWriteResult? WriteResult, WikiRewriteApplyResult RewriteResult)> TryWriteEncounterViaLlmAsync(
        string modelName,
        string vaultRoot,
        ChartContext chartContext,
        EncounterExtractionResult extraction,
        CancellationToken cancellationToken)
    {
        var rewriteResult = new WikiRewriteApplyResult
        {
            WasAttempted = true,
            Status = "ENCOUNTER_WIKI_REWRITE_NOT_ATTEMPTED"
        };

        var stagingRoot = Path.Combine(
            Path.GetTempPath(),
            "VitaMR",
            "EncounterRewrite",
            Guid.NewGuid().ToString("N"));

        try
        {
            SeedStagingVault(vaultRoot, stagingRoot, chartContext);

            var stagedWriteResult = _encounterNodeWriterService.WriteEncounterNode(
                stagingRoot,
                chartContext,
                extraction);

            if (!stagedWriteResult.WasWritten)
            {
                rewriteResult.Status = "ENCOUNTER_WIKI_STAGE_WRITE_FAILED";
                return (null, rewriteResult);
            }

            rewriteResult = await _encounterWikiRewriteService.RewriteEncounterFilesAsync(
                true,
                modelName,
                stagingRoot,
                chartContext,
                extraction,
                stagedWriteResult,
                cancellationToken);

            if (!rewriteResult.WasApplied)
            {
                return (null, rewriteResult);
            }

            CopyApprovedFilesToVault(stagingRoot, vaultRoot, rewriteResult.UpdatedFiles);
            MergeLinkRegistry(stagingRoot, vaultRoot);

            return (RemapWriteResultToVault(stagedWriteResult, stagingRoot, vaultRoot), rewriteResult);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            rewriteResult.Status = "ENCOUNTER_WIKI_REWRITE_STAGING_FAILED";
            rewriteResult.Messages.Add(exception.Message);
            return (null, rewriteResult);
        }
        finally
        {
            TryDeleteDirectory(stagingRoot);
        }
    }

    private static void SeedStagingVault(string vaultRoot, string stagingRoot, ChartContext chartContext)
    {
        Directory.CreateDirectory(stagingRoot);

        var sourceChartRoot = Path.Combine(vaultRoot, chartContext.ChartFolderName);
        var stagingChartRoot = Path.Combine(stagingRoot, chartContext.ChartFolderName);

        if (Directory.Exists(sourceChartRoot))
        {
            CopyDirectory(sourceChartRoot, stagingChartRoot);
        }
        else
        {
            Directory.CreateDirectory(stagingChartRoot);
        }

        var sourceLinkRegistry = Path.Combine(vaultRoot, "_Link_Registry.md");
        var stagingLinkRegistry = Path.Combine(stagingRoot, "_Link_Registry.md");

        if (File.Exists(sourceLinkRegistry))
        {
            File.Copy(sourceLinkRegistry, stagingLinkRegistry, true);
        }
    }

    private static void CopyApprovedFilesToVault(string stagingRoot, string vaultRoot, IEnumerable<string> stagedFiles)
    {
        foreach (var stagedPath in stagedFiles
                     .Where(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var relativePath = Path.GetRelativePath(stagingRoot, stagedPath);
            var targetPath = Path.Combine(vaultRoot, relativePath);
            var targetDirectory = Path.GetDirectoryName(targetPath);

            if (!string.IsNullOrWhiteSpace(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
            }

            File.Copy(stagedPath, targetPath, true);
        }
    }

    private static void MergeLinkRegistry(string stagingRoot, string vaultRoot)
    {
        var stagedPath = Path.Combine(stagingRoot, "_Link_Registry.md");

        if (!File.Exists(stagedPath))
        {
            return;
        }

        var targetPath = Path.Combine(vaultRoot, "_Link_Registry.md");

        if (!File.Exists(targetPath))
        {
            File.Copy(stagedPath, targetPath, true);
            return;
        }

        var targetContent = File.ReadAllText(targetPath);
        var rowsToAppend = File.ReadLines(stagedPath)
            .Where(line => line.StartsWith("| wiki/", StringComparison.OrdinalIgnoreCase))
            .Where(line => !targetContent.Contains(line, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (rowsToAppend.Count > 0)
        {
            File.AppendAllLines(targetPath, rowsToAppend);
        }
    }

    private static EncounterNodeWriteResult RemapWriteResultToVault(
        EncounterNodeWriteResult stagedWriteResult,
        string stagingRoot,
        string vaultRoot)
    {
        return new EncounterNodeWriteResult
        {
            WasWritten = stagedWriteResult.WasWritten,
            EncounterNodePath = MapPath(stagedWriteResult.EncounterNodePath, stagingRoot, vaultRoot),
            IndexPath = MapPath(stagedWriteResult.IndexPath, stagingRoot, vaultRoot),
            TimelinePath = MapPath(stagedWriteResult.TimelinePath, stagingRoot, vaultRoot),
            CareGapsPath = MapPath(stagedWriteResult.CareGapsPath, stagingRoot, vaultRoot),
            ConflictsPath = MapPath(stagedWriteResult.ConflictsPath, stagingRoot, vaultRoot),
            EmergencyCardPath = MapPath(stagedWriteResult.EmergencyCardPath, stagingRoot, vaultRoot),
            TopicPagePaths = stagedWriteResult.TopicPagePaths
                .Select(path => MapPath(path, stagingRoot, vaultRoot))
                .ToList(),
            LinkRegistryPath = Path.Combine(vaultRoot, "_Link_Registry.md"),
            Messages = [.. stagedWriteResult.Messages]
        };
    }

    private static string MapPath(string path, string stagingRoot, string vaultRoot)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var relativePath = Path.GetRelativePath(stagingRoot, path);
        return Path.Combine(vaultRoot, relativePath);
    }

    private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);

        foreach (var file in Directory.GetFiles(sourceDirectory))
        {
            var targetPath = Path.Combine(destinationDirectory, Path.GetFileName(file));
            File.Copy(file, targetPath, true);
        }

        foreach (var directory in Directory.GetDirectories(sourceDirectory))
        {
            var targetPath = Path.Combine(destinationDirectory, Path.GetFileName(directory));
            CopyDirectory(directory, targetPath);
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        try
        {
            Directory.Delete(path, true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void AddStep(
        GeminiBackendPipelineResult result,
        string name,
        string status,
        string detail)
    {
        result.Steps.Add(new GeminiBackendPipelineStep
        {
            Name = name,
            Status = status,
            Detail = detail
        });
    }

    private sealed record FingerprintRow(
        string HashKind,
        string Hash,
        string SourceName,
        string RawVaultPath,
        string FinalScrubbedPath,
        string EncounterNode,
        string FirstSeen,
        string LastSeen,
        int Count);
}
