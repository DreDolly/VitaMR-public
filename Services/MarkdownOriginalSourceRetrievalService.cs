using System.IO;
using System.Text.RegularExpressions;
using VitaMR.Models;

namespace VitaMR.Services;

public sealed class MarkdownOriginalSourceRetrievalService : IOriginalSourceRetrievalService
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf",
        ".png",
        ".jpg",
        ".jpeg",
        ".bmp",
        ".gif",
        ".tif",
        ".tiff",
        ".heic",
        ".txt",
        ".md",
        ".doc",
        ".docx"
    };

    public OriginalSourceRetrievalResult FindOriginalSources(
        string vaultRoot,
        ChartContext chartContext,
        string userRequest,
        int maxResults = 3)
    {
        var result = new OriginalSourceRetrievalResult();
        var rawFolder = Path.Combine(vaultRoot, chartContext.ChartFolderName, "raw");

        if (!Directory.Exists(rawFolder))
        {
            result.Status = "ORIGINAL_SOURCE_RAW_FOLDER_MISSING";
            result.Messages.Add("I do not see a saved original-source folder for this chart yet.");
            return result;
        }

        var rawRoot = Path.GetFullPath(rawFolder);
        var tokens = Tokenize(userRequest).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var candidates = Directory
            .EnumerateFiles(rawRoot, "*", SearchOption.TopDirectoryOnly)
            .Where(path => AllowedExtensions.Contains(Path.GetExtension(path)))
            .Select(path => new
            {
                Path = Path.GetFullPath(path),
                Score = ScoreFile(path, tokens),
                LastWrite = File.GetLastWriteTime(path)
            })
            .Where(item => item.Path.StartsWith(rawRoot, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.Score)
            .ThenByDescending(item => item.LastWrite)
            .Take(Math.Max(1, maxResults))
            .ToList();

        if (candidates.Count == 0)
        {
            result.Status = "ORIGINAL_SOURCE_NOT_FOUND";
            result.Messages.Add("I could not find a saved original file for this chart.");
            return result;
        }

        result.WasFound = true;
        result.Status = "ORIGINAL_SOURCE_READY";
        result.SourcePaths.AddRange(candidates.Select(item => item.Path));
        result.Messages.Add(candidates.Count == 1
            ? "I found the saved original file and attached it here unchanged."
            : $"I found {candidates.Count} saved original files and attached them here unchanged.");
        result.Messages.Add("I did not summarize or rewrite these originals.");
        return result;
    }

    private static int ScoreFile(string path, ISet<string> tokens)
    {
        if (tokens.Count == 0)
        {
            return 0;
        }

        var fileName = Path.GetFileNameWithoutExtension(path);
        var fileTokens = Tokenize(fileName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var score = fileTokens.Count(tokens.Contains) * 5;

        foreach (var token in tokens)
        {
            if (fileName.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                score += 2;
            }
        }

        return score;
    }

    private static IEnumerable<string> Tokenize(string value)
    {
        return Regex
            .Matches(value.ToLowerInvariant(), @"[a-z0-9]{3,}")
            .Select(match => match.Value)
            .Where(token => token is not "original" and not "source" and not "file" and not "document" and not "show" and not "fetch" and not "open");
    }
}
