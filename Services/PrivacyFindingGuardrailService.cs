using System.Text.RegularExpressions;
using VitaMR.Models;

namespace VitaMR.Services;

public static class PrivacyFindingGuardrailService
{
    private static readonly StringComparer TextComparer = StringComparer.OrdinalIgnoreCase;

    private static readonly HashSet<string> NamedClinicalTerms = new(TextComparer)
    {
        "Addison disease",
        "Alzheimer disease",
        "Alzheimer's disease",
        "Bell palsy",
        "Crohn disease",
        "Crohn's disease",
        "Cushing syndrome",
        "Down syndrome",
        "Ehlers-Danlos syndrome",
        "Gilbert syndrome",
        "Graves disease",
        "Guillain-Barre syndrome",
        "Guillain-Barre",
        "Hashimoto thyroiditis",
        "Hodgkin lymphoma",
        "Huntington disease",
        "Kaposi sarcoma",
        "Klinefelter syndrome",
        "Lou Gehrig disease",
        "Lynch Syndrome",
        "Marfan syndrome",
        "Parkinson disease",
        "Parkinson's Disease",
        "Raynaud phenomenon",
        "Sjogren syndrome",
        "Tourette syndrome",
        "Turner syndrome",
        "Wilms tumor",
        "Wilson disease",
        "Wolff-Parkinson-White syndrome"
    };

    private static readonly Regex RelativeTimingPattern = new(
        @"\b(?:(?:in|for|after|within|over)\s+(?:\d+|one|two|three|four|five|six|seven|eight|nine|ten|eleven|twelve)\s+(?:day|days|week|weeks|month|months|year|years)|\d+\s*-\s*(?:day|days|week|weeks|month|months|year|years)\s+(?:history|course|duration)|post-?op(?:erative)?\s+day\s+\d+|follow\s+up\s+in\s+(?:\d+|one|two|three|four|five|six|seven|eight|nine|ten|eleven|twelve)\s+(?:day|days|week|weeks|month|months|year|years)|several\s+(?:days|weeks|months|years))\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static IReadOnlyList<LocalModelFinding> FilterAndOrderFindings(IEnumerable<LocalModelFinding> findings)
    {
        var accepted = new Dictionary<string, LocalModelFinding>(StringComparer.OrdinalIgnoreCase);

        foreach (var finding in findings)
        {
            if (ShouldSkip(finding))
            {
                continue;
            }

            var normalizedText = NormalizeText(finding.Text);
            var normalizedReplacement = string.IsNullOrWhiteSpace(finding.SuggestedReplacement)
                ? string.Empty
                : finding.SuggestedReplacement.Trim();
            var key = $"{finding.Type.Trim()}|{normalizedText}|{normalizedReplacement}";

            accepted.TryAdd(key, new LocalModelFinding
            {
                Type = finding.Type.Trim(),
                Text = finding.Text.Trim(),
                SuggestedReplacement = normalizedReplacement
            });
        }

        return accepted.Values
            .OrderByDescending(finding => finding.Text.Length)
            .ThenBy(finding => finding.Text, TextComparer)
            .ToList();
    }

    private static bool ShouldSkip(LocalModelFinding finding)
    {
        if (string.IsNullOrWhiteSpace(finding.Text))
        {
            return true;
        }

        var text = NormalizeText(finding.Text);

        if (NamedClinicalTerms.Contains(text))
        {
            return true;
        }

        if (IsMedicalConditionFinding(finding.Type) &&
            NamedClinicalTerms.Any(term => text.Contains(NormalizeText(term), StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return RelativeTimingPattern.IsMatch(text);
    }

    private static bool IsMedicalConditionFinding(string type)
    {
        return type.Contains("condition", StringComparison.OrdinalIgnoreCase) ||
               type.Contains("diagnosis", StringComparison.OrdinalIgnoreCase) ||
               type.Contains("disease", StringComparison.OrdinalIgnoreCase) ||
               type.Contains("syndrome", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeText(string value)
    {
        return Regex.Replace(value.Trim(), @"\s+", " ");
    }
}
