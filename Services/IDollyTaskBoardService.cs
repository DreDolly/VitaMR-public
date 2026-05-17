using VitaMR.Models;

namespace VitaMR.Services;

public interface IDollyTaskBoardService
{
    DollyTaskBoardResult EnsureBoard(string vaultRoot, ChartContext chartContext);

    string StartTask(
        string vaultRoot,
        ChartContext chartContext,
        string taskName,
        string currentStep,
        string routeModel,
        string taskType = "user_task",
        int priority = 1);

    void UpdateTask(
        string vaultRoot,
        ChartContext chartContext,
        string taskId,
        string status,
        int attempts,
        string currentStep,
        string routeModel,
        string lastError,
        string nextRetry,
        string resultSummary,
        string taskType = "",
        int priority = 0);

    DollyTaskBoardResult BuildStatus(string vaultRoot, ChartContext chartContext);
}
