using System.Text.RegularExpressions;
using VitaMR.Models;

namespace VitaMR.Services;

public sealed partial class CSharpResponsePersonalizationService : IResponsePersonalizationService
{
    public string Personalize(
        string backendAnswer,
        ChartContext? chartContext,
        IReadOnlyList<PatientIdentityRecord> knownPatients)
    {
        if (string.IsNullOrWhiteSpace(backendAnswer))
        {
            return backendAnswer;
        }

        var personalized = backendAnswer;

        if (chartContext is not null && !string.IsNullOrWhiteSpace(chartContext.LocalDisplayName))
        {
            var firstName = ExtractFirstName(chartContext.LocalDisplayName);
            personalized = PatientPlaceholderPattern().Replace(
                personalized,
                firstName);
            personalized = PersonPlaceholderPattern().Replace(
                personalized,
                firstName);
        }

        foreach (var patient in knownPatients
                     .Where(patient => !string.IsNullOrWhiteSpace(patient.PatientDisplayName))
                     .OrderByDescending(patient => patient.PatientDisplayName.Length))
        {
            var displayName = patient.PatientDisplayName.Trim();
            var firstName = ExtractFirstName(displayName);

            personalized = ReplaceNameVariant(personalized, displayName, firstName);

            if (!string.IsNullOrWhiteSpace(patient.NormalizedName) &&
                !patient.NormalizedName.Equals(displayName, StringComparison.OrdinalIgnoreCase))
            {
                personalized = ReplaceNameVariant(personalized, patient.NormalizedName, firstName);
            }
        }

        var chartMatches = ChartPlaceholderPattern().Matches(personalized);
        var knownCharts = knownPatients
            .Where(patient => !string.IsNullOrWhiteSpace(patient.ChartId))
            .GroupBy(patient => patient.ChartId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last().PatientDisplayName, StringComparer.OrdinalIgnoreCase);

        foreach (Match match in chartMatches)
        {
            var chartId = match.Groups["chart"].Value;

            if (!knownCharts.TryGetValue(chartId, out var displayName) ||
                string.IsNullOrWhiteSpace(displayName))
            {
                continue;
            }

            personalized = personalized.Replace(match.Value, ExtractFirstName(displayName), StringComparison.OrdinalIgnoreCase);
        }

        return personalized;
    }

    private static string ReplaceNameVariant(string value, string nameVariant, string firstName)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            string.IsNullOrWhiteSpace(nameVariant) ||
            string.IsNullOrWhiteSpace(firstName))
        {
            return value;
        }

        var trimmed = nameVariant.Trim();

        if (trimmed.Equals(firstName, StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        var pattern = $@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(trimmed)}(?![\p{{L}}\p{{N}}])";
        return Regex.Replace(value, pattern, firstName, RegexOptions.IgnoreCase);
    }

    private static string ExtractFirstName(string displayName)
    {
        var words = displayName
            .Trim()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return words.Length == 0
            ? displayName.Trim()
            : words[0];
    }

    [GeneratedRegex(@"\[PATIENT\]", RegexOptions.IgnoreCase)]
    private static partial Regex PatientPlaceholderPattern();

    [GeneratedRegex(@"\[(?:PERSON|NAME|PATIENT_NAME|PATIENT_DISPLAY_NAME)\]", RegexOptions.IgnoreCase)]
    private static partial Regex PersonPlaceholderPattern();

    [GeneratedRegex(@"\[CHART:(?<chart>VITA-\d{4,6})\]", RegexOptions.IgnoreCase)]
    private static partial Regex ChartPlaceholderPattern();
}
