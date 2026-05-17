using VitaMR.Models;

namespace VitaMR.Services;

public interface IHealthPreferenceService
{
    void EnsureUserPreferenceScaffold(string vaultRoot, string userName);

    void EnsureAdultUserPreferenceScaffolds(
        string vaultRoot,
        IEnumerable<PatientIdentityRecord> identities,
        DateTime now);

    HealthPreferenceResult CaptureIfPreferenceMention(
        string vaultRoot,
        string userName,
        string userText,
        string sessionId);

    string BuildPreferenceContext(string vaultRoot, string userName);

    string BuildWeeklyCheckInPromptIfDue(string vaultRoot, string userName, DateTime now);
}
