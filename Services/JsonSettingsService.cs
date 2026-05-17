using System.IO;
using System.Text.Json;
using VitaMR.Models;

namespace VitaMR.Services;

public sealed class JsonSettingsService : ISettingsService
{
    private readonly string _settingsPath;

    public JsonSettingsService()
    {
        var settingsFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VitaMR");

        _settingsPath = Path.Combine(settingsFolder, "settings.json");
    }

    public AppSettings Load()
    {
        if (!File.Exists(_settingsPath))
        {
            return CreateDefaultSettings();
        }

        try
        {
            var json = File.ReadAllText(_settingsPath);
            var loaded = JsonSerializer.Deserialize<AppSettings>(json) ?? CreateDefaultSettings();
            return MigrateSettings(loaded);
        }
        catch
        {
            return CreateDefaultSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        var folder = Path.GetDirectoryName(_settingsPath);

        if (!string.IsNullOrWhiteSpace(folder))
        {
            Directory.CreateDirectory(folder);
        }

        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        File.WriteAllText(_settingsPath, json);
    }

    private static AppSettings CreateDefaultSettings()
    {
        var defaultVaultRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "VitaMR_TestVault");

        return new AppSettings
        {
            VaultRootPath = defaultVaultRoot,
            ActiveChartId = "VITA-0001",
            ActivePatientDisplayName = "Synthetic Test Patient",
            LastActiveChartId = string.Empty,
            MostRecentlyCreatedChartId = string.Empty,
            IsInitialSetupComplete = false,
            InitialSetupStep = "chart_manager_name",
            ActiveUserName = string.Empty,
            ActiveUserRole = "Chart Manager",
            AgentDisplayName = "Dolly",
            AgentTone = "Friendly",
            ChartManagerNickname = string.Empty,
            ChartManagerPhotoPath = string.Empty,
            ChartManagerPersonaStyle = string.Empty,
            SetupPatientNames = string.Empty,
            SetupAdditionalUsers = string.Empty,
            SetupPersonaPreference = string.Empty,
            IsSyntheticTestMode = true,
            DefaultSourceSystem = "Unknown",
            DefaultSourceFacility = "Unknown",
            DefaultSourceType = "Unknown",
            EnableLocalModelTestbed = true,
            LocalModelEndpoint = "http://localhost:11434",
            LocalModelName = "gemma4:e4b",
            SelectedModelRoute = "Dolly Main Agent",
            SelectedAiProvider = "Gemini",
            EnableHealthspanMode = false,
            HealthspanCoachingMode = "Record Assistant Mode",
            SelectedOmniboxAction = "Ask / Update Existing Patient",
            VaultOwnerName = "Local Owner",
            VaultOwnerRole = "Project Owner",
            VaultOwnerDeletePassword = "CHANGE_ME",
            ChartManagerName = "Chart Manager",
            ChartManagerPassword = "CHANGE_ME",
            EnableGeminiBackendProcessing = false,
            GeminiFastModelName = "gemini-2.5-flash-lite",
            GeminiThinkingModelName = "gemini-3-flash-preview"
        };
    }

    private AppSettings MigrateSettings(AppSettings settings)
    {
        var changed = false;

        if (string.IsNullOrWhiteSpace(settings.VaultRootPath))
        {
            settings.VaultRootPath = CreateDefaultSettings().VaultRootPath;
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(settings.ActiveChartId))
        {
            settings.ActiveChartId = "VITA-0001";
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(settings.ActivePatientDisplayName))
        {
            settings.ActivePatientDisplayName = "Synthetic Test Patient";
            changed = true;
        }

        settings.LastActiveChartId = string.IsNullOrWhiteSpace(settings.LastActiveChartId)
            ? string.Empty
            : settings.LastActiveChartId.Trim().ToUpperInvariant();
        settings.MostRecentlyCreatedChartId = string.IsNullOrWhiteSpace(settings.MostRecentlyCreatedChartId)
            ? string.Empty
            : settings.MostRecentlyCreatedChartId.Trim().ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(settings.AgentDisplayName))
        {
            settings.AgentDisplayName = "Dolly";
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(settings.InitialSetupStep))
        {
            settings.InitialSetupStep = settings.IsInitialSetupComplete
                ? "complete"
                : "chart_manager_name";
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(settings.AgentTone))
        {
            settings.AgentTone = "Friendly";
            changed = true;
        }

        settings.ChartManagerNickname = string.IsNullOrWhiteSpace(settings.ChartManagerNickname)
            ? string.Empty
            : settings.ChartManagerNickname.Trim();
        settings.ChartManagerPhotoPath = string.IsNullOrWhiteSpace(settings.ChartManagerPhotoPath)
            ? string.Empty
            : settings.ChartManagerPhotoPath.Trim();
        settings.ChartManagerPersonaStyle = string.IsNullOrWhiteSpace(settings.ChartManagerPersonaStyle)
            ? string.Empty
            : settings.ChartManagerPersonaStyle.Trim();

        if (string.IsNullOrWhiteSpace(settings.ActiveUserRole))
        {
            settings.ActiveUserRole = "Chart Manager";
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(settings.DefaultSourceSystem))
        {
            settings.DefaultSourceSystem = "Unknown";
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(settings.DefaultSourceFacility))
        {
            settings.DefaultSourceFacility = "Unknown";
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(settings.DefaultSourceType))
        {
            settings.DefaultSourceType = "Unknown";
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(settings.LocalModelEndpoint))
        {
            settings.LocalModelEndpoint = "http://localhost:11434";
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(settings.LocalModelName))
        {
            settings.LocalModelName = "gemma4:e4b";
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(settings.SelectedModelRoute))
        {
            settings.SelectedModelRoute = "Dolly Main Agent";
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(settings.HealthspanCoachingMode))
        {
            settings.HealthspanCoachingMode = "Record Assistant Mode";
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(settings.SelectedOmniboxAction))
        {
            settings.SelectedOmniboxAction = "Ask / Update Existing Patient";
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(settings.VaultOwnerName))
        {
            settings.VaultOwnerName = "Local Owner";
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(settings.VaultOwnerRole))
        {
            settings.VaultOwnerRole = "Project Owner";
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(settings.VaultOwnerDeletePassword))
        {
            settings.VaultOwnerDeletePassword = "CHANGE_ME";
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(settings.ChartManagerName))
        {
            settings.ChartManagerName = string.IsNullOrWhiteSpace(settings.VaultOwnerName)
                ? "Chart Manager"
                : settings.VaultOwnerName;
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(settings.ChartManagerPassword))
        {
            settings.ChartManagerPassword = string.IsNullOrWhiteSpace(settings.VaultOwnerDeletePassword)
                ? "CHANGE_ME"
                : settings.VaultOwnerDeletePassword;
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(settings.GeminiFastModelName))
        {
            settings.GeminiFastModelName = "gemini-2.5-flash-lite";
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(settings.GeminiThinkingModelName))
        {
            settings.GeminiThinkingModelName = "gemini-3-flash-preview";
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(settings.SelectedAiProvider))
        {
            settings.SelectedAiProvider = "Gemini";
            changed = true;
        }

        if (changed)
        {
            Save(settings);
        }

        return settings;
    }
}
