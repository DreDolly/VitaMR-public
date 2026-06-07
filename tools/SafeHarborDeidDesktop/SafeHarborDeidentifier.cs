using System.Text.Json;
using System.Text.RegularExpressions;
using System.IO;

namespace SafeHarborDeidDesktop;

internal sealed class SafeHarborDeidentifier
{
    private readonly IReadOnlyList<Detector> _detectors = BuildDetectors();

    public DeidResult Deidentify(string text, string sourceName)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new DeidResult("blocked", string.Empty, [], [], ["No readable text was supplied."]);
        }

        var redacted = RedactAgesOver89(text, out var findings);
        redacted = RedactKnownAndContextualNames(redacted, findings);

        foreach (var detector in _detectors)
        {
            redacted = detector.Pattern.Replace(redacted, match =>
            {
                findings.Add(new Finding(detector.Category, match.Value, detector.Replacement));
                return detector.Replacement;
            });
        }

        var warnings = BuildWarnings(redacted, sourceName);
        var blocked = BuildBlockers(sourceName);
        var status = blocked.Count > 0
            ? "blocked"
            : warnings.Count > 0
                ? "needs_review"
                : "safe_harbor_candidate";
        redacted = CollapseRepeatedTokens(redacted);

        return new DeidResult(status, redacted, findings, warnings, blocked);
    }

    private static IReadOnlyList<Detector> BuildDetectors()
    {
        const RegexOptions flags = RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled;
        return
        [
            new("email_address", new Regex(@"\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b", flags), "[EMAIL]"),
            new("url", new Regex(@"\b(?:https?://|www\.)[^\s<>()]+", flags), "[URL]"),
            new("ip_address", new Regex(@"\b(?:(?:25[0-5]|2[0-4]\d|1?\d?\d)\.){3}(?:25[0-5]|2[0-4]\d|1?\d?\d)\b", RegexOptions.Compiled), "[IP_ADDRESS]"),
            new("ssn", new Regex(@"\b\d{3}[- ]\d{2}[- ]\d{4}\b", RegexOptions.Compiled), "[SSN]"),
            new("phone_or_fax", new Regex(@"(?<!\w)(?:\+?1[-.\s]?)?(?:\(?\d{3}\)?[-.\s]?)\d{3}[-.\s]?\d{4}(?:\s*(?:x|ext\.?)\s*\d{1,6})?(?!\w)", flags), "[PHONE_OR_FAX]"),
            new("date", new Regex(@"\b(?:\d{1,2}[/-]\d{1,2}[/-](?:\d{2}|\d{4})|\d{4}-\d{2}-\d{2}|(?:Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Sept|Oct|Nov|Dec)[a-z]*\.?\s+\d{1,2},?\s+\d{4}|\d{1,2}\s+(?:Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Sept|Oct|Nov|Dec)[a-z]*\.?\s+\d{4})\b", flags), "[DATE]"),
            new("zip_code", new Regex(@"\b\d{5}(?:-\d{4})?\b", RegexOptions.Compiled), "[ZIP]"),
            new("medical_record_number", new Regex(@"\b(?:MRN|Medical Record(?: Number)?|Patient ID|Chart ID)\s*[:#-]?\s*[A-Z0-9-]{3,}\b", flags), "[MEDICAL_RECORD_NUMBER]"),
            new("health_plan_beneficiary_number", new Regex(@"\b(?:Member ID|Plan ID|Policy ID|Subscriber ID|Health Plan ID|Medicare Beneficiary Identifier|MBI)\s*[:#-]?\s*[A-Z0-9-]{3,}\b", flags), "[HEALTH_PLAN_ID]"),
            new("account_or_claim_number", new Regex(@"\b(?:Account|Acct|Claim|Accession|Invoice|Guarantor)\s*(?:Number|No\.?|ID)?\s*[:#-]?\s*[A-Z0-9-]{4,}\b", flags), "[ACCOUNT_OR_CLAIM_NUMBER]"),
            new("vehicle_identifier", new Regex(@"\b(?:VIN|Vehicle ID|License Plate|Plate)\s*[:#-]?\s*[A-Z0-9][A-Z0-9 -]{1,20}\b", flags), "[VEHICLE_IDENTIFIER]"),
            new("vin", new Regex(@"\b[A-HJ-NPR-Z0-9]{17}\b", flags), "[VEHICLE_IDENTIFIER]"),
            new("device_identifier", new Regex(@"\b(?:Serial|S/N|SN|Device ID|Device Serial|UDI|IMEI|MEID|ICCID|MAC)\s*[:#-]?\s*[A-Z0-9:.-]{4,}\b", flags), "[DEVICE_IDENTIFIER]"),
            new("mac_address", new Regex(@"\b(?:[0-9A-F]{2}[:-]){5}[0-9A-F]{2}\b", flags), "[DEVICE_IDENTIFIER]"),
            new("certificate_or_license_number", new Regex(@"\b(?!(?:License\s+Plate|Plate)\b)(?:License|Licence|Certificate|Certification|DEA|NPI)\s*(?:Number|No\.?|ID)?\s*[:#-]?\s*[A-Z0-9-]{4,}\b", flags), "[CERTIFICATE_OR_LICENSE]"),
            new("street_address", new Regex(@"(?<![-\w])\d{1,6}\s+[A-Za-z0-9 .'-]+\s+(?:Street|St|Avenue|Ave|Road|Rd|Boulevard|Blvd|Drive|Dr|Lane|Ln|Court|Ct|Way|Circle|Cir|Place|Pl|Terrace|Ter|Parkway|Pkwy)\b(?:,\s*[A-Za-z .'-]+,\s*[A-Z]{2}\s+\d{5}(?:-\d{4})?)?", flags), "[ADDRESS]"),
            new("biometric_identifier", new Regex(@"\b(?:fingerprint|voiceprint|voice print|retina scan|iris scan|palm print|faceprint|face print)\b", flags), "[BIOMETRIC_IDENTIFIER]"),
            new("geocode", new Regex(@"\b(?:lat(?:itude)?|lon(?:gitude)?|gps|geocode)\s*[:=]\s*-?\d{1,3}\.\d+\s*,?\s*-?\d{1,3}\.\d+\b", flags), "[GEOCODE]"),
            new("full_name", new Regex(@"\b(?:Mr|Mrs|Ms|Miss|Dr)\.?\s+[A-Z][A-Za-z'.-]+(?:[ \t]+[A-Z][A-Za-z'.-]+){0,3}\b", RegexOptions.Compiled), "[NAME]"),
            new("facility_or_school", new Regex(@"\b[A-Z][A-Za-z'.-]+(?:[ \t]+[A-Z][A-Za-z'.-]+){0,4}[ \t]+(?:Hospital|Clinic|Medical School|High School|University|College|Institute|Center|Centre|School)\b", RegexOptions.Multiline | RegexOptions.Compiled), "[FACILITY_OR_SCHOOL]"),
            new("named_trial_or_program", new Regex(@"\b[A-Z][A-Za-z'.-]+/[A-Z0-9-]+[ \t]+(?:Trial|Program|Study|Registry)\b", RegexOptions.Multiline | RegexOptions.Compiled), "[FACILITY_OR_SCHOOL]"),
            new("station_or_unit", new Regex(@"\b(?:Station|Unit|Ward|Floor)\s+[A-Z0-9-]{1,12}\b", flags), "[FACILITY_UNIT]"),
            new("other_unique_code", new Regex(@"\b(?:Token|API Key|Auth Code|Portal Username|Username|Unique ID|Record Locator)\s*[:#= -]?\s*[A-Z0-9._-]{4,}\b", flags), "[UNIQUE_IDENTIFIER]")
        ];
    }

    private static string RedactAgesOver89(string text, out List<Finding> findings)
    {
        findings = [];
        var localFindings = findings;
        var pattern = new Regex(
            @"\b(?:age\s*)?(?:9[0-9]|1[01][0-9]|120)(?:\s*[- ]?\s*(?:year|yr)s?\s*old)?\b(?!\s*%)(?!\s+(?:Street|St|Avenue|Ave|Road|Rd|Boulevard|Blvd|Drive|Dr|Lane|Ln|Court|Ct|Way|Circle|Cir|Place|Pl|Terrace|Ter|Parkway|Pkwy)\b)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        return pattern.Replace(text, match =>
        {
            localFindings.Add(new Finding("age_over_89", match.Value, "[AGE_90_OR_OLDER]"));
            return "[AGE_90_OR_OLDER]";
        });
    }

    private static string RedactKnownAndContextualNames(string text, List<Finding> findings)
    {
        var names = CollectLikelyNames(text);
        var redacted = text;

        foreach (var name in names.OrderByDescending(name => name.Length))
        {
            var escaped = Regex.Escape(name);
            redacted = Regex.Replace(
                redacted,
                $@"\b{escaped}(?:['’]s)?\b",
                match =>
                {
                    findings.Add(new Finding("person_name", match.Value, "[NAME]"));
                    return "[NAME]";
                },
                RegexOptions.IgnoreCase);

            foreach (var part in name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var normalizedPart = part.Trim('\'', '.', '-');
                if (normalizedPart.Length < 4 || IsHonorific(normalizedPart) || IsCommonNonNameToken(normalizedPart))
                {
                    continue;
                }

                redacted = Regex.Replace(
                    redacted,
                    $@"(?<!\[)\b{Regex.Escape(normalizedPart)}(?:['’]s)?\b(?!\])",
                    match =>
                    {
                        findings.Add(new Finding("person_name_part", match.Value, "[NAME]"));
                        return "[NAME]";
                    },
                    RegexOptions.IgnoreCase);
            }
        }

        return redacted;
    }

    private static HashSet<string> CollectLikelyNames(string text)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var namePattern = @"(?<name>[A-Z][A-Za-z'-]+(?:[ \t]+[A-Z][A-Za-z'-]+){1,3})";
        var patterns = new[]
        {
            $@"\*\*(?i:patient|pt|name|provider|doctor|dr|clinician|guardian|parent|mother|father|sibling|spouse|wife|husband|relative|emergency contact):\*\*\s*(?:\*)?(?i:Dr\.?\s*)?{namePattern}",
            $@"\*\*(?i:referring provider|electronically signed|signed):\*\*\s*(?:\*)?(?i:Dr\.?\s*)?{namePattern}",
            $@"(?:^|[\r\n\s*#>])(?:\*{{0,2}})?(?i:patient|pt|name|provider|doctor|dr|clinician|guardian|parent|mother|father|sibling|spouse|wife|husband|relative|emergency contact)(?:\*{{0,2}})?\s*:\s*(?:\*)?(?i:Dr\.?\s*)?{namePattern}",
            $@"(?:^|[\r\n\s*#>])(?:\*{{0,2}})?(?i:referring provider|electronically signed|signed)(?:\*{{0,2}})?\s*:\s*(?:\*)?(?i:Dr\.?\s*)?{namePattern}",
            $@"\b(?i:Dr|Mr|Mrs|Ms|Miss)\.?\s+{namePattern}",
            $@"\b(?i:guardian|parent|mother|father|sibling|spouse|wife|husband|relative|peer|caregiver|provider|doctor|clinician|partner|friend)\s*,?\s+(?i:Dr\.?\s*)?{namePattern}",
            $@"\b(?i:friends|companions|contacts|caregivers)\s*:\s*{namePattern}",
            $@"\b(?i:friends|companions|contacts|caregivers)\s*:\s*[A-Z][A-Za-z'-]+(?:[ \t]+[A-Z][A-Za-z'-]+){{1,3}}\s+(?:and|,)\s*{namePattern}",
            $@"\((?:Dr\.?\s*)?{namePattern}\)",
        };

        foreach (var pattern in patterns)
        {
            foreach (Match match in Regex.Matches(text, pattern))
            {
                var name = match.Groups["name"].Value.Trim();
                if (!string.IsNullOrWhiteSpace(name) && !LooksLikeClinicalPhrase(name))
                {
                    names.Add(name);
                }
            }
        }

        return names;
    }

    private static string CollapseRepeatedTokens(string text)
    {
        var collapsed = Regex.Replace(text, @"(?:\[NAME\]\s+){1,}\[NAME\]", "[NAME]");
        collapsed = Regex.Replace(collapsed, @"(?:\[FACILITY_OR_SCHOOL\]\s*/\s*)+\[FACILITY_OR_SCHOOL\]", "[FACILITY_OR_SCHOOL] / [FACILITY_OR_SCHOOL]");
        return collapsed;
    }

    private static bool LooksLikeClinicalPhrase(string value)
    {
        var lower = Regex.Replace(value, @"\s+", " ").Trim().ToLowerInvariant();
        return lower.Contains("internal medicine", StringComparison.Ordinal) ||
               lower.Contains("clinical trial", StringComparison.Ordinal) ||
               lower.Contains("athletic clearance", StringComparison.Ordinal) ||
               lower.Contains("medical clearance", StringComparison.Ordinal) ||
               lower.Contains("adjustment disorder", StringComparison.Ordinal) ||
               lower.Contains("hypertrophic obstructive cardiomyopathy", StringComparison.Ordinal) ||
               lower.Contains("systolic anterior motion", StringComparison.Ordinal) ||
               lower.Contains("cardiac mri", StringComparison.Ordinal) ||
               lower.Contains("stress echo", StringComparison.Ordinal) ||
               lower.Contains("stem cell", StringComparison.Ordinal) ||
               lower.Contains("varsity football", StringComparison.Ordinal) ||
               lower.Contains("hospital", StringComparison.Ordinal) ||
               lower.Contains("clinic", StringComparison.Ordinal) ||
               lower.Contains("medical school", StringComparison.Ordinal) ||
               lower.Contains("high school", StringComparison.Ordinal) ||
               lower.Contains("university", StringComparison.Ordinal) ||
               lower.Contains("college", StringComparison.Ordinal) ||
               lower.Contains("institute", StringComparison.Ordinal) ||
               lower.Contains("center", StringComparison.Ordinal) ||
               lower.Contains("centre", StringComparison.Ordinal);
    }

    private static bool IsHonorific(string value)
    {
        return value.Equals("Dr", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("Mr", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("Mrs", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("Ms", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("Miss", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCommonNonNameToken(string value)
    {
        return value.Equals("Post", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("Clinical", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("Trial", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("Review", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("Athletic", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("Clearance", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("Medicine", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("Internal", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("Activity", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("Follow", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("Hospital", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("Specialist", StringComparison.OrdinalIgnoreCase);
    }

    private static List<string> BuildWarnings(string text, string sourceName)
    {
        var warnings = new List<string>();
        if (Regex.IsMatch(text, @"\b(?:county|precinct|school|employer|workplace|church|mosque|synagogue|temple|shelter|prison|jail)\b", RegexOptions.IgnoreCase))
        {
            warnings.Add("Context may contain sub-state geography, employer, school, or institution details that require human review.");
        }

        if (Regex.IsMatch(text, @"\b(?:rare|only|sole|unique|mayor|celebrity|professional athlete|public figure)\b", RegexOptions.IgnoreCase))
        {
            warnings.Add("Context may contain unique characteristics that could identify a person.");
        }

        if (IsVisualOrMediaFile(sourceName))
        {
            warnings.Add("Visual or media files can contain faces, comparable images, voice prints, or biometric identifiers.");
        }

        return warnings;
    }

    private static List<string> BuildBlockers(string sourceName)
    {
        if (string.IsNullOrWhiteSpace(sourceName))
        {
            return [];
        }

        var extension = Path.GetExtension(sourceName);
        if (IsVisualOrMediaFile(sourceName))
        {
            return ["Visual/audio/video PHI review is required before this file can be marked as Safe Harbor candidate."];
        }

        if (extension.Equals(".doc", StringComparison.OrdinalIgnoreCase))
        {
            return ["Legacy DOC extraction is not supported. Convert to DOCX or text first."];
        }

        return [];
    }

    private static bool IsVisualOrMediaFile(string sourceName)
    {
        var extension = Path.GetExtension(sourceName);
        return extension.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".tif", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".tiff", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".bmp", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".webp", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".gif", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".mp3", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".wav", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".m4a", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".mp4", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".mov", StringComparison.OrdinalIgnoreCase);
    }
}

internal sealed record Detector(string Category, Regex Pattern, string Replacement);

internal sealed record Finding(string Category, string Text, string Replacement);

internal sealed record DeidResult(
    string Status,
    string RedactedText,
    IReadOnlyList<Finding> Findings,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> BlockedReasons,
    AiReviewResult? AiReview = null)
{
    public string ToAuditJson()
    {
        return JsonSerializer.Serialize(this, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }
}
