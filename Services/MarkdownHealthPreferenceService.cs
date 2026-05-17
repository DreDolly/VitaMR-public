using System.IO;
using System.Text.RegularExpressions;
using VitaMR.Models;

namespace VitaMR.Services;

public sealed class MarkdownHealthPreferenceService : IHealthPreferenceService
{
    private const int AdultAgeYears = 18;

    public void EnsureUserPreferenceScaffold(string vaultRoot, string userName)
    {
        var folder = GetUserFolder(vaultRoot, userName);
        Directory.CreateDirectory(folder);

        EnsureFile(Path.Combine(folder, "User_Profile.md"), $"""
        # User Profile: {userName}

        ## Role
        - vault_owner: true
        - preferred_name: {userName}

        ## Notes
        User profile stores preferences and communication style. It is separate from patient chart medical facts.
        """);

        EnsureFile(Path.Combine(folder, "Health_Northstar.md"), """
        # Health Northstar

        > User-owned priorities Dolly should optimize around. Prototype mode only; no diagnosis, triage, prescriptions, or clinical orders.

        | Priority | Why It Matters | Current Focus | Status | Updated |
        |---|---|---|---|---|
        """);

        EnsureFile(Path.Combine(folder, "Goal_Compass.md"), """
        # Goal Compass

        | Goal | Support Style | Barriers | Check-In Cadence | Status | Updated |
        |---|---|---|---|---|---|
        """);

        EnsureFile(Path.Combine(folder, "Weekly_Checkins.md"), """
        # Weekly Check-ins

        ## Check-in Questions
        1. What felt better this week?
        2. What felt harder this week?
        3. How was sleep overall?
        4. How was movement or fitness?
        5. Any symptoms, stressors, or barriers worth tracking?
        6. Keep, change, pause, or add a goal?

        """);

        EnsureFile(Path.Combine(folder, "Communication_Preferences.md"), """
        # Communication Preferences

        | Preference | Current Setting | Notes | Updated |
        |---|---|---|---|
        | reminder_style | not set | direct / gentle / data-first / quiet tracking | |
        | answer_length | concise | Dolly should default to short, useful answers. | |
        | coaching_tone | not set | Encouraging without pressure. | |
        """);

        EnsureFile(Path.Combine(folder, "Dolly_Coaching_Boundaries.md"), """
        # Dolly Coaching Boundaries

        - Dolly may help track user-stated goals and preferences.
        - Dolly may ask brief weekly check-in questions.
        - Dolly may frame reminders around the user's declared priorities.
        - Dolly must not diagnose, triage, prescribe, order tests, or give emergency instructions.
        - Dolly must preserve user autonomy; goals may be changed, paused, or deleted by the user.
        """);
    }

    public void EnsureAdultUserPreferenceScaffolds(
        string vaultRoot,
        IEnumerable<PatientIdentityRecord> identities,
        DateTime now)
    {
        foreach (var identity in identities)
        {
            if (IsAdult(identity, now))
            {
                EnsureUserPreferenceScaffold(vaultRoot, identity.PatientDisplayName);
            }
        }
    }

    public HealthPreferenceResult CaptureIfPreferenceMention(
        string vaultRoot,
        string userName,
        string userText,
        string sessionId)
    {
        var result = new HealthPreferenceResult
        {
            WasRun = true,
            UserName = userName,
            Status = "HEALTH_PREFERENCE_SCANNED"
        };

        if (!LooksLikePreference(userText))
        {
            return result;
        }

        EnsureUserPreferenceScaffold(vaultRoot, userName);
        var folder = GetUserFolder(vaultRoot, userName);
        var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var summary = BuildSummary(userText);

        var checkinsPath = Path.Combine(folder, "Weekly_Checkins.md");
        File.AppendAllText(checkinsPath, $"""
        ## {now} - Captured Preference / Check-in
        - Session: {sessionId}
        - User statement: {EscapeLine(userText)}
        - Summary: {EscapeLine(summary)}

        """);

        var northstarPath = Path.Combine(folder, "Health_Northstar.md");
        File.AppendAllText(northstarPath, $"| {EscapeTable(summary)} | user-stated priority | active preference/check-in | active | {DateTime.Now:yyyy-MM-dd} |{Environment.NewLine}");

        var compassPath = Path.Combine(folder, "Goal_Compass.md");
        File.AppendAllText(compassPath, $"| {EscapeTable(summary)} | user preference guided | captured from conversation | weekly | active | {DateTime.Now:yyyy-MM-dd} |{Environment.NewLine}");

        result.WasCaptured = true;
        result.Status = "HEALTH_PREFERENCE_CAPTURED";
        result.Summary = summary;
        result.UpdatedFiles.AddRange([checkinsPath, northstarPath, compassPath]);
        return result;
    }

    public string BuildPreferenceContext(string vaultRoot, string userName)
    {
        EnsureUserPreferenceScaffold(vaultRoot, userName);
        var folder = GetUserFolder(vaultRoot, userName);
        var files = new[]
        {
            "Health_Northstar.md",
            "Goal_Compass.md",
            "Communication_Preferences.md",
            "Dolly_Coaching_Boundaries.md"
        };

        return string.Join(
            Environment.NewLine + Environment.NewLine,
            files.Select(file => File.ReadAllText(Path.Combine(folder, file)).Trim()));
    }

    public string BuildWeeklyCheckInPromptIfDue(string vaultRoot, string userName, DateTime now)
    {
        EnsureUserPreferenceScaffold(vaultRoot, userName);
        var folder = GetUserFolder(vaultRoot, userName);
        var markerPath = Path.Combine(folder, ".weekly_checkin_prompted");

        if (File.Exists(markerPath) &&
            DateTime.TryParse(File.ReadAllText(markerPath).Trim(), out var lastPrompted) &&
            (now.Date - lastPrompted.Date).TotalDays < 7)
        {
            return string.Empty;
        }

        File.WriteAllText(markerPath, now.ToString("yyyy-MM-dd"));

        return $"I have a quick weekly health-preference check-in ready for {userName}. Want to answer a few short questions about sleep, movement, energy, stress, and whether your goals should stay the same?";
    }

    private static bool LooksLikePreference(string value)
    {
        return Regex.IsMatch(
            value,
            @"\b(my health goal|my northstar|my priority|i want to focus on|i want dolly to|weekly check.?in|sleep|fitness|exercise|movement|nutrition|stress|energy|weight|steps|workout|habit)\b",
            RegexOptions.IgnoreCase);
    }

    private static bool IsAdult(PatientIdentityRecord identity, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(identity.PatientDisplayName) ||
            string.IsNullOrWhiteSpace(identity.DateOfBirth) ||
            !DateTime.TryParse(identity.DateOfBirth, out var dob))
        {
            return false;
        }

        var age = now.Year - dob.Year;
        if (dob.Date > now.Date.AddYears(-age))
        {
            age--;
        }

        return age >= AdultAgeYears;
    }

    private static string BuildSummary(string value)
    {
        var cleaned = Regex.Replace(value.Trim(), @"\s+", " ");
        return cleaned.Length <= 110 ? cleaned : cleaned[..110].Trim() + "...";
    }

    private static string GetUserFolder(string vaultRoot, string userName)
    {
        var safeUserName = Regex.Replace(string.IsNullOrWhiteSpace(userName) ? "User" : userName.Trim(), @"[^A-Za-z0-9_-]+", "_");
        return Path.Combine(vaultRoot, "_System", "Users", safeUserName);
    }

    private static void EnsureFile(string path, string content)
    {
        if (File.Exists(path))
        {
            return;
        }

        File.WriteAllText(path, content.Replace("\r\n", "\n").TrimStart().TrimEnd() + Environment.NewLine);
    }

    private static string EscapeTable(string value)
    {
        return EscapeLine(value).Replace("|", "/", StringComparison.Ordinal);
    }

    private static string EscapeLine(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "none"
            : Regex.Replace(value.Replace("\r", " ").Replace("\n", " "), @"\s+", " ").Trim();
    }
}
