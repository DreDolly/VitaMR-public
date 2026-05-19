namespace VitaMR.Models;

public sealed class AppSettings
{
    public string VaultRootPath { get; set; } = string.Empty;

    public string ActiveChartId { get; set; } = "VITA-0001";

    public string ActivePatientDisplayName { get; set; } = "Synthetic Test Patient";

    public string LastActiveChartId { get; set; } = string.Empty;

    public string MostRecentlyCreatedChartId { get; set; } = string.Empty;

    public bool IsInitialSetupComplete { get; set; }

    public string InitialSetupStep { get; set; } = "chart_manager_name";

    public string ActiveUserName { get; set; } = string.Empty;

    public string ActiveUserRole { get; set; } = "Chart Manager";

    public string AgentDisplayName { get; set; } = "Dolly";

    public string AgentTone { get; set; } = "Friendly";

    public string ChartManagerNickname { get; set; } = string.Empty;

    public string ChartManagerPhotoPath { get; set; } = string.Empty;

    public string ChartManagerPersonaStyle { get; set; } = string.Empty;

    public string SetupPatientNames { get; set; } = string.Empty;

    public string SetupAdditionalUsers { get; set; } = string.Empty;

    public string SetupPersonaPreference { get; set; } = string.Empty;

    public string SelectedModelRoute { get; set; } = "Dolly Main Agent";

    public string SelectedAiProvider { get; set; } = "Gemini";

    public bool EnableHealthspanMode { get; set; }

    public string HealthspanCoachingMode { get; set; } = "Record Assistant Mode";

    public string ActiveLifeMode { get; set; } = "Medical";

    public string SelectedOmniboxAction { get; set; } = "Ask / Update Existing Patient";

    public string VaultOwnerName { get; set; } = "Vault Manager";

    public string VaultOwnerRole { get; set; } = "Project Owner";

    public string VaultOwnerDeletePassword { get; set; } = "Vault Manager";

    public string ChartManagerName { get; set; } = "Vault Manager";

    public string ChartManagerPassword { get; set; } = "Vault Manager";

    public bool IsSyntheticTestMode { get; set; } = true;

    public string DefaultSourceSystem { get; set; } = "Unknown";

    public string DefaultSourceFacility { get; set; } = "Unknown";

    public string DefaultSourceType { get; set; } = "Unknown";

    public bool EnableLocalModelTestbed { get; set; } = true;

    public string LocalModelEndpoint { get; set; } = "http://localhost:11434";

    public string LocalModelName { get; set; } = "gemma4:e4b";

    public bool EnableGeminiBackendProcessing { get; set; }

    public string GeminiFastModelName { get; set; } = "gemini-2.5-flash-lite";

    public string GeminiThinkingModelName { get; set; } = "gemini-3-flash-preview";
}
