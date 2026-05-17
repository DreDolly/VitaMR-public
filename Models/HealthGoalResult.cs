namespace VitaMR.Models;

public sealed class HealthGoalResult
{
    public bool WasRun { get; set; }

    public bool WasCaptured { get; set; }

    public string Status { get; set; } = "HEALTH_GOAL_NOT_RUN";

    public string RawEntryPath { get; set; } = string.Empty;

    public string GoalsPath { get; set; } = string.Empty;

    public string GoalTitle { get; set; } = string.Empty;
}
