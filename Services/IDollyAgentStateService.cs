using VitaMR.Models;

namespace VitaMR.Services;

public interface IDollyAgentStateService
{
    void EnsureAgentScaffold(string vaultRoot);

    string BuildAgentContext(string vaultRoot);

    void StartTurn(
        string vaultRoot,
        ChartContext chartContext,
        string userText,
        GemmaActionPacket packet);

    void CompleteTurn(
        string vaultRoot,
        ChartContext chartContext,
        string status,
        string resultSummary);

    void RecordTaskStarted(
        string vaultRoot,
        ChartContext chartContext,
        string taskId,
        string taskName,
        string currentStep);

    void RecordTaskUpdated(
        string vaultRoot,
        ChartContext chartContext,
        string taskId,
        string status,
        string currentStep,
        string resultSummary);

    void RecordHeartbeat(
        string vaultRoot,
        ChartContext chartContext,
        DollyTaskBoardResult taskBoardResult);

    void AppendPacketAudit(
        string vaultRoot,
        ChartContext chartContext,
        string routeUsed,
        string modelName,
        string scrubbedUserPreview,
        GemmaActionPacketResult result);
}
