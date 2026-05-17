using VitaMR.Models;
using System.IO;

namespace VitaMR.Services;

public sealed class VaultIngestService : IVaultIngestService
{
    private readonly IEntityScaffoldingService _entityScaffoldingService;

    public VaultIngestService(string vaultRoot, IEntityScaffoldingService entityScaffoldingService)
    {
        VaultRoot = vaultRoot;
        _entityScaffoldingService = entityScaffoldingService;
    }

    public string VaultRoot { get; set; }

    public IReadOnlyList<IngestResult> IngestRawFiles(ChartContext chartContext, IEnumerable<string> sourcePaths)
    {
        var scaffoldCreatedPaths = _entityScaffoldingService.EnsureScaffold(VaultRoot, chartContext);
        var rawFolder = Path.Combine(VaultRoot, chartContext.ChartFolderName, "raw");
        Directory.CreateDirectory(rawFolder);

        var results = new List<IngestResult>();

        foreach (var sourcePath in sourcePaths)
        {
            if (!File.Exists(sourcePath))
            {
                continue;
            }

            var ingestedAt = DateTime.Now;
            var sourceFileName = Path.GetFileName(sourcePath);
            var safeFileName = BuildSafeFileName(sourceFileName);
            var datedFileName = $"{ingestedAt:yyyy-MM-dd}_{safeFileName}";
            var targetPath = GetNonConflictingPath(rawFolder, datedFileName);

            File.Copy(sourcePath, targetPath, overwrite: false);

            results.Add(new IngestResult(
                sourcePath,
                targetPath,
                Path.GetFileName(targetPath),
                ingestedAt,
                scaffoldCreatedPaths));
        }

        return results;
    }

    public IngestResult IngestRawText(ChartContext chartContext, string sourceText, string displayName)
    {
        var scaffoldCreatedPaths = _entityScaffoldingService.EnsureScaffold(VaultRoot, chartContext);
        var rawFolder = Path.Combine(VaultRoot, chartContext.ChartFolderName, "raw");
        Directory.CreateDirectory(rawFolder);

        var ingestedAt = DateTime.Now;
        var safeFileName = BuildSafeFileName(displayName);

        if (!Path.HasExtension(safeFileName))
        {
            safeFileName = $"{safeFileName}.md";
        }

        var datedFileName = $"{ingestedAt:yyyy-MM-dd_HHmmss}_{safeFileName}";
        var targetPath = GetNonConflictingPath(rawFolder, datedFileName);

        File.WriteAllText(targetPath, sourceText);

        return new IngestResult(
            "omnibox://typed-note",
            targetPath,
            Path.GetFileName(targetPath),
            ingestedAt,
            scaffoldCreatedPaths);
    }

    private static string BuildSafeFileName(string fileName)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var safeCharacters = fileName.Select(character =>
            invalidCharacters.Contains(character) ? '_' : character);

        var safeFileName = new string(safeCharacters.ToArray()).Trim();
        return string.IsNullOrWhiteSpace(safeFileName)
            ? "uploaded_file"
            : safeFileName;
    }

    private static string GetNonConflictingPath(string folder, string fileName)
    {
        var targetPath = Path.Combine(folder, fileName);

        if (!File.Exists(targetPath))
        {
            return targetPath;
        }

        var name = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);

        for (var index = 2; ; index++)
        {
            var candidate = Path.Combine(folder, $"{name}_{index}{extension}");

            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }
    }
}
