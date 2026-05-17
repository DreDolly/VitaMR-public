namespace VitaMR.Models;

public sealed class VaultContextPacket
{
    public string ChartId { get; set; } = string.Empty;

    public string PatientDisplayName { get; set; } = string.Empty;

    public string Status { get; set; } = "VAULT_CONTEXT_NOT_LOADED";

    public IReadOnlyList<string> KnownPatients { get; set; } = [];

    public IReadOnlyList<string> IncludedFiles { get; set; } = [];

    public IReadOnlyList<VaultSourceSummary> Sources { get; set; } = [];

    public IReadOnlyList<string> KnownFacts { get; set; } = [];

    public IReadOnlyList<string> AvailableSources { get; set; } = [];

    public IReadOnlyList<string> PendingItems { get; set; } = [];

    public IReadOnlyList<string> MissingOrUnavailableData { get; set; } = [];

    public IReadOnlyList<string> RetrievalWarnings { get; set; } = [];

    public string ContextText { get; set; } = string.Empty;
}
