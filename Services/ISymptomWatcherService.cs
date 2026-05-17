using VitaMR.Models;

namespace VitaMR.Services;

public interface ISymptomWatcherService
{
    SymptomWatcherResult CaptureIfSymptomMention(string vaultRoot, ChartContext chartContext, string userText, string sessionId);
}
