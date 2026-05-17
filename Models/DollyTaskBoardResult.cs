namespace VitaMR.Models;

public sealed class DollyTaskBoardResult
{
    public bool WasWritten { get; set; }

    public string Status { get; set; } = "DOLLY_TASK_BOARD_NOT_RUN";

    public string BoardPath { get; set; } = string.Empty;

    public int ActiveTaskCount { get; set; }

    public int CompletedTaskCount { get; set; }

    public List<string> Lines { get; } = [];
}
