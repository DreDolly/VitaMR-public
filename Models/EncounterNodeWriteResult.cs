namespace VitaMR.Models;

public sealed class EncounterNodeWriteResult
{
    public bool WasWritten { get; set; }

    public string EncounterNodePath { get; set; } = string.Empty;

    public string IndexPath { get; set; } = string.Empty;

    public string TimelinePath { get; set; } = string.Empty;

    public string CareGapsPath { get; set; } = string.Empty;

    public string ConflictsPath { get; set; } = string.Empty;

    public string EmergencyCardPath { get; set; } = string.Empty;

    public List<string> TopicPagePaths { get; set; } = [];

    public string LinkRegistryPath { get; set; } = string.Empty;

    public List<string> Messages { get; set; } = [];
}
