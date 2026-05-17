namespace VitaMR.Services;

public interface IPromptCacheService
{
    string GetPrompt(string fileName, string fallback);

    void PreloadPrompts(IEnumerable<string> fileNames);
}
