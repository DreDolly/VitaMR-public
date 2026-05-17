using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using VitaMR.Models;

namespace VitaMR.Services;

public sealed class VitaMasteryService : IVitaMasteryService
{
    private static readonly DateOnly SourceCheckedDate = new(2026, 5, 12);

    private static readonly IReadOnlyList<VitaMasteryQuest> Catalog =
    [
        Quest("core.identity", "Confirm identity basics", VitaMasteryStage.Foundation, "all", 2, "Name, DOB, and family role are saved.", "VitaMR Core"),
        Quest("core.medications", "Add current medication list", VitaMasteryStage.Foundation, "all", 2, "Medication list or confirmed none.", "VitaMR Core"),
        Quest("core.allergies", "Add allergy/adverse reaction list", VitaMasteryStage.Foundation, "all", 2, "Allergy list or confirmed no known allergies.", "VitaMR Core"),
        Quest("core.vaccines", "Import vaccine record", VitaMasteryStage.Foundation, "all", 3, "Vaccine table, card, or portal export.", "CDC Immunization Schedules", "https://www.cdc.gov/vaccines/hcp/imz-schedules/index.html"),
        Quest("core.recent_visit", "Add recent primary care or well visit note", VitaMasteryStage.Foundation, "all", 3, "Recent PCP, pediatrician, or well visit note.", "VitaMR Core"),
        Quest("core.vitals", "Add recent vitals", VitaMasteryStage.Foundation, "all", 1, "BP/HR/height/weight/BMI where age-appropriate.", "USPSTF Hypertension", "https://www.uspreventiveservicestaskforce.org/uspstf/recommendation/hypertension-in-adults-screening"),
        Quest("core.family_history", "Add family history snapshot", VitaMasteryStage.Foundation, "all", 1, "First-degree family history or confirmed none.", "VitaMR Core"),

        Quest("child.well_child", "Collect well-child visit history", VitaMasteryStage.Foundation, "child", 3, "Well-child notes or visit date list.", "AAP Bright Futures", "https://www.aap.org/periodicityschedule", ageMax: 18),
        Quest("child.growth", "Add growth chart", VitaMasteryStage.Foundation, "child", 2, "Height/weight/BMI/head circumference history.", "AAP Bright Futures", "https://www.aap.org/periodicityschedule", ageMax: 18),
        Quest("child.developmental", "Add developmental screening record", VitaMasteryStage.PreventiveMap, "child", 1, "Developmental screen records if age-relevant.", "AAP Bright Futures", "https://www.aap.org/periodicityschedule", ageMax: 5),
        Quest("child.autism", "Add autism screening record", VitaMasteryStage.PreventiveMap, "child", 1, "Autism screen at toddler age if available.", "AAP Bright Futures", "https://www.aap.org/periodicityschedule", ageMin: 1, ageMax: 5),
        Quest("child.vision", "Add vision screening record", VitaMasteryStage.PreventiveMap, "child", 1, "Vision screen or eye exam.", "AAP Bright Futures", "https://www.aap.org/periodicityschedule", ageMin: 3, ageMax: 18),
        Quest("child.hearing", "Add hearing screening record", VitaMasteryStage.PreventiveMap, "child", 1, "Hearing screen or audiology record.", "AAP Bright Futures", "https://www.aap.org/periodicityschedule", ageMax: 18),
        Quest("child.dental", "Add dental/oral health record", VitaMasteryStage.PreventiveMap, "child", 1, "Dental visit or oral health record.", "AAP Bright Futures", "https://www.aap.org/periodicityschedule", ageMin: 1, ageMax: 18),

        Quest("adult.depression", "Add depression screening record", VitaMasteryStage.PreventiveMap, "adult", 1, "PHQ/behavioral health screen or annual note.", "USPSTF Depression", "https://www.uspreventiveservicestaskforce.org/uspstf/recommendation/screening-depression-suicide-risk-adults", ageMin: 19),
        Quest("adult.anxiety", "Add anxiety screening record", VitaMasteryStage.PreventiveMap, "adult", 1, "GAD/behavioral health screen or annual note.", "USPSTF Anxiety", "https://www.uspreventiveservicestaskforce.org/uspstf/recommendation/anxiety-adults-screening", ageMin: 19, ageMax: 64),
        Quest("adult.hiv", "Add HIV screening record", VitaMasteryStage.PreventiveMap, "adult", 1, "HIV result or clinician documentation.", "USPSTF HIV", "https://www.uspreventiveservicestaskforce.org/uspstf/recommendation/human-immunodeficiency-virus-hiv-infection-screening", ageMin: 15, ageMax: 65),
        Quest("adult.hcv", "Add hepatitis C screening record", VitaMasteryStage.PreventiveMap, "adult", 1, "HCV antibody/result documentation.", "USPSTF Hepatitis C", "https://www.uspreventiveservicestaskforce.org/uspstf/recommendation/hepatitis-c-screening", ageMin: 18, ageMax: 79),
        Quest("adult.colorectal", "Add colorectal cancer screening record", VitaMasteryStage.PreventiveMap, "adult", 3, "Colonoscopy/FIT/stool DNA/CT colonography result.", "USPSTF Colorectal Cancer", "https://www.uspreventiveservicestaskforce.org/uspstf/recommendation/colorectal-cancer-screening", ageMin: 45, ageMax: 85),
        Quest("adult.diabetes", "Add diabetes/prediabetes screening data", VitaMasteryStage.PreventiveMap, "adult", 2, "A1c, fasting glucose, or OGTT.", "USPSTF Prediabetes and Type 2 Diabetes", "https://www.uspreventiveservicestaskforce.org/uspstf/recommendation/screening-for-prediabetes-and-type-2-diabetes", ageMin: 35, ageMax: 70),
        Quest("adult.cvd_risk", "Add lipid/CVD risk data", VitaMasteryStage.PreventiveMap, "adult", 2, "Lipid panel plus BP/smoking/diabetes data.", "USPSTF Statin/CVD Risk", "https://www.uspreventiveservicestaskforce.org/uspstf/document/RecommendationStatementFinal/statin-use-in-adults-preventive-medication", ageMin: 40, ageMax: 75),

        Quest("female.cervical", "Add cervical cancer screening record", VitaMasteryStage.PreventiveMap, "female", 3, "Pap/HPV result and date.", "USPSTF Cervical Cancer", "https://www.uspreventiveservicestaskforce.org/uspstf/document/ClinicalSummaryFinal/cervical-cancer-screening", ageMin: 21, ageMax: 65, sexOrAnatomyRule: "female"),
        Quest("female.breast", "Add mammogram report", VitaMasteryStage.PreventiveMap, "female", 3, "Mammogram report/date.", "USPSTF Breast Cancer", "https://www.uspreventiveservicestaskforce.org/uspstf/recommendation/breast-cancer-screening", ageMin: 40, ageMax: 74, sexOrAnatomyRule: "female"),
        Quest("female.osteoporosis", "Add bone density record", VitaMasteryStage.PreventiveMap, "female", 2, "DXA/bone density report.", "USPSTF Osteoporosis", "https://www.uspreventiveservicestaskforce.org/uspstf/recommendation/osteoporosis-screening", ageMin: 65, sexOrAnatomyRule: "female"),

        Quest("male.prostate_decision", "Document prostate screening decision", VitaMasteryStage.PreventiveMap, "male", 1, "PSA result or shared-decision note.", "USPSTF Prostate Cancer", "https://www.uspreventiveservicestaskforce.org/uspstf/recommendation/prostate-cancer-screening%20", ageMin: 55, ageMax: 69, sexOrAnatomyRule: "male"),
        Quest("male.aaa", "Add AAA screening decision/report", VitaMasteryStage.DeepSignal, "male", 2, "One-time ultrasound report or documented discussion.", "USPSTF Abdominal Aortic Aneurysm", "https://www.uspreventiveservicestaskforce.org/uspstf/recommendation/abdominal-aortic-aneurysm-screening", ageMin: 65, ageMax: 75, sexOrAnatomyRule: "male"),

        Quest("older.med_review", "Add medication review", VitaMasteryStage.Foundation, "older", 2, "Updated med list reviewed by clinician/pharmacist.", "VitaMR Older Adult Core", ageMin: 65),
        Quest("older.advance_directive", "Add advance directive or health care proxy", VitaMasteryStage.Foundation, "older", 2, "Directive/proxy/POLST/MOLST if applicable.", "VitaMR Older Adult Core", ageMin: 65),
        Quest("older.cancer_status", "Review cancer screening status", VitaMasteryStage.PreventiveMap, "older", 1, "Documented continue/stop status for age-relevant screens.", "USPSTF Age-Specific Screening", ageMin: 65)
    ];

    public VitaMasteryResult Evaluate(string vaultRoot, ChartContext chartContext, PatientIdentityRecord? identity)
    {
        var profile = BuildProfile(identity);
        var chartRoot = Path.Combine(vaultRoot, chartContext.ChartFolderName);
        var wikiRoot = Path.Combine(chartRoot, "wiki");
        var evidence = LoadEvidence(chartRoot, wikiRoot, identity);
        var applicable = Catalog
            .Where(quest => Applies(quest, profile))
            .ToList();
        var results = applicable
            .Select(quest => EvaluateQuest(quest, evidence, profile))
            .ToList();

        var possiblePoints = results.Sum(result => result.Quest.Points);
        var earnedPoints = results.Sum(result => result.EarnedPoints);
        var percent = possiblePoints == 0
            ? 0
            : (int)Math.Round(earnedPoints * 100d / possiblePoints, MidpointRounding.AwayFromZero);
        var stage = percent >= 70
            ? VitaMasteryStage.DeepSignal
            : percent >= 35
                ? VitaMasteryStage.PreventiveMap
                : VitaMasteryStage.Foundation;
        var activeMicroQuests = results
            .Where(result => result.Status == VitaMasteryQuestStatus.Missing || result.Status == VitaMasteryQuestStatus.NeedsReview)
            .OrderBy(result => result.Quest.Stage)
            .ThenByDescending(result => result.Quest.Points)
            .ThenBy(result => result.Quest.Title, StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToList();

        var result = new VitaMasteryResult
        {
            ChartId = chartContext.ChartId,
            PatientDisplayName = chartContext.LocalDisplayName,
            ApplicableQuestCount = results.Count,
            CompletedQuestCount = results.Count(item => item.Status == VitaMasteryQuestStatus.Complete),
            EarnedPoints = earnedPoints,
            PossiblePoints = possiblePoints,
            PercentComplete = Math.Clamp(percent, 0, 100),
            CurrentStage = stage,
            QuestResults = results,
            ActiveMicroQuests = activeMicroQuests,
            Messages = BuildMessages(profile, results, activeMicroQuests)
        };

        WriteVitaMasteryFile(wikiRoot, result);
        return result;
    }

    private static VitaMasteryQuest Quest(
        string id,
        string title,
        VitaMasteryStage stage,
        string populationRule,
        int points,
        string evidenceHint,
        string sourceName,
        string sourceUrl = "",
        int? ageMin = null,
        int? ageMax = null,
        string sexOrAnatomyRule = "any")
    {
        return new VitaMasteryQuest
        {
            QuestId = id,
            Title = title,
            Stage = stage,
            PopulationRule = populationRule,
            Points = points,
            EvidenceHint = evidenceHint,
            SourceName = sourceName,
            SourceUrl = sourceUrl,
            AgeMin = ageMin,
            AgeMax = ageMax,
            SexOrAnatomyRule = sexOrAnatomyRule,
            SourceCheckedDate = SourceCheckedDate,
            UserMotivationText = $"Add evidence for: {title}."
        };
    }

    private static VitaMasteryQuestResult EvaluateQuest(VitaMasteryQuest quest, EvidenceSnapshot evidence, ProfileSnapshot profile)
    {
        var (status, summary) = quest.QuestId switch
        {
            "core.identity" => Evidence(profile.HasIdentityBasics, "Identity basics are saved in the sealed registry."),
            "core.medications" => Evidence(evidence.HasMedicationData, "Medication data found in sterile wiki."),
            "core.allergies" => Evidence(evidence.HasAllergyData, "Allergy/adverse reaction data found in sterile wiki."),
            "core.vaccines" => Evidence(evidence.HasVaccineData, "Vaccine rows found in Vaccines.md."),
            "core.recent_visit" => Evidence(evidence.HasVisitData, "Visit/encounter data found."),
            "core.vitals" => Evidence(evidence.HasVitalsData, "Vitals or BP data found."),
            "core.family_history" => Evidence(evidence.HasFamilyHistoryData, "Family history data found."),
            "child.well_child" => Evidence(evidence.HasWellChildData || evidence.HasVisitData, "Well-child or visit history found."),
            "child.growth" => Evidence(evidence.HasGrowthData, "Growth chart or height/weight trend found."),
            "child.developmental" => Evidence(evidence.HasDevelopmentalData, "Developmental screening evidence found."),
            "child.autism" => Evidence(evidence.HasAutismData, "Autism screening evidence found."),
            "child.vision" => Evidence(evidence.HasVisionData, "Vision screening evidence found."),
            "child.hearing" => Evidence(evidence.HasHearingData, "Hearing screening evidence found."),
            "child.dental" => Evidence(evidence.HasDentalData, "Dental/oral health evidence found."),
            "adult.depression" => Evidence(evidence.HasDepressionData, "Depression screening evidence found."),
            "adult.anxiety" => Evidence(evidence.HasAnxietyData, "Anxiety screening evidence found."),
            "adult.hiv" => Evidence(evidence.HasHivData, "HIV screening evidence found."),
            "adult.hcv" => Evidence(evidence.HasHepatitisCData, "Hepatitis C screening evidence found."),
            "adult.colorectal" => Evidence(evidence.HasColorectalData, "Colorectal screening evidence found."),
            "adult.diabetes" => Evidence(evidence.HasDiabetesData, "Diabetes/prediabetes screening evidence found."),
            "adult.cvd_risk" => Evidence(evidence.HasLipidData && evidence.HasVitalsData, "Lipid and BP/vitals data found."),
            "female.cervical" => Evidence(evidence.HasCervicalData, "Cervical screening evidence found."),
            "female.breast" => Evidence(evidence.HasBreastData, "Mammogram/breast screening evidence found."),
            "female.osteoporosis" => Evidence(evidence.HasBoneDensityData, "Bone density evidence found."),
            "male.prostate_decision" => Evidence(evidence.HasProstateData, "PSA/prostate decision evidence found."),
            "male.aaa" => Evidence(evidence.HasAaaData, "AAA ultrasound/discussion evidence found."),
            "older.med_review" => Evidence(evidence.HasMedicationData && evidence.HasVisitData, "Medication list and visit context found."),
            "older.advance_directive" => Evidence(evidence.HasAdvanceDirectiveData, "Advance directive/proxy evidence found."),
            "older.cancer_status" => Evidence(evidence.HasCancerScreeningStatusData, "Cancer screening status evidence found."),
            _ => (VitaMasteryQuestStatus.Missing, quest.EvidenceHint)
        };

        if (status == VitaMasteryQuestStatus.Missing && evidence.HasPendingPhoneEvidence)
        {
            status = VitaMasteryQuestStatus.NeedsReview;
            summary = "Phone evidence is waiting for desktop review.";
        }

        return new VitaMasteryQuestResult
        {
            Quest = quest,
            Status = status,
            EvidenceSummary = summary
        };
    }

    private static (VitaMasteryQuestStatus Status, string Summary) Evidence(bool hasEvidence, string summary)
    {
        return hasEvidence
            ? (VitaMasteryQuestStatus.Complete, summary)
            : (VitaMasteryQuestStatus.Missing, string.Empty);
    }

    private static bool Applies(VitaMasteryQuest quest, ProfileSnapshot profile)
    {
        if (quest.AgeMin.HasValue && (!profile.AgeYears.HasValue || profile.AgeYears.Value < quest.AgeMin.Value))
        {
            return false;
        }

        if (quest.AgeMax.HasValue && (!profile.AgeYears.HasValue || profile.AgeYears.Value > quest.AgeMax.Value))
        {
            return false;
        }

        if (quest.SexOrAnatomyRule.Equals("female", StringComparison.OrdinalIgnoreCase) &&
            !profile.SexOrAnatomy.Equals("female", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (quest.SexOrAnatomyRule.Equals("male", StringComparison.OrdinalIgnoreCase) &&
            !profile.SexOrAnatomy.Equals("male", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return quest.PopulationRule switch
        {
            "all" => true,
            "child" => profile.AgeYears is <= 18,
            "adult" => profile.AgeYears is null or >= 19,
            "female" => profile.SexOrAnatomy.Equals("female", StringComparison.OrdinalIgnoreCase),
            "male" => profile.SexOrAnatomy.Equals("male", StringComparison.OrdinalIgnoreCase),
            "older" => profile.AgeYears is >= 65,
            _ => true
        };
    }

    private static ProfileSnapshot BuildProfile(PatientIdentityRecord? identity)
    {
        var age = TryCalculateAge(identity?.DateOfBirth);
        var text = $"{identity?.PatientDisplayName} {identity?.RelationshipNotes}".Trim();
        var sexOrAnatomy = InferSexOrAnatomy(text);
        return new ProfileSnapshot(
            AgeYears: age,
            SexOrAnatomy: sexOrAnatomy,
            HasIdentityBasics: identity is not null &&
                               !string.IsNullOrWhiteSpace(identity.PatientDisplayName) &&
                               (!string.IsNullOrWhiteSpace(identity.DateOfBirth) ||
                                !string.IsNullOrWhiteSpace(identity.RelationshipNotes)));
    }

    private static int? TryCalculateAge(string? dateOfBirth)
    {
        if (string.IsNullOrWhiteSpace(dateOfBirth))
        {
            return null;
        }

        if (!DateTime.TryParse(dateOfBirth, out var parsed))
        {
            return null;
        }

        var today = DateTime.Today;
        var age = today.Year - parsed.Year;
        if (parsed.Date > today.AddYears(-age))
        {
            age--;
        }

        return age < 0 || age > 125 ? null : age;
    }

    private static string InferSexOrAnatomy(string text)
    {
        if (Regex.IsMatch(text, @"\b(female|woman|girl|mother|mom|wife|daughter|sister|she|her)\b", RegexOptions.IgnoreCase))
        {
            return "female";
        }

        if (Regex.IsMatch(text, @"\b(male|man|boy|father|dad|husband|son|brother|he|him)\b", RegexOptions.IgnoreCase))
        {
            return "male";
        }

        return "unknown";
    }

    private static EvidenceSnapshot LoadEvidence(string chartRoot, string wikiRoot, PatientIdentityRecord? identity)
    {
        var files = Directory.Exists(wikiRoot)
            ? Directory.EnumerateFiles(wikiRoot, "*.md", SearchOption.AllDirectories)
                .Where(IsEvidenceSourceFile)
                .ToList()
            : [];
        var combined = new StringBuilder();
        foreach (var file in files)
        {
            try
            {
                combined.AppendLine(Path.GetFileName(file));
                combined.AppendLine(File.ReadAllText(file));
            }
            catch
            {
            }
        }

        var text = combined.ToString();
        return new EvidenceSnapshot
        {
            HasMedicationData = HasTableData(Path.Combine(wikiRoot, "Index.md"), "## Medications") ||
                                HasAny(text, @"\b(no current medications|no active medications|confirmed no medications)\b"),
            HasAllergyData = HasAny(text, @"\b(allerg(?:y|ies|ic)|adverse reaction|anaphylaxis|nkda|no known allergies)\b"),
            HasVaccineData = HasTableData(Path.Combine(wikiRoot, "Vaccines.md"), null) ||
                             HasAny(text, @"\b(immunization record imported|vaccine record imported)\b"),
            HasVisitData = Directory.Exists(Path.Combine(wikiRoot, "encounters")) &&
                           Directory.EnumerateFiles(Path.Combine(wikiRoot, "encounters"), "*.md").Any() ||
                           HasTableData(Path.Combine(wikiRoot, "Index.md"), "## Visit Notes"),
            HasVitalsData = HasAny(text, @"\b(blood pressure|bp\b|vitals?|height|weight|bmi|heart rate|pulse)\b|\b\d{2,3}/\d{2,3}\b"),
            HasFamilyHistoryData = HasAny(text, @"\b(family history|mother had|father had|sibling|first-degree|maternal|paternal)\b"),
            HasWellChildData = HasAny(text, @"\b(well child|well-child|pediatrician|growth chart|developmental)\b"),
            HasGrowthData = HasAny(text, @"\b(growth chart|height|weight|bmi|head circumference|percentile)\b"),
            HasDevelopmentalData = HasAny(text, @"\b(developmental screen|developmental screening|milestone|asq\b)\b"),
            HasAutismData = HasAny(text, @"\b(autism|m-chat|mchat|asd screen)\b"),
            HasVisionData = HasAny(text, @"\b(vision screen|visual acuity|eye exam|ophthalmology|optometry)\b"),
            HasHearingData = HasAny(text, @"\b(hearing screen|audiology|audiogram|newborn hearing)\b"),
            HasDentalData = HasAny(text, @"\b(dental|dentist|oral health|fluoride)\b"),
            HasDepressionData = HasAny(text, @"\b(depression screen|phq-?2|phq-?9|mood screen)\b"),
            HasAnxietyData = HasAny(text, @"\b(anxiety screen|gad-?2|gad-?7)\b"),
            HasHivData = HasAny(text, @"\b(hiv\b|human immunodeficiency)\b"),
            HasHepatitisCData = HasAny(text, @"\b(hepatitis c|hcv)\b"),
            HasColorectalData = HasAny(text, @"\b(colonoscopy|fit test|cologuard|stool dna|ct colonography|colorectal|colon cancer)\b"),
            HasDiabetesData = HasAny(text, @"\b(a1c|hba1c|fasting glucose|ogtt|prediabetes|diabetes)\b"),
            HasLipidData = HasAny(text, @"\b(lipid panel|cholesterol|ldl|hdl|triglyceride|ascvd)\b"),
            HasCervicalData = HasAny(text, @"\b(pap smear|pap test|hpv test|cervical cancer|cervix|colposcopy)\b"),
            HasBreastData = HasAny(text, @"\b(mammogram|mammography|breast screening|breast imaging)\b"),
            HasBoneDensityData = HasAny(text, @"\b(dxa|dexa|bone density|osteoporosis)\b"),
            HasProstateData = HasAny(text, @"\b(psa\b|prostate)\b"),
            HasAaaData = HasAny(text, @"\b(abdominal aortic aneurysm|aaa\b|aortic ultrasound)\b"),
            HasAdvanceDirectiveData = HasAny(text, @"\b(advance directive|health care proxy|healthcare proxy|polst|molst|living will)\b"),
            HasCancerScreeningStatusData = HasAny(text, @"\b(cancer screening|screening status|mammogram|colonoscopy|pap|psa|ldct)\b"),
            HasPendingPhoneEvidence = HasAny(text, @"\b(phone inbox|mobile capture|android sent|mobile_offline)\b") ||
                                      HasAny(identity?.RelationshipNotes ?? string.Empty, @"\b(phone inbox|mobile capture)\b")
        };
    }

    private static bool IsEvidenceSourceFile(string path)
    {
        var fileName = Path.GetFileName(path);
        if (fileName.Equals("Vita_Mastery.md", StringComparison.OrdinalIgnoreCase) ||
            fileName.StartsWith("Dolly_", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("Future_Data_Needed.md", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private static bool HasAny(string text, string pattern)
    {
        return !string.IsNullOrWhiteSpace(text) &&
               Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static bool HasTableData(string path, string? sectionHeading)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        var content = File.ReadAllText(path);
        if (!string.IsNullOrWhiteSpace(sectionHeading))
        {
            var match = Regex.Match(
                content,
                $@"(?ms)^{Regex.Escape(sectionHeading)}\s*(?<body>.*?)(?=^##\s+|\z)");
            if (!match.Success)
            {
                return false;
            }

            content = match.Groups["body"].Value;
        }

        return content
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(line => line.StartsWith('|') &&
                         !line.Contains("---", StringComparison.Ordinal) &&
                         line.Count(character => character == '|') >= 3 &&
                         !Regex.IsMatch(line, @"^\|\s*(Date|Medication|Condition|Item|Summary|Dose)", RegexOptions.IgnoreCase));
    }

    private static IReadOnlyList<string> BuildMessages(
        ProfileSnapshot profile,
        IReadOnlyList<VitaMasteryQuestResult> results,
        IReadOnlyList<VitaMasteryQuestResult> activeMicroQuests)
    {
        var messages = new List<string>();
        if (profile.AgeYears is null)
        {
            messages.Add("DOB is missing or unreadable, so age-specific quests are conservative.");
        }

        if (profile.SexOrAnatomy.Equals("unknown", StringComparison.OrdinalIgnoreCase))
        {
            messages.Add("Sex/anatomy profile is unknown, so sex-specific screening quests are not applied yet.");
        }

        if (activeMicroQuests.Count == 0 && results.Count > 0)
        {
            messages.Add("No active micro-quests from the starter catalog right now.");
        }

        return messages;
    }

    private static void WriteVitaMasteryFile(string wikiRoot, VitaMasteryResult result)
    {
        try
        {
            Directory.CreateDirectory(wikiRoot);
            var path = Path.Combine(wikiRoot, "Vita_Mastery.md");
            var builder = new StringBuilder();
            builder.AppendLine("---");
            builder.AppendLine($"chart_id: \"{result.ChartId}\"");
            builder.AppendLine("document_type: \"Vita_Mastery\"");
            builder.AppendLine($"generated_at: \"{DateTimeOffset.Now:O}\"");
            builder.AppendLine("---");
            builder.AppendLine();
            builder.AppendLine("# Vita Mastery");
            builder.AppendLine();
            builder.AppendLine($"> {result.SummaryLine}");
            builder.AppendLine();
            builder.AppendLine($"- Earned points: {result.EarnedPoints}");
            builder.AppendLine($"- Possible points: {result.PossiblePoints}");
            builder.AppendLine($"- Completed quests: {result.CompletedQuestCount}/{result.ApplicableQuestCount}");
            builder.AppendLine();
            builder.AppendLine("## Active Micro-Quests");
            builder.AppendLine();
            foreach (var quest in result.ActiveMicroQuests)
            {
                builder.AppendLine($"- [{quest.Status}] {quest.Quest.Title} - {quest.Quest.EvidenceHint}");
            }

            builder.AppendLine();
            builder.AppendLine("## Quest Results");
            builder.AppendLine();
            builder.AppendLine("| Quest | Stage | Status | Points | Evidence |");
            builder.AppendLine("|---|---|---|---:|---|");
            foreach (var quest in result.QuestResults)
            {
                builder.AppendLine($"| {Escape(quest.Quest.Title)} | {VitaMasteryResult.FormatStage(quest.Quest.Stage)} | {quest.Status} | {quest.EarnedPoints}/{quest.Quest.Points} | {Escape(quest.EvidenceSummary)} |");
            }

            File.WriteAllText(path, builder.ToString());
        }
        catch
        {
            // Vita Mastery should never block core chart use.
        }
    }

    private static string Escape(string value)
    {
        return value.Replace("|", "\\|", StringComparison.Ordinal).Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
    }

    private sealed record ProfileSnapshot(int? AgeYears, string SexOrAnatomy, bool HasIdentityBasics);

    private sealed class EvidenceSnapshot
    {
        public bool HasMedicationData { get; init; }
        public bool HasAllergyData { get; init; }
        public bool HasVaccineData { get; init; }
        public bool HasVisitData { get; init; }
        public bool HasVitalsData { get; init; }
        public bool HasFamilyHistoryData { get; init; }
        public bool HasWellChildData { get; init; }
        public bool HasGrowthData { get; init; }
        public bool HasDevelopmentalData { get; init; }
        public bool HasAutismData { get; init; }
        public bool HasVisionData { get; init; }
        public bool HasHearingData { get; init; }
        public bool HasDentalData { get; init; }
        public bool HasDepressionData { get; init; }
        public bool HasAnxietyData { get; init; }
        public bool HasHivData { get; init; }
        public bool HasHepatitisCData { get; init; }
        public bool HasColorectalData { get; init; }
        public bool HasDiabetesData { get; init; }
        public bool HasLipidData { get; init; }
        public bool HasCervicalData { get; init; }
        public bool HasBreastData { get; init; }
        public bool HasBoneDensityData { get; init; }
        public bool HasProstateData { get; init; }
        public bool HasAaaData { get; init; }
        public bool HasAdvanceDirectiveData { get; init; }
        public bool HasCancerScreeningStatusData { get; init; }
        public bool HasPendingPhoneEvidence { get; init; }
    }
}
