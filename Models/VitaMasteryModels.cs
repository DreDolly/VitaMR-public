namespace VitaMR.Models;

public enum VitaMasteryStage
{
    Foundation,
    PreventiveMap,
    DeepSignal
}

public enum VitaMasteryQuestStatus
{
    Missing,
    InProgress,
    NeedsReview,
    Complete,
    Declined,
    NotApplicable,
    Deferred
}

public sealed class VitaMasteryQuest
{
    public string QuestId { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public VitaMasteryStage Stage { get; init; } = VitaMasteryStage.Foundation;

    public string PopulationRule { get; init; } = "all";

    public int? AgeMin { get; init; }

    public int? AgeMax { get; init; }

    public string SexOrAnatomyRule { get; init; } = "any";

    public string RiskRule { get; init; } = string.Empty;

    public int Points { get; init; } = 1;

    public string EvidenceHint { get; init; } = string.Empty;

    public string SourceName { get; init; } = string.Empty;

    public string SourceUrl { get; init; } = string.Empty;

    public DateOnly SourceCheckedDate { get; init; } = new(2026, 5, 12);

    public string UserMotivationText { get; init; } = string.Empty;
}

public sealed class VitaMasteryQuestResult
{
    public VitaMasteryQuest Quest { get; init; } = new();

    public VitaMasteryQuestStatus Status { get; init; } = VitaMasteryQuestStatus.Missing;

    public string EvidenceSummary { get; init; } = string.Empty;

    public bool AwardsPoints => Status == VitaMasteryQuestStatus.Complete;

    public int EarnedPoints => AwardsPoints ? Quest.Points : 0;
}

public sealed class VitaMasteryResult
{
    public string ChartId { get; init; } = string.Empty;

    public string PatientDisplayName { get; init; } = string.Empty;

    public int ApplicableQuestCount { get; init; }

    public int CompletedQuestCount { get; init; }

    public int EarnedPoints { get; init; }

    public int PossiblePoints { get; init; }

    public int PercentComplete { get; init; }

    public VitaMasteryStage CurrentStage { get; init; } = VitaMasteryStage.Foundation;

    public IReadOnlyList<VitaMasteryQuestResult> QuestResults { get; init; } = [];

    public IReadOnlyList<VitaMasteryQuestResult> ActiveMicroQuests { get; init; } = [];

    public IReadOnlyList<string> Messages { get; init; } = [];

    public string SummaryLine =>
        PossiblePoints <= 0
            ? "Vita Mastery: not started"
            : $"Vita Mastery: {PercentComplete}% | {FormatStage(CurrentStage)}";

    public static string FormatStage(VitaMasteryStage stage)
    {
        return stage switch
        {
            VitaMasteryStage.Foundation => "Basic",
            VitaMasteryStage.PreventiveMap => "Master",
            VitaMasteryStage.DeepSignal => "Legendary",
            _ => "Basic"
        };
    }
}
