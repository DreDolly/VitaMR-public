using VitaMR.Models;

namespace VitaMR.Services;

public interface IHealthGoalEngineService
{
    HealthGoalResult CaptureIfGoalMention(string vaultRoot, ChartContext chartContext, string userText, string sessionId);
}
