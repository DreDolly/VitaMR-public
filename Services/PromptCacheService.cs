using System.Collections.Concurrent;
using System.IO;

namespace VitaMR.Services;

public sealed class PromptCacheService : IPromptCacheService
{
    private readonly ConcurrentDictionary<string, string> _prompts = new(StringComparer.OrdinalIgnoreCase);

    public string GetPrompt(string fileName, string fallback)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return fallback;
        }

        var key = fileName.Trim();

        if (_prompts.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var loaded = LoadPrompt(key);

        if (string.IsNullOrWhiteSpace(loaded))
        {
            return fallback;
        }

        _prompts[key] = loaded;
        return loaded;
    }

    public void PreloadPrompts(IEnumerable<string> fileNames)
    {
        foreach (var fileName in fileNames)
        {
            GetPrompt(fileName, string.Empty);
        }
    }

    private static string LoadPrompt(string fileName)
    {
        var promptPath = Path.Combine(AppContext.BaseDirectory, "Prompts", fileName);

        return File.Exists(promptPath)
            ? File.ReadAllText(promptPath)
            : string.Empty;
    }
}
