using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using VitaMR.Commands;
using VitaMR.Models;
using VitaMR.Services;

namespace VitaMR.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    private const string DesktopGemmaEndpoint = "http://localhost:11434";
    private const string PreferredGemmaModelName = "gemma4:e4b";
    private const int MaxTaskCompletionAttempts = 5;
    private const int DataHunterQuestionsPerSession = 3;
    private const string PhoneInboxWaitingStatus = "Waiting for manager review";
    private const string DataHunterAnswerStatusAnswered = "answered";
    private const string DataHunterAnswerStatusDeferred = "deferred";
    private const string DataHunterAnswerStatusNotSure = "not_sure";
    private const string DataHunterAnswerStatusNotApplicable = "not_applicable";
    private const string DataHunterAnswerStatusRecordSuggested = "record_suggested_pending_confirmation";
    private const string DataHunterAnswerStatusRecordConfirmed = "record_confirmed_by_user";
    private const int PersonalVaultWeaveThreshold = 12;
    private const string PersonalVaultTaggedCaptureFileName = "tagged-captures.jsonl";
    private const string PersonalVaultBatchFileName = "wiki-batches.jsonl";
    private const string PersonalVaultMemorySummaryFileName = "Personal_Memory_Summary.md";
    private const string YouTubeProjectSlug = "LT_Transformation_Early_Detection";
    private const string YouTubeCurrentVideoFileName = "Current_Video.json";
    private static readonly string[] DataHunterBasicQuestions =
    [
        "What are your main long-term health goals? For example: live longer and stay functional, feel good day to day, prevent future problems, control a known disease, not sure yet, or something else.",
        "Do you have any health problems or diagnoses you already know about?",
        "If yes, what are they?",
        "Which health problem or concern feels most important to keep organized right now?",
        "Are you having any new or frequent symptoms you want me to keep track of?",
        "Has anything changed recently with your energy, pain, breathing, weight, appetite, mood, memory, sleep, or daily function?",
        "Are you taking any prescription medications?",
        "If yes, what are they?",
        "Are you taking any vitamins, supplements, inhalers, injections, or over-the-counter treatments?",
        "Do you ever miss doses, stop meds because of side effects, or take anything only as needed?",
        "Do you have any allergies or bad reactions to medicines, foods, latex, contrast dye, or anything else?",
        "Have you ever had surgery?",
        "If yes, what surgeries have you had?",
        "Have you ever had a medical procedure that was not surgery?",
        "If yes, what procedures have you had?",
        "Have you ever been hospitalized overnight?",
        "If yes, where and what was it for?",
        "Have you ever been to the ER or urgent care for something important?",
        "If yes, what happened?",
        "Do any major health problems run in your family?",
        "If yes, who had them? Parent, sibling, grandparent, child, or other relative?",
        "What condition did they have?",
        "About how old were they when it started, if you know?",
        "Did anyone in your family have early heart disease, stroke, cancer, diabetes, dementia, autoimmune disease, blood clots, kidney disease, or mental health conditions?",
        "Is there anything about your family history you are unsure about but want Dolly to remember to ask again later?",
        "Do you smoke, vape, or use nicotine now, or did you in the past?",
        "If yes, about how much and for how many years?",
        "Do you drink alcohol?",
        "If yes, about how much and how often?",
        "Do you use cannabis, recreational substances, or non-prescribed medications that you want Dolly to remember privately for context?",
        "How often do you exercise or move on purpose?",
        "If you exercise or move on purpose, what type of exercise do you do?",
        "How many hours of sleep do you usually get?",
        "Does that sleep feel like enough for you?",
        "Do you know your usual blood pressure?",
        "Do you know your current height and weight?",
        "Has your weight changed recently without trying?",
        "Do you have a primary care doctor?",
        "If yes, what is their name?",
        "Who helps you make medical decisions, if anyone?",
        "Do you have an emergency contact or health care proxy you want Dolly to remember?",
        "Do you think your primary care doctor has all or most of your medical records?",
        "Do you have medical records at home, such as papers, folders, binders, discs, photos, or PDFs?",
        "Do you use any patient portals?",
        "If yes, which portals or health systems?",
        "Have you had care at any hospitals in the past?",
        "If yes, which hospitals?",
        "Do you see any specialists?",
        "If yes, what kind of specialists and where?",
        "Do you use a regular pharmacy?",
        "If yes, which pharmacy?",
        "Have you had labs done through a lab company, clinic, hospital, or portal?",
        "If yes, where do those lab records live?",
        "Have you had imaging, such as X-ray, CT, MRI, ultrasound, mammogram, or imaging CDs?",
        "If yes, where were those done?",
        "Do you have vaccine records or a vaccine card somewhere?",
        "Which records would be most useful to gather first?",
        "Is there anything you already know is missing from your record?"
    ];
    private static readonly string[] DataHunterBasicQuestionKeys =
    [
        "long_term_health_goals",
        "has_known_health_problems",
        "known_health_problems",
        "top_known_problem",
        "new_or_frequent_symptoms",
        "recent_health_changes",
        "has_prescription_medications",
        "prescription_medications",
        "supplements_and_nonprescription_treatments",
        "medication_adherence_or_side_effects",
        "allergies_and_reactions",
        "has_surgery_history",
        "surgery_history",
        "has_procedure_history",
        "procedure_history",
        "has_hospital_history",
        "hospital_history",
        "has_er_or_urgent_care_history",
        "er_or_urgent_care_history",
        "has_family_history",
        "family_history_relatives",
        "family_history_conditions",
        "family_history_onset_age",
        "high_signal_family_history",
        "family_history_unsure",
        "nicotine_status",
        "nicotine_amount_duration",
        "alcohol_status",
        "alcohol_amount_frequency",
        "substance_context",
        "exercise_frequency",
        "exercise_type",
        "sleep_hours",
        "sleep_enough",
        "usual_blood_pressure",
        "current_height_weight",
        "unintentional_weight_change",
        "has_primary_care_doctor",
        "primary_care_doctor_name",
        "medical_decision_support",
        "emergency_contact_or_proxy",
        "pcp_has_most_records",
        "home_records",
        "has_patient_portals",
        "patient_portals",
        "has_past_hospital_care",
        "past_hospitals",
        "has_specialists",
        "specialists",
        "has_regular_pharmacy",
        "regular_pharmacy",
        "has_lab_records",
        "lab_record_locations",
        "has_imaging_records",
        "imaging_record_locations",
        "vaccine_record_location",
        "priority_records",
        "known_missing_records"
    ];
    private const string DataHunterPersonalDataMapJsonFileName = "Data_Hunter_Personal_Data_Map.json";
    private const string DataHunterPersonalDataMapMarkdownFileName = "Data_Hunter_Personal_Data_Map.md";
    private const string DataHunterMasterQuestStatusPendingEvidence = "Pending accepted evidence";
    private const int DataHunterCategoryCompleteXp = 5;
    private const string SetupStepChartManagerName = "chart_manager_name";
    private const string SetupStepManagerPassword = "manager_password";
    private const string SetupStepManagerProfile = "manager_profile";
    private const string SetupStepAgentName = "agent_name";
    private const string SetupStepAgentTone = "agent_tone";
    private const string SetupStepPatientNames = "patient_names";
    private const string SetupStepAdditionalUsers = "additional_users";
    private const string SetupStepPersonaInterview = "persona_interview";
    private const string SetupStepComplete = "complete";

    private readonly IFilePickerService _filePickerService;
    private readonly IVaultIngestService _vaultIngestService;
    private readonly IFastScrubberService _fastScrubberService;
    private readonly IEncounterDraftService _encounterDraftService;
    private readonly ILocalModelReviewService _localModelReviewService;
    private readonly ISettingsService _settingsService;
    private readonly IIngestAuditService _ingestAuditService;
    private readonly IPatientRegistryService _patientRegistryService;
    private readonly ISecondPassScrubberService _secondPassScrubberService;
    private readonly IImageDocumentReviewService _imageDocumentReviewService;
    private readonly IOmniboxPreflightService _omniboxPreflightService;
    private readonly IVaultContextService _vaultContextService;
    private readonly IDollyVaultChatService _dollyVaultChatService;
    private readonly IConversationArchiveService _conversationArchiveService;
    private readonly ILocalModelWarmupService _localModelWarmupService;
    private readonly IGeminiApiKeyStore _geminiApiKeyStore;
    private readonly IProviderApiKeyStore _providerApiKeyStore;
    private readonly IGeminiOmniboxAnswerService _geminiOmniboxAnswerService;
    private readonly IGeminiSmallTalkService _geminiSmallTalkService;
    private readonly IResponsePersonalizationService _responsePersonalizationService;
    private readonly IGeminiWikiPatchService _geminiWikiPatchService;
    private readonly IGeminiWikiRewriteService _geminiWikiRewriteService;
    private readonly ILocalWikiPatchService _localWikiPatchService;
    private readonly IWikiPatchApplyService _wikiPatchApplyService;
    private readonly IDreamRunnerAuditService _dreamRunnerAuditService;
    private readonly ISpecialtyWeaverService _specialtyWeaverService;
    private readonly ISymptomWatcherService _symptomWatcherService;
    private readonly IPatternLinkerService _patternLinkerService;
    private readonly IHealthGoalEngineService _healthGoalEngineService;
    private readonly IHealthPreferenceService _healthPreferenceService;
    private readonly IDrugInteractionScannerService _drugInteractionScannerService;
    private readonly IPreVisitBriefService _preVisitBriefService;
    private readonly IOriginalSourceRetrievalService _originalSourceRetrievalService;
    private readonly ISessionOpenAwarenessService _sessionOpenAwarenessService;
    private readonly IDollyWorkingSummaryService _dollyWorkingSummaryService;
    private readonly IDollyTaskBoardService _dollyTaskBoardService;
    private readonly IDollyAgentStateService _dollyAgentStateService;
    private readonly IChronosLedgerService _chronosLedgerService;
    private readonly IPatientIntakeExtractionService _patientIntakeExtractionService;
    private readonly IGemmaActionPacketService _gemmaActionPacketService;
    private readonly IGeminiBackendPipelineService _geminiBackendPipelineService;
    private readonly IVitaMasteryService _vitaMasteryService;
    private readonly DispatcherTimer _dollyHeartbeatTimer;
    private readonly List<IngestResult> _lastIngestResults = [];
    private readonly List<ScrubResult> _lastScrubResults = [];
    private readonly List<SecondPassScrubResult> _lastSecondPassScrubResults = [];
    private readonly List<ImageDocumentReviewResult> _lastImageDocumentReviewResults = [];
    private readonly List<IngestAuditEntry> _lastAuditEntries = [];
    private readonly List<EncounterNodeWriteResult> _lastEncounterNodeWriteResults = [];
    private readonly Queue<string> _recentActionPacketAuditLines = [];
    private readonly Dictionary<string, ChartContext> _pendingMobileChartSwitches = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _phoneInboxActionPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "VitaMR",
        "Mobile_Phone_Inbox_Actions.jsonl");
    private GeminiBackendPipelineResult? _lastGeminiBackendPipelineResult;
    private DreamRunnerAuditResult? _lastDreamRunnerAuditResult;
    private SpecialtyWeaverResult? _lastSpecialtyWeaverResult;
    private SymptomWatcherResult? _lastSymptomWatcherResult;
    private PatternLinkerResult? _lastPatternLinkerResult;
    private HealthGoalResult? _lastHealthGoalResult;
    private HealthPreferenceResult? _lastHealthPreferenceResult;
    private DrugInteractionScanResult? _lastDrugInteractionScanResult;
    private PreVisitBriefResult? _lastPreVisitBriefResult;
    private SessionOpenAwarenessResult? _lastSessionOpenAwarenessResult;
    private DollyWorkingSummaryResult? _lastDollyWorkingSummaryResult;
    private DollyTaskBoardResult? _lastDollyTaskBoardResult;
    private VitaMasteryResult? _lastVitaMasteryResult;
    private EncounterDraft? _lastEncounterDraft;
    private DraftValidationResult? _lastDraftValidation;
    private LocalModelReviewResult? _lastLocalModelReview;
    private PendingTypedCapture? _pendingTypedCapture;
    private PendingPatientIntake? _pendingPatientIntake;
    private PendingPatientDelete? _pendingPatientDelete;
    private PendingCareGapResolution? _pendingCareGapResolution;
    private PendingVaccineUpdate? _pendingVaccineUpdate;
    private PendingReminderUpdate? _pendingReminderUpdate;
    private PendingPhotoUpdate? _pendingPhotoUpdate;
    private bool _pendingSetupRepairConfirmation;
    private string _providerApiKeyInput = string.Empty;
    private string _personalVaultSearchText = string.Empty;
    private string _selectedPersonalVaultTopic = "All Topics";
    private string _personalVaultStatus = "Personal Vault not loaded yet";
    private AppSettings _settings;
    private string _promptText = string.Empty;
    private bool _isListening;
    private bool _isPreviewPanelOpen;
    private bool _isAdminDashboardOpen;
    private bool _isVitaMasteryOpen;
    private bool _isEvaluationToolOpen;
    private string _selectedEvaluationScenario = "Live Use Check";
    private string _evaluationNotes = "Notes, surprise, or weakness found while using VitaMR...";
    private string _evaluationSavedStatus = "Not saved yet";
    private bool _isThinking;
    private bool _isDragTargetActive;
    private bool _backgroundSummaryQueued;
    private bool _backgroundSummaryRunning;
    private ChartContext? _backgroundSummaryChart;
    private string _backgroundSummaryReason = string.Empty;
    private string _lastGemmaWorkingSummaryContext = string.Empty;
    private string _lastDollyHeartbeatSignature = string.Empty;
    private string _pendingDataHunterBasicChartId = string.Empty;
    private string _pendingDataHunterExpansionChartId = string.Empty;
    private string _pendingDataHunterExpansionKey = string.Empty;
    private string _pendingHealthspanMotivationStep = string.Empty;
    private readonly Dictionary<string, string> _pendingHealthspanMotivationAnswers = new(StringComparer.OrdinalIgnoreCase);
    private int _dataHunterQuestionsAnsweredThisSession;
    private bool _dataHunterContinuousInterviewEnabled;
    private bool _dataHunterPausedForSession;
    private string _vaultWriteStatus = "Vault idle";
    private string _localModelReadinessStatus = "Local helper idle";
    private string _apiLlmStatus = "API ready";
    private string _hostLlmStatus = "Local helper idle";
    private bool _isApiLlmWorking;
    private bool _isHostLlmWorking;

    public MainWindowViewModel(
        IFilePickerService filePickerService,
        IVaultIngestService vaultIngestService,
        IFastScrubberService fastScrubberService,
        IEncounterDraftService encounterDraftService,
        ILocalModelReviewService localModelReviewService,
        ISettingsService settingsService,
        IIngestAuditService ingestAuditService,
        IPatientRegistryService patientRegistryService,
        ISecondPassScrubberService secondPassScrubberService,
        IImageDocumentReviewService imageDocumentReviewService,
        IOmniboxPreflightService omniboxPreflightService,
        IVaultContextService vaultContextService,
        IDollyVaultChatService dollyVaultChatService,
        IConversationArchiveService conversationArchiveService,
        ILocalModelWarmupService localModelWarmupService,
        IGeminiApiKeyStore geminiApiKeyStore,
        IProviderApiKeyStore providerApiKeyStore,
        IGeminiOmniboxAnswerService geminiOmniboxAnswerService,
        IGeminiSmallTalkService geminiSmallTalkService,
        IResponsePersonalizationService responsePersonalizationService,
        IGeminiWikiPatchService geminiWikiPatchService,
        IGeminiWikiRewriteService geminiWikiRewriteService,
        ILocalWikiPatchService localWikiPatchService,
        IWikiPatchApplyService wikiPatchApplyService,
        IDreamRunnerAuditService dreamRunnerAuditService,
        ISpecialtyWeaverService specialtyWeaverService,
        ISymptomWatcherService symptomWatcherService,
        IPatternLinkerService patternLinkerService,
        IHealthGoalEngineService healthGoalEngineService,
        IHealthPreferenceService healthPreferenceService,
        IDrugInteractionScannerService drugInteractionScannerService,
        IPreVisitBriefService preVisitBriefService,
        IOriginalSourceRetrievalService originalSourceRetrievalService,
        ISessionOpenAwarenessService sessionOpenAwarenessService,
        IDollyWorkingSummaryService dollyWorkingSummaryService,
        IDollyTaskBoardService dollyTaskBoardService,
        IDollyAgentStateService dollyAgentStateService,
        IChronosLedgerService chronosLedgerService,
        IPatientIntakeExtractionService patientIntakeExtractionService,
        IGemmaActionPacketService gemmaActionPacketService,
        IGeminiBackendPipelineService geminiBackendPipelineService,
        IVitaMasteryService vitaMasteryService)
    {
        _filePickerService = filePickerService;
        _vaultIngestService = vaultIngestService;
        _fastScrubberService = fastScrubberService;
        _encounterDraftService = encounterDraftService;
        _localModelReviewService = localModelReviewService;
        _settingsService = settingsService;
        _ingestAuditService = ingestAuditService;
        _patientRegistryService = patientRegistryService;
        _secondPassScrubberService = secondPassScrubberService;
        _imageDocumentReviewService = imageDocumentReviewService;
        _omniboxPreflightService = omniboxPreflightService;
        _vaultContextService = vaultContextService;
        _dollyVaultChatService = dollyVaultChatService;
        _conversationArchiveService = conversationArchiveService;
        _localModelWarmupService = localModelWarmupService;
        _geminiApiKeyStore = geminiApiKeyStore;
        _providerApiKeyStore = providerApiKeyStore;
        _geminiOmniboxAnswerService = geminiOmniboxAnswerService;
        _geminiSmallTalkService = geminiSmallTalkService;
        _responsePersonalizationService = responsePersonalizationService;
        _geminiWikiPatchService = geminiWikiPatchService;
        _geminiWikiRewriteService = geminiWikiRewriteService;
        _localWikiPatchService = localWikiPatchService;
        _wikiPatchApplyService = wikiPatchApplyService;
        _dreamRunnerAuditService = dreamRunnerAuditService;
        _specialtyWeaverService = specialtyWeaverService;
        _symptomWatcherService = symptomWatcherService;
        _patternLinkerService = patternLinkerService;
        _healthGoalEngineService = healthGoalEngineService;
        _healthPreferenceService = healthPreferenceService;
        _drugInteractionScannerService = drugInteractionScannerService;
        _preVisitBriefService = preVisitBriefService;
        _originalSourceRetrievalService = originalSourceRetrievalService;
        _sessionOpenAwarenessService = sessionOpenAwarenessService;
        _dollyWorkingSummaryService = dollyWorkingSummaryService;
        _dollyTaskBoardService = dollyTaskBoardService;
        _dollyAgentStateService = dollyAgentStateService;
        _chronosLedgerService = chronosLedgerService;
        _patientIntakeExtractionService = patientIntakeExtractionService;
        _gemmaActionPacketService = gemmaActionPacketService;
        _geminiBackendPipelineService = geminiBackendPipelineService;
        _vitaMasteryService = vitaMasteryService;
        _dollyHeartbeatTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(15)
        };
        _dollyHeartbeatTimer.Tick += HandleDollyHeartbeat;
        _settings = _settingsService.Load();
        _settings.ActiveChartId = NormalizeChartId(_settings.ActiveChartId);
        _settings.ActivePatientDisplayName = _patientRegistryService
            .GetOrCreateIdentity(_settings.ActiveChartId, _settings.ActivePatientDisplayName)
            .PatientDisplayName;
        _vaultIngestService.VaultRoot = _settings.VaultRootPath;
        _chronosLedgerService.EnsureLedger(_settings.VaultRootPath);
        _healthPreferenceService.EnsureUserPreferenceScaffold(_settings.VaultRootPath, _settings.VaultOwnerName);
        _healthPreferenceService.EnsureAdultUserPreferenceScaffolds(
            _settings.VaultRootPath,
            _patientRegistryService.GetAllIdentities(),
            DateTime.Now);
        RefreshVitaMastery();

        Models =
        [
            "Dolly Main Agent",
            "Fast response",
            "Deep review",
            "Local fallback"
        ];

        ProviderOptions =
        [
            "Gemini",
            "OpenAI",
            "Anthropic",
            "xAI",
            "Local"
        ];

        OmniboxActions =
        [
            "Ask / Update Existing Patient",
            "Add New Patient",
            "Delete Patient File"
        ];

        SourceTypes =
        [
            "Unknown",
            "Patient_Provided",
            "EMR",
            "Hospital_Portal",
            "Lab_Direct",
            "Imaging_Center",
            "Fax_Scan",
            "Manual_Entry"
        ];

        HealthspanCoachingModes =
        [
            "Record Assistant Mode",
            "Healthspan Support Mode",
            "Longevity Performance Mode",
            "Elite / Experimental Longevity Mode"
        ];

        EvaluationScenarios =
        [
            "Live Use Check",
            "Setup Repair",
            "Admin Dashboard",
            "Chart Switching",
            "Document Ingest",
            "Vaccine Records",
            "Reminders",
            "Privacy Gate"
        ];

        AttachFilesCommand = new RelayCommand(AttachFiles);
        RemoveAttachmentCommand = new RelayCommand(RemoveAttachment);
        OpenAttachmentCommand = new RelayCommand(OpenAttachment);
        SendMessageCommand = new RelayCommand(SendMessage, CanSendMessage);
        ToggleVoiceCommand = new RelayCommand(ToggleVoice);
        TogglePreviewPanelCommand = new RelayCommand(TogglePreviewPanel);
        ClosePreviewPanelCommand = new RelayCommand(ClosePreviewPanel);
        StartDataHunterCommand = new RelayCommand(StartDataHunterBasicInterview);
        UseRecordsModeCommand = new RelayCommand(() => ApplyHealthspanMode("Record Assistant Mode", false));
        UseHealthspanModeCommand = new RelayCommand(EnableHealthspanModeWithChoicePrompt);
        UseHealthspanSupportCommand = new RelayCommand(() => ApplyHealthspanMode("Healthspan Support Mode", true));
        UseLongevityPerformanceCommand = new RelayCommand(() => ApplyHealthspanMode("Longevity Performance Mode", true));
        UseExperimentalLongevityCommand = new RelayCommand(() => ApplyHealthspanMode("Elite / Experimental Longevity Mode", true));
        UseMedicalLifeModeCommand = new RelayCommand(() => ApplyLifeMode("Medical"));
        UsePersonalLifeModeCommand = new RelayCommand(() => ApplyLifeMode("Personal"));
        UseLockboxLifeModeCommand = new RelayCommand(() => ApplyLifeMode("Lockbox"));
        UsePetsLifeModeCommand = new RelayCommand(() => ApplyLifeMode("Pets"));
        ShowConversationCommand = new RelayCommand(ShowConversation);
        ShowAdminDashboardCommand = new RelayCommand(ShowAdminDashboard);
        ShowVitaMasteryCommand = new RelayCommand(ShowVitaMastery);
        RefreshAdminDashboardCommand = new RelayCommand(RefreshAdminDashboard);
        ToggleEvaluationToolCommand = new RelayCommand(ToggleEvaluationTool);
        SaveEvaluationResultCommand = new RelayCommand(SaveEvaluationResult);
        AdminRunSetupCommand = new RelayCommand(RunSetupFromAdmin);
        AdminRepairSetupCommand = new RelayCommand(RepairSetupFromAdmin);
        AdminReviewCleanupCommand = new RelayCommand(ReviewCleanupFromAdmin);
        AdminFocusChartCommand = new RelayCommand(FocusChartFromAdmin);
        SaveSettingsCommand = new RelayCommand(SaveSettings);
        SaveProviderApiKeyCommand = new RelayCommand(SaveProviderApiKey, CanSaveProviderApiKey);
        ClearProviderApiKeyCommand = new RelayCommand(ClearProviderApiKey);
        ChooseVaultFolderCommand = new RelayCommand(ChooseVaultFolder);
        RefreshPhoneInboxCommand = new RelayCommand(RefreshPhoneInbox);
        RefreshPersonalVaultCommand = new RelayCommand(RefreshPersonalVault);
        WeavePersonalVaultCommand = new RelayCommand(WeavePersonalVault);
        PhoneInboxActionCommand = new RelayCommand(HandlePhoneInboxAction);

        RefreshPhoneInbox();
        RefreshPersonalVault();
        RefreshAdminDashboard();
        BuildEvaluationQuestions();
        StartFreshConversation();
        _dollyHeartbeatTimer.Start();
        _ = WarmLocalModelAsync();
    }

    public ObservableCollection<string> Models { get; }

    public ObservableCollection<string> ProviderOptions { get; }

    public ObservableCollection<string> OmniboxActions { get; }

    public ObservableCollection<string> SourceTypes { get; }

    public ObservableCollection<string> HealthspanCoachingModes { get; }

    public ObservableCollection<string> EvaluationScenarios { get; }

    public ObservableCollection<ChatMessageViewModel> Messages { get; } = [];

    public ObservableCollection<AttachmentViewModel> Attachments { get; } = [];

    public ObservableCollection<AdminChartRowViewModel> AdminChartRows { get; } = [];

    public ObservableCollection<string> AdminProtectedActions { get; } = [];

    public ObservableCollection<EvaluationQuestionViewModel> EvaluationQuestions { get; } = [];

    public ObservableCollection<PhoneInboxItemViewModel> PhoneInboxItems { get; } = [];

    public ObservableCollection<PersonalVaultItemViewModel> PersonalVaultItems { get; } = [];

    public ObservableCollection<string> PersonalVaultTopicFilters { get; } = ["All Topics"];

    public ObservableCollection<VitaMasteryQuestRowViewModel> VitaMasteryActiveQuestRows { get; } = [];

    public ObservableCollection<VitaMasteryQuestRowViewModel> VitaMasteryFoundationQuestRows { get; } = [];

    public ObservableCollection<VitaMasteryQuestRowViewModel> VitaMasteryPreventiveQuestRows { get; } = [];

    public ObservableCollection<VitaMasteryQuestRowViewModel> VitaMasteryDeepSignalQuestRows { get; } = [];

    public ObservableCollection<DataHunterDebugRowViewModel> DataHunterDebugRows { get; } = [];

    public RelayCommand AttachFilesCommand { get; }

    public RelayCommand RemoveAttachmentCommand { get; }

    public RelayCommand OpenAttachmentCommand { get; }

    public RelayCommand SendMessageCommand { get; }

    public RelayCommand ToggleVoiceCommand { get; }

    public RelayCommand TogglePreviewPanelCommand { get; }

    public RelayCommand ClosePreviewPanelCommand { get; }

    public RelayCommand StartDataHunterCommand { get; }

    public RelayCommand UseRecordsModeCommand { get; }

    public RelayCommand UseHealthspanModeCommand { get; }

    public RelayCommand UseHealthspanSupportCommand { get; }

    public RelayCommand UseLongevityPerformanceCommand { get; }

    public RelayCommand UseExperimentalLongevityCommand { get; }

    public RelayCommand UseMedicalLifeModeCommand { get; }

    public RelayCommand UsePersonalLifeModeCommand { get; }

    public RelayCommand UseLockboxLifeModeCommand { get; }

    public RelayCommand UsePetsLifeModeCommand { get; }

    public RelayCommand ShowConversationCommand { get; }

    public RelayCommand ShowAdminDashboardCommand { get; }

    public RelayCommand ShowVitaMasteryCommand { get; }

    public RelayCommand RefreshAdminDashboardCommand { get; }

    public RelayCommand ToggleEvaluationToolCommand { get; }

    public RelayCommand SaveEvaluationResultCommand { get; }

    public RelayCommand AdminRunSetupCommand { get; }

    public RelayCommand AdminRepairSetupCommand { get; }

    public RelayCommand AdminReviewCleanupCommand { get; }

    public RelayCommand AdminFocusChartCommand { get; }

    public RelayCommand SaveSettingsCommand { get; }

    public RelayCommand SaveProviderApiKeyCommand { get; }

    public RelayCommand ClearProviderApiKeyCommand { get; }

    public RelayCommand ChooseVaultFolderCommand { get; }

    public RelayCommand RefreshPhoneInboxCommand { get; }

    public RelayCommand RefreshPersonalVaultCommand { get; }

    public RelayCommand WeavePersonalVaultCommand { get; }

    public RelayCommand PhoneInboxActionCommand { get; }

    public string PersonalVaultSearchText
    {
        get => _personalVaultSearchText;
        set
        {
            if (SetProperty(ref _personalVaultSearchText, value ?? string.Empty))
            {
                RefreshPersonalVault();
            }
        }
    }

    public string PersonalVaultStatus
    {
        get => _personalVaultStatus;
        private set => SetProperty(ref _personalVaultStatus, value);
    }

    public string SelectedPersonalVaultTopic
    {
        get => _selectedPersonalVaultTopic;
        set
        {
            var normalized = string.IsNullOrWhiteSpace(value) ? "All Topics" : value.Trim();
            if (SetProperty(ref _selectedPersonalVaultTopic, normalized))
            {
                RefreshPersonalVault();
            }
        }
    }

    public string ActivePersona => "Active Persona: Dolly";

    public Visibility ConversationVisibility => _isAdminDashboardOpen || _isVitaMasteryOpen
        ? Visibility.Collapsed
        : Visibility.Visible;

    public Visibility AdminDashboardVisibility => _isAdminDashboardOpen
        ? Visibility.Visible
        : Visibility.Collapsed;

    public Visibility VitaMasteryVisibility => _isVitaMasteryOpen
        ? Visibility.Visible
        : Visibility.Collapsed;

    public string AdminManagerDisplayName => FirstNonEmpty(_settings.ChartManagerName, "Not set");

    public string AdminManagerNickname => string.IsNullOrWhiteSpace(_settings.ChartManagerNickname)
        ? "No manager nickname saved."
        : $"Nickname: {_settings.ChartManagerNickname.Trim()}";

    public string AdminPersonaPath => GetChartManagerPersonaRelativePath();

    public string AdminActiveUserDisplay =>
        $"{FirstNonEmpty(_settings.ActiveUserName, _settings.ChartManagerNickname, _settings.ChartManagerName, "Not set")} ({FirstNonEmpty(_settings.ActiveUserRole, "Chart Manager")})";

    public string AdminAgentDisplay => $"{AgentDisplayName} / {FirstNonEmpty(_settings.AgentTone, "Friendly")}";

    public string AdminSetupStatus => _settings.IsInitialSetupComplete
        ? "Setup complete"
        : $"Setup in progress: {FirstNonEmpty(_settings.InitialSetupStep, SetupStepChartManagerName)}";

    public string AdminActiveChartDisplay => $"{ActivePatientDisplayName} ({ActiveChartId})";

    public string AdminAccessNotes => string.IsNullOrWhiteSpace(_settings.SetupAdditionalUsers)
        ? "Access notes: only the Chart Manager is recorded right now."
        : $"Access notes: {_settings.SetupAdditionalUsers.Trim()}";

    public bool IsEvaluationToolOpen
    {
        get => _isEvaluationToolOpen;
        set => SetProperty(ref _isEvaluationToolOpen, value);
    }

    public string SelectedEvaluationScenario
    {
        get => _selectedEvaluationScenario;
        set
        {
            var normalized = string.IsNullOrWhiteSpace(value) ? "Live Use Check" : value.Trim();
            if (!SetProperty(ref _selectedEvaluationScenario, normalized))
            {
                return;
            }

            BuildEvaluationQuestions();
            EvaluationSavedStatus = "Not saved yet";
        }
    }

    public string EvaluationNotes
    {
        get => _evaluationNotes;
        set => SetProperty(ref _evaluationNotes, value);
    }

    public string EvaluationSavedStatus
    {
        get => _evaluationSavedStatus;
        set => SetProperty(ref _evaluationSavedStatus, value);
    }

    public string ActiveChartFocus => $"Working on: {ExtractFirstNameForDisplay(ActivePatientDisplayName)}";

    public string ActiveChartFocusDetail => $"{ActivePatientDisplayName} • {ActiveChartId}";

    public string VitaMasterySummary => _lastVitaMasteryResult?.SummaryLine ?? "Vita Mastery: not scored yet";

    public int VitaMasteryPercent => _lastVitaMasteryResult?.PercentComplete ?? 0;

    public string VitaMasteryMicroQuestSummary =>
        _lastVitaMasteryResult?.ActiveMicroQuests.Count > 0
            ? string.Join(" | ", _lastVitaMasteryResult.ActiveMicroQuests.Select(result => result.Quest.Title))
            : "No active Vita Mastery micro-quests yet.";

    public string DataHunterTitle => "Data Hunter";

    public string DataHunterProgressSummary => $"Data Hunter: {DataHunterBasicPercent}% | Basic";

    public string DataHunterStageSummary => $"Basic {DataHunterBasicPercent}% - Talk with Dolly";

    public int DataHunterBasicPercent => CalculateDataHunterBasicPercent();

    public string DataHunterPointsSummary
    {
        get
        {
            var map = LoadDataHunterPersonalDataMap(GetActiveChart());
            return BuildDataHunterCategoryProgressSummary(map);
        }
    }

    public string DataHunterNextQuestion => GetDataHunterNextQuestion(GetActiveChart());

    public string DataHunterQuestCardMeta
    {
        get
        {
            var map = LoadDataHunterPersonalDataMap(GetActiveChart());
            var nextIndex = GetNextDataHunterBasicQuestionIndex(map);
            if (!nextIndex.HasValue)
            {
                return "Master - gather accepted records";
            }

            var key = GetDataHunterBasicQuestionKey(nextIndex.Value);
            return $"{GetDataHunterQuestionCategory(key)} - +{GetDataHunterQuestionXp(key)} XP";
        }
    }

    public string DataHunterFirstMasterTargetSummary
    {
        get
        {
            var quest = GetFirstDataHunterMasterQuest(LoadDataHunterPersonalDataMap(GetActiveChart()));
            return quest is null
                ? "Master target: answer Basic record-location questions to queue one."
                : $"Master target: {quest.Title}";
        }
    }

    public string DataHunterFirstMasterTargetDetail
    {
        get
        {
            var quest = GetFirstDataHunterMasterQuest(LoadDataHunterPersonalDataMap(GetActiveChart()));
            return quest is null
                ? "Evidence XP stays locked until a matching record is accepted into the vault."
                : $"{BuildDataHunterQuestClueLine(quest)} | {quest.Status} | Evidence: {FirstNonEmpty(quest.AcceptedEvidenceSummary, quest.EvidenceHint)}";
        }
    }

    public string DataHunterMasterEvidenceSummary
    {
        get
        {
            var quests = GenerateDataHunterMasterQuests(LoadDataHunterPersonalDataMap(GetActiveChart()));
            if (quests.Count == 0)
            {
                return "Master Hunt: no evidence targets yet";
            }

            var accepted = quests.Count(IsDataHunterMasterQuestAccepted);
            var pending = quests.Count - accepted;
            return $"Master Hunt: {accepted} accepted evidence / {pending} pending";
        }
    }

    public string DataHunterDebugSummary
    {
        get
        {
            var map = LoadDataHunterPersonalDataMap(GetActiveChart());
            var nextIndex = GetNextDataHunterBasicQuestionIndex(map);
            var nextKey = nextIndex.HasValue ? GetDataHunterBasicQuestionKey(nextIndex.Value) : "Basic complete";
            return $"Question key: {nextKey} | XP: {CalculateDataHunterBasicXp(map)} | Master targets: {GenerateDataHunterMasterQuests(map).Count}";
        }
    }

    public string VitaMasteryPointsSummary => _lastVitaMasteryResult is null
        ? "0 / 0 points"
        : $"{_lastVitaMasteryResult.EarnedPoints} / {_lastVitaMasteryResult.PossiblePoints} points";

    public string VitaMasteryStageDisplay => _lastVitaMasteryResult is null
        ? "Basic"
        : VitaMasteryResult.FormatStage(_lastVitaMasteryResult.CurrentStage);

    public string VitaMasteryCompletionSummary => _lastVitaMasteryResult is null
        ? "No quests scored yet."
        : $"{_lastVitaMasteryResult.CompletedQuestCount} of {_lastVitaMasteryResult.ApplicableQuestCount} applicable quests complete.";

    public int VitaMasteryFoundationStepValue => VitaMasteryPercent >= 1 ? 100 : 0;

    public int VitaMasteryPreventiveStepValue => VitaMasteryPercent >= 35 ? 100 : Math.Clamp(VitaMasteryPercent * 100 / 35, 0, 100);

    public int VitaMasteryDeepSignalStepValue => VitaMasteryPercent >= 70 ? Math.Clamp((VitaMasteryPercent - 70) * 100 / 30, 0, 100) : 0;

    public string ActiveChartPath
    {
        get
        {
            var chartPath = Path.Combine(VaultRootPath, ActiveChartId, $"_Chart_{ActiveChartId}.md");
            return File.Exists(chartPath)
                ? chartPath
                : Path.Combine(VaultRootPath, ActiveChartId);
        }
    }

    public string ActivePatientPhotoPath
    {
        get
        {
            var identity = _patientRegistryService.GetAllIdentities()
                .FirstOrDefault(patient => patient.ChartId.Equals(ActiveChartId, StringComparison.OrdinalIgnoreCase));

            return identity is not null &&
                   !string.IsNullOrWhiteSpace(identity.PhotoPath) &&
                   File.Exists(identity.PhotoPath)
                ? identity.PhotoPath
                : string.Empty;
        }
    }

    public ImageSource? ActivePatientPhotoImage
    {
        get
        {
            var path = ActivePatientPhotoPath;
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            try
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.UriSource = new Uri(path, UriKind.Absolute);
                image.EndInit();
                image.Freeze();
                return image;
            }
            catch
            {
                return null;
            }
        }
    }

    public string ActivePatientInitials => BuildInitials(ActivePatientDisplayName);

    public Visibility PatientPhotoVisibility => ActivePatientPhotoImage is null
        ? Visibility.Collapsed
        : Visibility.Visible;

    public Visibility PatientInitialsVisibility => ActivePatientPhotoImage is null
        ? Visibility.Visible
        : Visibility.Collapsed;

    public string ChartManagerName
    {
        get => _settings.ChartManagerName;
        set
        {
            var normalized = string.IsNullOrWhiteSpace(value) ? "Vault Manager" : value.Trim();
            if (_settings.ChartManagerName == normalized)
            {
                return;
            }

            _settings.ChartManagerName = normalized;
            OnPropertyChanged();
            RefreshPreview();
        }
    }

    public string ChartManagerPassword
    {
        get => _settings.ChartManagerPassword;
        set
        {
            var normalized = string.IsNullOrWhiteSpace(value) ? "Vault Manager" : value.Trim();
            if (_settings.ChartManagerPassword == normalized)
            {
                return;
            }

            _settings.ChartManagerPassword = normalized;
            OnPropertyChanged();
            RefreshPreview();
        }
    }

    public string ClinicalAxis => "Axis 1-4: Full Clinical / Synthesized";

    public string RiskStatus => "Risk Status: Low";

    public string RiskSummary => "Risk: Low";

    public bool IsInitialSetupActive => !_settings.IsInitialSetupComplete;

    public string PromptPlaceholder => SelectedOmniboxAction switch
    {
        _ when IsInitialSetupActive => BuildSetupPlaceholder(),
        "Add New Patient" => "Add name, DOB, address, and phone; optional email, relation, SSN, photo...",
        "Delete Patient File" => "Type the patient name to move their file to the 7-day wastebasket...",
        _ => "Ask Dolly about a patient, assign a task, or drop a file..."
    };

    public string VaultRootPath
    {
        get => _settings.VaultRootPath;
        set
        {
            if (_settings.VaultRootPath == value)
            {
                return;
            }

            _settings.VaultRootPath = value;
        _vaultIngestService.VaultRoot = value;
        _chronosLedgerService.EnsureLedger(value);
            OnPropertyChanged();
            RefreshPreview();
        }
    }

    public string ActiveChartId
    {
        get => _settings.ActiveChartId;
        set
        {
            var normalized = NormalizeChartId(value);

            if (_settings.ActiveChartId == normalized)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(_settings.ActiveChartId))
            {
                _settings.LastActiveChartId = _settings.ActiveChartId;
            }

            _settings.ActiveChartId = normalized;
            _settings.ActivePatientDisplayName = _patientRegistryService.ResolveDisplayName(normalized);
            OnPropertyChanged();
            OnPropertyChanged(nameof(ActivePatientDisplayName));
            OnPropertyChanged(nameof(ActiveChartFocus));
            OnPropertyChanged(nameof(ActiveChartFocusDetail));
            OnPropertyChanged(nameof(ActiveChartPath));
            OnActivePatientVisualChanged();
            _settingsService.Save(_settings);
            RefreshVitaMastery();
            RefreshPreview();
        }
    }

    public string ActivePatientDisplayName
    {
        get => _settings.ActivePatientDisplayName;
        set
        {
            if (_settings.ActivePatientDisplayName == value)
            {
                return;
            }

            _settings.ActivePatientDisplayName = string.IsNullOrWhiteSpace(value)
                ? ActiveChartId
                : value.Trim();
            _patientRegistryService.SaveIdentityRecord(new PatientIdentityRecord
            {
                ChartId = ActiveChartId,
                PatientDisplayName = _settings.ActivePatientDisplayName
            });
            OnPropertyChanged();
            OnPropertyChanged(nameof(ActiveChartFocus));
            OnPropertyChanged(nameof(ActiveChartFocusDetail));
            OnPropertyChanged(nameof(ActiveChartPath));
            OnActivePatientVisualChanged();
            RefreshVitaMastery();
            RefreshPreview();
        }
    }

    public bool IsSyntheticTestMode
    {
        get => _settings.IsSyntheticTestMode;
        set
        {
            if (_settings.IsSyntheticTestMode == value)
            {
                return;
            }

            _settings.IsSyntheticTestMode = value;
            OnPropertyChanged();
            RefreshPreview();
        }
    }

    public string SourceSystem
    {
        get => _settings.DefaultSourceSystem;
        set
        {
            var normalized = NormalizeProvenanceField(value);

            if (_settings.DefaultSourceSystem == normalized)
            {
                return;
            }

            _settings.DefaultSourceSystem = normalized;
            OnPropertyChanged();
            RefreshPreview();
        }
    }

    public string SourceFacility
    {
        get => _settings.DefaultSourceFacility;
        set
        {
            var normalized = NormalizeProvenanceField(value);

            if (_settings.DefaultSourceFacility == normalized)
            {
                return;
            }

            _settings.DefaultSourceFacility = normalized;
            OnPropertyChanged();
            RefreshPreview();
        }
    }

    public string SelectedSourceType
    {
        get => _settings.DefaultSourceType;
        set
        {
            var normalized = NormalizeProvenanceField(value);

            if (_settings.DefaultSourceType == normalized)
            {
                return;
            }

            _settings.DefaultSourceType = normalized;
            OnPropertyChanged();
            RefreshPreview();
        }
    }

    public bool EnableHealthspanMode
    {
        get => _settings.EnableHealthspanMode;
        set
        {
            if (_settings.EnableHealthspanMode == value)
            {
                return;
            }

            _settings.EnableHealthspanMode = value;
            if (!value)
            {
                _settings.HealthspanCoachingMode = "Record Assistant Mode";
                OnPropertyChanged(nameof(HealthspanCoachingMode));
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(IsRecordsModeSelected));
            OnPropertyChanged(nameof(IsHealthspanModeSelected));
            OnPropertyChanged(nameof(IsHealthspanSupportSelected));
            OnPropertyChanged(nameof(IsLongevityPerformanceSelected));
            OnPropertyChanged(nameof(IsExperimentalLongevitySelected));
            OnPropertyChanged(nameof(HealthspanIntensityVisibility));
            OnPropertyChanged(nameof(HealthspanModeSummary));
            OnPropertyChanged(nameof(HealthspanModeDetail));
            OnPropertyChanged(nameof(HealthspanStarBrush));
            RefreshPreview();
        }
    }

    public string HealthspanCoachingMode
    {
        get => string.IsNullOrWhiteSpace(_settings.HealthspanCoachingMode)
            ? "Record Assistant Mode"
            : _settings.HealthspanCoachingMode;
        set
        {
            var normalized = string.IsNullOrWhiteSpace(value)
                ? "Record Assistant Mode"
                : value.Trim();

            if (_settings.HealthspanCoachingMode == normalized)
            {
                return;
            }

            _settings.HealthspanCoachingMode = normalized;
            _settings.EnableHealthspanMode = !normalized.Equals("Record Assistant Mode", StringComparison.OrdinalIgnoreCase);
            OnPropertyChanged();
            OnPropertyChanged(nameof(EnableHealthspanMode));
            OnPropertyChanged(nameof(IsRecordsModeSelected));
            OnPropertyChanged(nameof(IsHealthspanModeSelected));
            OnPropertyChanged(nameof(IsHealthspanSupportSelected));
            OnPropertyChanged(nameof(IsLongevityPerformanceSelected));
            OnPropertyChanged(nameof(IsExperimentalLongevitySelected));
            OnPropertyChanged(nameof(HealthspanIntensityVisibility));
            OnPropertyChanged(nameof(HealthspanModeSummary));
            OnPropertyChanged(nameof(HealthspanModeDetail));
            OnPropertyChanged(nameof(HealthspanStarBrush));
            RefreshPreview();
        }
    }

    public bool IsRecordsModeSelected => !EnableHealthspanMode ||
        HealthspanCoachingMode.Equals("Record Assistant Mode", StringComparison.OrdinalIgnoreCase);

    public bool IsHealthspanModeSelected => !IsRecordsModeSelected;

    public bool IsHealthspanSupportSelected => HealthspanCoachingMode.Equals("Healthspan Support Mode", StringComparison.OrdinalIgnoreCase);

    public bool IsLongevityPerformanceSelected => HealthspanCoachingMode.Equals("Longevity Performance Mode", StringComparison.OrdinalIgnoreCase);

    public bool IsExperimentalLongevitySelected => HealthspanCoachingMode.Equals("Elite / Experimental Longevity Mode", StringComparison.OrdinalIgnoreCase);

    public string ActiveLifeMode
    {
        get => NormalizeLifeMode(_settings.ActiveLifeMode);
        set => ApplyLifeMode(value);
    }

    public bool IsMedicalLifeModeSelected => ActiveLifeMode.Equals("Medical", StringComparison.OrdinalIgnoreCase);

    public bool IsPersonalLifeModeSelected => ActiveLifeMode.Equals("Personal", StringComparison.OrdinalIgnoreCase);

    public bool IsLockboxLifeModeSelected => ActiveLifeMode.Equals("Lockbox", StringComparison.OrdinalIgnoreCase);

    public bool IsPetsLifeModeSelected => ActiveLifeMode.Equals("Pets", StringComparison.OrdinalIgnoreCase);

    public string ActiveLifeModeDetail => ActiveLifeMode switch
    {
        "Personal" => "Personal notes stay out of medical chart evidence.",
        "Lockbox" => "Lockbox notes are sensitive, local/trusted-host only, and not sent to cloud AI.",
        "Pets" => "Pet notes stay animal/pet context, not a human chart.",
        _ => "Medical Mode is the controlled chart/evidence lane."
    };

    public Visibility HealthspanIntensityVisibility => IsHealthspanModeSelected
        ? Visibility.Visible
        : Visibility.Collapsed;

    public string HealthspanModeSummary => EnableHealthspanMode
        ? $"Healthspan: {HealthspanCoachingMode}"
        : "Healthspan: off | Record Assistant Mode";

    public string HealthspanModeDetail => HealthspanCoachingMode switch
    {
        "Healthspan Support Mode" => "Dolly can use records and goals to guide healthspan optimization.",
        "Longevity Performance Mode" => "Direct accountability for ambitious healthspan goals, opt-in only.",
        "Elite / Experimental Longevity Mode" => "Rigorous tracking and research awareness, separate from medical advice.",
        _ => "Dolly focuses on organizing, searching, summarizing, and protecting records."
    };

    public Brush HealthspanStarBrush => HealthspanCoachingMode switch
    {
        "Longevity Performance Mode" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C0C7D1")),
        "Elite / Experimental Longevity Mode" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D9A441")),
        "Healthspan Support Mode" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2B7FFF")),
        _ => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A7B4C2"))
    };

    public bool EnableLocalModelTestbed
    {
        get => _settings.EnableLocalModelTestbed;
        set
        {
            if (_settings.EnableLocalModelTestbed == value)
            {
                return;
            }

            _settings.EnableLocalModelTestbed = value;
            OnPropertyChanged();
            RefreshPreview();
        }
    }

    public string LocalModelEndpoint
    {
        get => _settings.LocalModelEndpoint;
        set
        {
            var normalized = string.IsNullOrWhiteSpace(value)
                ? "http://localhost:11434"
                : value.Trim();

            if (_settings.LocalModelEndpoint == normalized)
            {
                return;
            }

            _settings.LocalModelEndpoint = normalized;
            OnPropertyChanged();
            RefreshPreview();
        }
    }

    public string LocalModelName
    {
        get => _settings.LocalModelName;
        set
        {
            var normalized = string.IsNullOrWhiteSpace(value)
                ? PreferredGemmaModelName
                : value.Trim();

            if (_settings.LocalModelName == normalized)
            {
                return;
            }

            _settings.LocalModelName = normalized;
            OnPropertyChanged();
            RefreshPreview();
        }
    }

    public bool EnableGeminiBackendProcessing
    {
        get => _settings.EnableGeminiBackendProcessing;
        set
        {
            if (_settings.EnableGeminiBackendProcessing == value)
            {
                return;
            }

            _settings.EnableGeminiBackendProcessing = value;
            OnPropertyChanged();
            RefreshPreview();
        }
    }

    public string GeminiFastModelName
    {
        get => _settings.GeminiFastModelName;
        set
        {
            var normalized = string.IsNullOrWhiteSpace(value)
                ? "gemini-3.1-flash-lite-preview"
                : value.Trim();

            if (_settings.GeminiFastModelName == normalized)
            {
                return;
            }

            _settings.GeminiFastModelName = normalized;
            OnPropertyChanged();
            RefreshPreview();
        }
    }

    public string GeminiThinkingModelName
    {
        get => _settings.GeminiThinkingModelName;
        set
        {
            var normalized = string.IsNullOrWhiteSpace(value)
                ? "gemini-3-flash-preview"
                : value.Trim();

            if (_settings.GeminiThinkingModelName == normalized)
            {
                return;
            }

            _settings.GeminiThinkingModelName = normalized;
            OnPropertyChanged();
            RefreshPreview();
        }
    }

    public string GeminiKeyStatus => _geminiApiKeyStore.HasConfiguredKeys()
        ? "Agent connection ready"
        : "Agent connection needs setup";

    public string SelectedAiProvider
    {
        get => string.IsNullOrWhiteSpace(_settings.SelectedAiProvider) ? "Gemini" : _settings.SelectedAiProvider;
        set
        {
            var normalized = NormalizeProviderDisplayName(value);
            if (_settings.SelectedAiProvider == normalized)
            {
                return;
            }

            _settings.SelectedAiProvider = normalized;
            ProviderApiKeyInput = string.Empty;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ProviderSetupStatus));
            OnPropertyChanged(nameof(ProviderSetupDetail));
            OnPropertyChanged(nameof(ProviderApiKeyInput));
            SaveProviderApiKeyCommand.RaiseCanExecuteChanged();
            RefreshPreview();
        }
    }

    public string ProviderApiKeyInput
    {
        get => _providerApiKeyInput;
        set
        {
            if (_providerApiKeyInput == value)
            {
                return;
            }

            _providerApiKeyInput = value;
            OnPropertyChanged();
            SaveProviderApiKeyCommand.RaiseCanExecuteChanged();
        }
    }

    public string ProviderSetupStatus => _providerApiKeyStore.HasProviderKey(SelectedAiProvider)
        ? $"{SelectedAiProvider} key configured"
        : $"{SelectedAiProvider} key not configured";

    public string ProviderSetupDetail => SelectedAiProvider.Equals("Gemini", StringComparison.OrdinalIgnoreCase)
        ? "Gemini is the active implemented provider in this prototype."
        : $"{SelectedAiProvider} is a planned provider slot. Key storage is ready; request routing still needs implementation.";

    public string AgentDisplayName
    {
        get => string.IsNullOrWhiteSpace(_settings.AgentDisplayName)
            ? "Dolly"
            : _settings.AgentDisplayName.Trim();
        set
        {
            var normalized = string.IsNullOrWhiteSpace(value)
                ? "Dolly"
                : value.Trim();

            if (_settings.AgentDisplayName == normalized)
            {
                return;
            }

            _settings.AgentDisplayName = normalized;
            OnPropertyChanged();
            OnPropertyChanged(nameof(LlmActivityStatus));
            RefreshPreview();
        }
    }

    private string AgentLabel => $"{AgentDisplayName} - {DateTime.Now:h:mm tt}";

    public string LocalScrubberStatus => Attachments.Count > 0
        ? "Local scrubber queued"
        : "Local scrubber idle";

    public string PayloadStatus => Attachments.Count > 0 || !string.IsNullOrWhiteSpace(PromptText)
        ? "Ready to review"
        : "Vault idle";

    public string LlmActivityStatus => IsThinking
        ? $"{AgentDisplayName} is working"
        : PayloadStatus;

    public string ApiLlmStatus
    {
        get => _apiLlmStatus;
        private set => SetProperty(ref _apiLlmStatus, value);
    }

    public string HostLlmStatus
    {
        get => _hostLlmStatus;
        private set => SetProperty(ref _hostLlmStatus, value);
    }

    public bool IsApiLlmWorking
    {
        get => _isApiLlmWorking;
        private set => SetProperty(ref _isApiLlmWorking, value);
    }

    public bool IsHostLlmWorking
    {
        get => _isHostLlmWorking;
        private set => SetProperty(ref _isHostLlmWorking, value);
    }

    public string ConversationArchiveStatus => string.IsNullOrWhiteSpace(_conversationArchiveService.RawLogPath)
        ? $"Conversation archive ready: {_conversationArchiveService.SessionId}"
        : $"Conversation archived: {_conversationArchiveService.SessionId}";

    public string SendButtonText => IsThinking
        ? string.IsNullOrWhiteSpace(PromptText) ? "Working" : "Chat"
        : "Send";

    public bool IsInputEnabled => true;

    public bool IsChatInputEnabled => true;

    public bool IsTaskInputEnabled => !IsThinking;

    public string OmniboxRunState => IsThinking
        ? "Task running - chat stays open"
        : "Ready for task or chat";

    public string VaultWriteStatus
    {
        get => _vaultWriteStatus;
        private set => SetProperty(ref _vaultWriteStatus, value);
    }

    public string LocalModelReadinessStatus
    {
        get => _localModelReadinessStatus;
        private set
        {
            if (SetProperty(ref _localModelReadinessStatus, value))
            {
                RefreshPreview();
            }
        }
    }

    public string PromptText
    {
        get => _promptText;
        set
        {
            if (SetProperty(ref _promptText, value))
            {
                OnPropertyChanged(nameof(IsPromptPlaceholderVisible));
                OnPropertyChanged(nameof(SendButtonText));
                OnStatusChanged();
                RefreshPreview();
                SendMessageCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string SelectedModel
    {
        get => _settings.SelectedModelRoute;
        set
        {
            var normalized = string.IsNullOrWhiteSpace(value)
                ? "Dolly Main Agent"
                : value.Trim();

            if (_settings.SelectedModelRoute == normalized)
            {
                return;
            }

            _settings.SelectedModelRoute = normalized;
            OnPropertyChanged();
            RefreshPreview();
        }
    }

    public string SelectedOmniboxAction
    {
        get => _settings.SelectedOmniboxAction;
        set
        {
            var normalized = string.IsNullOrWhiteSpace(value)
                ? "Ask / Update Existing Patient"
                : value.Trim();

            if (_settings.SelectedOmniboxAction == normalized)
            {
                return;
            }

            _settings.SelectedOmniboxAction = normalized;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PromptPlaceholder));
            SendMessageCommand.RaiseCanExecuteChanged();
            RefreshPreview();
        }
    }

    public bool IsListening
    {
        get => _isListening;
        private set => SetProperty(ref _isListening, value);
    }

    public bool IsPreviewPanelOpen
    {
        get => _isPreviewPanelOpen;
        private set => SetProperty(ref _isPreviewPanelOpen, value);
    }

    public bool IsThinking
    {
        get => _isThinking;
        private set
        {
            if (SetProperty(ref _isThinking, value))
            {
                OnPropertyChanged(nameof(SendButtonText));
                OnPropertyChanged(nameof(IsInputEnabled));
                OnPropertyChanged(nameof(IsChatInputEnabled));
                OnPropertyChanged(nameof(IsTaskInputEnabled));
                OnPropertyChanged(nameof(OmniboxRunState));
                OnPropertyChanged(nameof(LlmActivityStatus));
                SendMessageCommand.RaiseCanExecuteChanged();

                if (!value)
                {
                    _ = TryRunQueuedBackgroundSummaryAsync();
                }
            }
        }
    }

    public bool IsDragTargetActive
    {
        get => _isDragTargetActive;
        set => SetProperty(ref _isDragTargetActive, value);
    }

    public string PreviewPanelTitle => "Privacy Gate";

    public ObservableCollection<string> PreviewLines { get; } = [];

    public bool IsPromptPlaceholderVisible => string.IsNullOrWhiteSpace(PromptText);

    private void StartFreshConversation()
    {
        if (IsInitialSetupActive)
        {
            EnsureSetupFiles();
            Messages.Add(new ChatMessageViewModel(
                MessageAuthor.Dolly,
                AgentLabel,
                BuildSetupPromptLines()));
            RefreshPreview();
            return;
        }

        var activeChart = GetActiveChart();
        _dollyAgentStateService.EnsureAgentScaffold(VaultRootPath);
        _chronosLedgerService.RecordEvent(
            VaultRootPath,
            activeChart,
            "session_opened",
            $"{AgentDisplayName} session opened.",
            AgentDisplayName,
            "session");
        _lastDollyTaskBoardResult = _dollyTaskBoardService.EnsureBoard(VaultRootPath, activeChart);
        _lastSessionOpenAwarenessResult = _sessionOpenAwarenessService.BuildSessionOpenAwareness(VaultRootPath, activeChart);
        Messages.Add(new ChatMessageViewModel(
            MessageAuthor.Dolly,
            AgentLabel,
            BuildSessionOpenMessageLines(_lastSessionOpenAwarenessResult, _lastDollyWorkingSummaryResult, _lastDollyTaskBoardResult)));
        var setupIssues = DetectSetupRepairIssues();
        if (setupIssues.Count > 0)
        {
            Messages.Add(new ChatMessageViewModel(
                MessageAuthor.Dolly,
                AgentLabel,
                BuildSetupRepairOfferLines(setupIssues)));
        }

        QueueBackgroundSummaryRefresh(activeChart, "session open gap");

        RefreshPreview();
    }

    private string StartDollyTask(
        ChartContext chartContext,
        string taskName,
        string currentStep,
        string taskType = "user_task",
        int priority = 1)
    {
        var taskId = _dollyTaskBoardService.StartTask(
            VaultRootPath,
            chartContext,
            taskName,
            currentStep,
            $"{SelectedModel} / {LocalModelName}",
            taskType,
            priority);
        _lastDollyTaskBoardResult = _dollyTaskBoardService.BuildStatus(VaultRootPath, chartContext);
        _dollyAgentStateService.RecordTaskStarted(VaultRootPath, chartContext, taskId, taskName, currentStep);
        _chronosLedgerService.RecordEvent(
            VaultRootPath,
            chartContext,
            "task_started",
            $"{taskName}: {currentStep}",
            AgentDisplayName,
            "task_board");
        return taskId;
    }

    private void CompleteDollyTask(ChartContext chartContext, string taskId, string currentStep, string resultSummary)
    {
        _dollyTaskBoardService.UpdateTask(
            VaultRootPath,
            chartContext,
            taskId,
            "completed",
            1,
            currentStep,
            $"{SelectedModel} / {LocalModelName}",
            "none",
            "none",
            resultSummary);
        _lastDollyTaskBoardResult = _dollyTaskBoardService.BuildStatus(VaultRootPath, chartContext);
        _dollyAgentStateService.RecordTaskUpdated(VaultRootPath, chartContext, taskId, "completed", currentStep, resultSummary);
        _chronosLedgerService.RecordEvent(
            VaultRootPath,
            chartContext,
            "task_completed",
            $"{currentStep}: {resultSummary}",
            AgentDisplayName,
            "task_board");
    }

    private void UpdateDollyTask(
        ChartContext chartContext,
        string taskId,
        string status,
        string currentStep,
        string resultSummary,
        string lastError = "none")
    {
        _dollyTaskBoardService.UpdateTask(
            VaultRootPath,
            chartContext,
            taskId,
            status,
            1,
            currentStep,
            $"{SelectedModel} / {LocalModelName}",
            lastError,
            "none",
            resultSummary);
        _lastDollyTaskBoardResult = _dollyTaskBoardService.BuildStatus(VaultRootPath, chartContext);
        _dollyAgentStateService.RecordTaskUpdated(VaultRootPath, chartContext, taskId, status, currentStep, resultSummary);
        if (!status.Equals("running", StringComparison.OrdinalIgnoreCase))
        {
            _chronosLedgerService.RecordEvent(
                VaultRootPath,
                chartContext,
                $"task_{status}",
                $"{currentStep}: {resultSummary}",
                AgentDisplayName,
                "task_board");
        }
        RefreshPreview();
    }

    private void BlockDollyTask(ChartContext chartContext, string taskId, string currentStep, string lastError)
    {
        _dollyTaskBoardService.UpdateTask(
            VaultRootPath,
            chartContext,
            taskId,
            "blocked_safety",
            1,
            currentStep,
            $"{SelectedModel} / {LocalModelName}",
            lastError,
            "none",
            "blocked");
        _lastDollyTaskBoardResult = _dollyTaskBoardService.BuildStatus(VaultRootPath, chartContext);
        _dollyAgentStateService.RecordTaskUpdated(VaultRootPath, chartContext, taskId, "blocked_safety", currentStep, lastError);
        _chronosLedgerService.RecordEvent(
            VaultRootPath,
            chartContext,
            "task_blocked",
            $"{currentStep}: {lastError}",
            AgentDisplayName,
            "task_board");
    }

    private void HandleDollyHeartbeat(object? sender, EventArgs e)
    {
        try
        {
            var chartContext = GetActiveChart();
            var taskBoard = _dollyTaskBoardService.BuildStatus(VaultRootPath, chartContext);
            _lastDollyTaskBoardResult = taskBoard;
            MaybeOfferWeeklyHealthPreferenceCheckIn(chartContext);

            if (!IsThinking && taskBoard.ActiveTaskCount == 0)
            {
                return;
            }

            _dollyAgentStateService.RecordHeartbeat(VaultRootPath, chartContext, taskBoard);

            var signature = BuildHeartbeatSignature(taskBoard);
            if (!string.Equals(signature, _lastDollyHeartbeatSignature, StringComparison.OrdinalIgnoreCase))
            {
                _lastDollyHeartbeatSignature = signature;
                RefreshPreview();
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static string BuildHeartbeatSignature(DollyTaskBoardResult taskBoard)
    {
        var line = taskBoard.Lines.FirstOrDefault() ?? string.Empty;
        line = Regex.Replace(line, @"; elapsed .*?\.", ".", RegexOptions.IgnoreCase);
        return $"{taskBoard.ActiveTaskCount}|{taskBoard.CompletedTaskCount}|{line}";
    }

    private DollyWorkingSummaryResult RefreshDollyWorkingSummary(ChartContext chartContext)
    {
        var taskId = StartDollyTask(chartContext, "Refresh Dolly working summary", "building sterile chart packet");
        var result = _dollyWorkingSummaryService.RefreshSummary(VaultRootPath, chartContext);
        _lastDollyWorkingSummaryResult = result;
        CompleteDollyTask(
            chartContext,
            taskId,
            result.Status,
            result.WasWritten
                ? $"Summary ready with {result.SectionsIncluded} section(s)."
                : "Summary refresh did not write a packet.");
        return result;
    }

    private void QueueBackgroundSummaryRefresh(ChartContext chartContext, string reason)
    {
        _backgroundSummaryQueued = true;
        _backgroundSummaryChart = chartContext;
        _backgroundSummaryReason = reason;

        if (!IsThinking)
        {
            _ = TryRunQueuedBackgroundSummaryAsync();
        }
    }

    private async Task TryRunQueuedBackgroundSummaryAsync()
    {
        if (!_backgroundSummaryQueued || _backgroundSummaryRunning || IsThinking)
        {
            return;
        }

        _backgroundSummaryRunning = true;

        try
        {
            await Task.Delay(1500);

            if (IsThinking || _backgroundSummaryChart is null)
            {
                return;
            }

            var chartContext = _backgroundSummaryChart;
            _backgroundSummaryQueued = false;
            await RunDollyWorkingSummaryThroughGemmaAsync(
                chartContext,
                _backgroundSummaryReason,
                allowDesktopFallback: false,
                announceCompletion: true,
                taskType: "background_summary",
                priority: 9);
        }
        finally
        {
            _backgroundSummaryRunning = false;

            if (_backgroundSummaryQueued && !IsThinking)
            {
                _ = TryRunQueuedBackgroundSummaryAsync();
            }
        }
    }

    private async Task<DollyWorkingSummaryResult?> RunDollyWorkingSummaryThroughGemmaAsync(
        ChartContext chartContext,
        string reason,
        bool allowDesktopFallback,
        bool announceCompletion,
        string taskType,
        int priority)
    {
        if (taskType.Equals("background_summary", StringComparison.OrdinalIgnoreCase) && IsThinking)
        {
            QueueBackgroundSummaryRefresh(chartContext, reason);
            return null;
        }

        var taskId = StartDollyTask(
            chartContext,
            "Dolly deterministic working context",
            $"refreshing C# sterile context ({reason})",
            taskType,
            priority);

        var result = _dollyWorkingSummaryService.RefreshSummary(VaultRootPath, chartContext);
        _lastDollyWorkingSummaryResult = result;

        if (!result.WasWritten || !File.Exists(result.SummaryPath))
        {
            BlockDollyTask(chartContext, taskId, result.Status, "Dolly_Working_Summary.md was not written.");
            return result;
        }

        var summaryMarkdown = File.ReadAllText(result.SummaryPath);
        var localContext = BuildDollyLocalContextPacket(chartContext, summaryMarkdown);
        _lastGemmaWorkingSummaryContext = localContext;

        var localContextPath = Path.Combine(VaultRootPath, chartContext.ChartFolderName, "wiki", "Dolly_Local_Context.md");
        File.WriteAllText(
            localContextPath,
            $"# Dolly Local Context: {chartContext.ChartId}{Environment.NewLine}{Environment.NewLine}" +
            $"> Prose-shaped sterile memory for the local fallback. Built by C# from Dolly_Working_Summary.md. No raw files or model rewrite were used.{Environment.NewLine}{Environment.NewLine}" +
            localContext.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd() +
            Environment.NewLine);
        File.WriteAllText(
            Path.Combine(VaultRootPath, chartContext.ChartFolderName, "wiki", "Dolly_Gemma_Context.md"),
            File.ReadAllText(localContextPath));
        WriteFamilyContextFile();

        CompleteDollyTask(
            chartContext,
            taskId,
            "C# local context ready",
            "Dolly local context and family context written.");

        VaultWriteStatus = "Working summary loaded through C# context";

        if (announceCompletion)
        {
            AddArchivedMessage(
                MessageAuthor.Dolly,
                $"Dolly Update - {DateTime.Now:h:mm tt}",
                [
                    "Quick update: I refreshed Dolly's local memory for this chart.",
                    $"Your requested tasks stay first in line; this context is ready for {AgentDisplayName} to use."
                ],
                mode: "dolly_background_summary",
                linkedChartId: chartContext.ChartId);
        }

        RefreshPreview();
        return result;
    }

    private void WriteFamilyContextFile()
    {
        var systemFolder = Path.Combine(VaultRootPath, "_System");
        Directory.CreateDirectory(systemFolder);
        var path = Path.Combine(systemFolder, "Family_Context.md");
        var builder = new StringBuilder();
        var activePatients = _patientRegistryService.GetAllIdentities()
            .Where(patient => !string.IsNullOrWhiteSpace(patient.PatientDisplayName))
            .Where(patient => Directory.Exists(Path.Combine(VaultRootPath, patient.ChartId)))
            .OrderBy(patient => patient.PatientDisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        builder.AppendLine("# Family Context");
        builder.AppendLine();
        builder.AppendLine("> Sterile family-vault brief for Dolly. No raw files are included.");
        builder.AppendLine();

        if (activePatients.Count == 0)
        {
            builder.AppendLine("No active patient files are registered.");
        }

        foreach (var patient in activePatients)
        {
            var chartContext = new ChartContext(patient.ChartId, patient.PatientDisplayName);
            var summaryPath = Path.Combine(VaultRootPath, chartContext.ChartFolderName, "wiki", "Dolly_Working_Summary.md");
            var summary = File.Exists(summaryPath)
                ? File.ReadAllText(summaryPath)
                : string.Empty;
            var diagnoses = ExtractMarkdownTableRows(summary, "## Diagnoses And Conditions", 3).Select(CleanPacketFact).ToList();
            var meds = ExtractMarkdownTableRows(summary, "## Medications", 3).Select(CleanPacketFact).ToList();
            var gaps = ExtractMarkdownTableRows(summary, "## Open Care Gaps", 3).Select(CleanPacketFact).ToList();
            var timeline = ExtractMarkdownTableRows(summary, "## Recent Timeline", 1).Select(CleanPacketFact).FirstOrDefault();

            builder.AppendLine($"## {patient.PatientDisplayName}");
            builder.AppendLine($"- Chart folder: {patient.ChartId}");
            builder.AppendLine(diagnoses.Count == 0
                ? "- High-signal conditions: not summarized yet."
                : $"- High-signal conditions: {JoinHumanList(diagnoses)}.");
            builder.AppendLine(meds.Count == 0
                ? "- Medications: not summarized yet."
                : $"- Medications: {JoinHumanList(meds)}.");
            builder.AppendLine(gaps.Count == 0
                ? "- Open loops: none summarized."
                : $"- Open loops: {JoinHumanList(gaps)}.");

            if (!string.IsNullOrWhiteSpace(timeline))
            {
                builder.AppendLine($"- Latest timeline item: {timeline}.");
            }

            builder.AppendLine();
        }

        builder.AppendLine("## Boundaries");
        builder.AppendLine("- Use this for roster and family-vault orientation only.");
        builder.AppendLine("- Do not reveal internal chart IDs unless the user asks for technical details.");
        builder.AppendLine("- Do not read raw files from this context.");
        File.WriteAllText(path, builder.ToString().TrimEnd() + Environment.NewLine);
    }

    private static string BuildDollyLocalContextPacket(ChartContext chartContext, string summaryMarkdown)
    {
        var builder = new StringBuilder();
        var diagnoses = ExtractMarkdownTableRows(summaryMarkdown, "## Diagnoses And Conditions", 8).Select(CleanPacketFact).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var medications = ExtractMarkdownTableRows(summaryMarkdown, "## Medications", 8).Select(CleanPacketFact).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var careGaps = ExtractMarkdownTableRows(summaryMarkdown, "## Open Care Gaps", 8).Select(CleanPacketFact).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var conflicts = ExtractMarkdownTableRows(summaryMarkdown, "## Active Conflicts And Safety Flags", 4).Select(CleanPacketFact).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var timeline = ExtractMarkdownTableRows(summaryMarkdown, "## Recent Timeline", 8).Select(CleanPacketFact).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var hooks = ExtractMarkdownBullets(summaryMarkdown, "## Conversation Hooks", 6).Select(CleanPacketFact).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        builder.AppendLine("## Patient");
        builder.AppendLine($"{chartContext.LocalDisplayName} is the active chart for this session. This is sterile wiki context only.");
        builder.AppendLine();

        builder.AppendLine("## Human Overview");
        builder.AppendLine(diagnoses.Count == 0
            ? "The chart does not yet have a clean condition summary."
            : $"The main high-signal picture is {JoinHumanList(diagnoses.Take(4).ToList())}.");
        if (medications.Count > 0)
        {
            builder.AppendLine($"Medication context includes {JoinHumanList(medications.Take(3).ToList())}.");
        }
        if (careGaps.Count > 0)
        {
            builder.AppendLine($"The active open loops are {JoinHumanList(careGaps.Take(4).ToList())}.");
        }
        if (timeline.Count > 0)
        {
            builder.AppendLine($"The latest timeline item is {timeline[0]}.");
        }
        builder.AppendLine();

        AppendPacketSection(builder, "## Conditions And Medications", diagnoses.Concat(medications).Take(10));
        AppendPacketSection(builder, "## Open Loops", careGaps.Take(8));
        AppendPacketSection(builder, "## Conflicts Or Safety Flags", conflicts.Take(4));
        AppendPacketSection(builder, "## Recent Timeline", timeline.Take(8));
        AppendPacketSection(builder, "## Conversation Hooks", hooks.Take(6));

        builder.AppendLine("## Boundaries");
        builder.AppendLine("- Do not read raw files during normal conversation.");
        builder.AppendLine("- Do not diagnose, triage, prescribe, order tests, or escalate clinically.");
        builder.AppendLine("- If the requested detail is missing here, say the local context does not contain it.");
        builder.AppendLine("- Speak as Dolly in warm, human language. Do not expose internal row syntax.");

        return builder.ToString().Trim();
    }

    private static void AppendPacketSection(StringBuilder builder, string title, IEnumerable<string> rows)
    {
        var rowList = rows.Where(row => !string.IsNullOrWhiteSpace(row)).ToList();

        builder.AppendLine(title);

        if (rowList.Count == 0)
        {
            builder.AppendLine("- No summary rows available.");
        }
        else
        {
            foreach (var row in rowList)
            {
                builder.AppendLine($"- {row}");
            }
        }

        builder.AppendLine();
    }

    private static IReadOnlyList<string> ExtractMarkdownBullets(string markdown, string heading, int maxRows)
    {
        var lines = markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var start = Array.FindIndex(lines, line => line.Trim().Equals(heading, StringComparison.OrdinalIgnoreCase));

        if (start < 0)
        {
            return [];
        }

        var rows = new List<string>();

        for (var index = start + 1; index < lines.Length; index++)
        {
            var line = lines[index].Trim();

            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                break;
            }

            if (line.StartsWith("- ", StringComparison.Ordinal))
            {
                rows.Add(StripWikiSyntax(line[2..]));
            }

            if (rows.Count >= maxRows)
            {
                break;
            }
        }

        return rows;
    }

    private static bool IsWorkingSummaryGemmaFailure(string value)
    {
        return value.Equals("LOCAL_GEMMA_WORKING_SUMMARY_UNAVAILABLE", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("LOCAL_GEMMA_WORKING_SUMMARY_EMPTY", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("I checked the allowed vault context", StringComparison.OrdinalIgnoreCase);
    }

    private async Task WarmLocalModelAsync()
    {
        ApplyLocalModelRoute(DesktopGemmaEndpoint, PreferredGemmaModelName);
        LocalModelReadinessStatus = "Local helper warming...";
        SetLocalLlmState(DesktopGemmaEndpoint, true, "Local helper warming");

        LocalModelWarmupResult result;

        try
        {
            result = await _localModelWarmupService.WarmAsync(LocalModelEndpoint, LocalModelName);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            result = new LocalModelWarmupResult
            {
                IsAvailable = false,
                Message = "Local helper warmup could not connect."
            };
        }

        LocalModelReadinessStatus = result.IsAvailable
            ? $"Local helper {result.Message}"
            : $"{result.Message} Local privacy/status work will wait until the helper is available.";
        SetLocalLlmState(DesktopGemmaEndpoint, false, result.IsAvailable ? "Local helper ready" : "Local helper unavailable");
    }

    private Task EnsureLocalModelRouteReadyAsync()
    {
        ApplyLocalModelRoute(DesktopGemmaEndpoint, PreferredGemmaModelName);
        LocalModelReadinessStatus = "Local helper selected for privacy/status work.";
        return Task.CompletedTask;
    }

    private async Task<OmniboxPreflightResult> ReviewOmniboxPreflightWithFailoverAsync(
        string userText,
        IReadOnlyList<AttachmentViewModel> sentAttachments,
        IReadOnlyList<PatientIdentityRecord> knownPatients)
    {
        await EnsureLocalModelRouteReadyAsync();

        var endpoint = LocalModelEndpoint;
        var modelName = LocalModelName;
        SetLocalLlmState(endpoint, true, "Local helper preflight");
        var finalStatus = "Local helper ready";

        try
        {
            var result = await _omniboxPreflightService.ReviewAsync(
                endpoint,
                modelName,
                userText,
                ActivePatientDisplayName,
                sentAttachments,
                knownPatients,
                BuildRecentConversationContext());

            finalStatus = result.IsAvailable ? "Local helper ready" : "Local helper preflight failed";
            StampPreflightRoute(result, endpoint, modelName, false, LocalModelReadinessStatus);
            return result;
        }
        finally
        {
            SetLocalLlmState(endpoint, false, finalStatus);
        }
    }

    private async Task<LocalModelReviewResult> ReviewScrubbedPayloadsWithFailoverAsync(
        ChatMessageViewModel thinkingMessage,
        IReadOnlyList<ScrubResult> scrubResults)
    {
        var endpoint = LocalModelEndpoint;
        var modelName = LocalModelName;
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        SetLocalLlmState(endpoint, true, "Local privacy review");
        var finalStatus = "Local helper ready";

        try
        {
            var result = await _localModelReviewService.ReviewScrubbedPayloadAsync(
                endpoint,
                modelName,
                scrubResults,
                timeout.Token);
            finalStatus = result.IsAvailable ? "Local helper ready" : "Local privacy review failed";
            StampLocalModelRoute(result, endpoint, modelName, false, LocalModelReadinessStatus);
            return result;
        }
        finally
        {
            SetLocalLlmState(endpoint, false, finalStatus);
        }
    }

    private async Task<List<SecondPassScrubResult>> BuildApiEligibleScrubbedPayloadsWithRetriesAsync(
        ChatMessageViewModel thinkingMessage,
        ChartContext chartContext)
    {
        var apiEligibleScrubbedPayloads = new List<SecondPassScrubResult>();

        if (_lastScrubResults.Count == 0)
        {
            _lastLocalModelReview = null;
            _lastSecondPassScrubResults.Clear();
            return apiEligibleScrubbedPayloads;
        }

        for (var attempt = 1; attempt <= MaxTaskCompletionAttempts; attempt++)
        {
            VaultWriteStatus = attempt == 1
                ? "Running privacy review"
                : $"Retrying privacy review ({attempt}/{MaxTaskCompletionAttempts})";
            UpdateDollyStatus(
                thinkingMessage,
                attempt == 1
                    ? "The local Presidio privacy service is checking the scrubbed text for identifiers before anything can go to the API."
                    : $"The task is not complete yet, so I am retrying the local privacy path ({attempt}/{MaxTaskCompletionAttempts}) instead of quitting.");
            RefreshPreview();

            await EnsureLocalModelRouteReadyAsync();
            _lastLocalModelReview = await ReviewScrubbedPayloadsWithFailoverAsync(thinkingMessage, _lastScrubResults);

            _lastSecondPassScrubResults.Clear();

            if (_lastLocalModelReview is { IsAvailable: true })
            {
                VaultWriteStatus = attempt == 1
                    ? "Saving safe chart text"
                    : $"Saving safe chart text ({attempt}/{MaxTaskCompletionAttempts})";
                UpdateDollyStatus(
                    thinkingMessage,
                    "I am validating the local privacy findings and saving the safe chart text.");
                RefreshPreview();
                _lastSecondPassScrubResults.AddRange(
                    _secondPassScrubberService.SaveFinalScrubbedPayloads(
                        VaultRootPath,
                        chartContext,
                        _lastScrubResults,
                        _lastLocalModelReview));
            }

            apiEligibleScrubbedPayloads = _lastSecondPassScrubResults
                .Where(IsApiEligibleScrubbedPayload)
                .ToList();

            if (apiEligibleScrubbedPayloads.Count > 0)
            {
                if (attempt > 1)
                {
                    UpdateDollyStatus(
                        thinkingMessage,
                        $"The retry worked on attempt {attempt}. I have safe text ready for chart review, and I am continuing the task.");
                    RefreshPreview();
                }

                return apiEligibleScrubbedPayloads;
            }

            if (!ShouldRetryIncompletePrivacyPath(_lastLocalModelReview, _lastSecondPassScrubResults) ||
                attempt == MaxTaskCompletionAttempts)
            {
                return apiEligibleScrubbedPayloads;
            }

            PreferAlternateLocalModelRouteForRetry(attempt);
            await Task.Delay(TimeSpan.FromMilliseconds(350));
        }

        return apiEligibleScrubbedPayloads;
    }

    private static bool ShouldRetryIncompletePrivacyPath(
        LocalModelReviewResult? review,
        IReadOnlyList<SecondPassScrubResult> secondPassResults)
    {
        if (review is null)
        {
            return true;
        }

        if (!review.IsAvailable)
        {
            return true;
        }

        return secondPassResults.Any(result =>
            result.Status.Contains("MODEL_PASS_UNAVAILABLE", StringComparison.OrdinalIgnoreCase) ||
            result.Status.Contains("AUDIT_MODEL_PASS_UNAVAILABLE", StringComparison.OrdinalIgnoreCase));
    }

    private void PreferAlternateLocalModelRouteForRetry(int attempt)
    {
        ApplyLocalModelRoute(DesktopGemmaEndpoint, PreferredGemmaModelName);
    }

    private static void StampPreflightRoute(
        OmniboxPreflightResult result,
        string endpoint,
        string modelName,
        bool failoverUsed,
        string routeStatus)
    {
        result.Route = DescribeGemmaRoute(endpoint, failoverUsed);
        result.EndpointUsed = endpoint;
        result.ModelNameUsed = modelName;
        result.FailoverUsed = failoverUsed;
        result.RouteStatus = routeStatus;
    }

    private static void StampLocalModelRoute(
        LocalModelReviewResult result,
        string endpoint,
        string modelName,
        bool failoverUsed,
        string routeStatus)
    {
        result.Route = DescribeGemmaRoute(endpoint, failoverUsed);
        result.EndpointUsed = endpoint;
        result.ModelNameUsed = modelName;
        result.FailoverUsed = failoverUsed;
        result.RouteStatus = routeStatus;
    }

    private static string DescribeGemmaRoute(string endpoint, bool failoverUsed)
    {
        return failoverUsed ? "host_fallback" : "host_local";
    }

    private void ApplyLocalModelRoute(string endpoint, string modelName)
    {
        var changed = false;

        if (!string.Equals(_settings.LocalModelEndpoint, endpoint, StringComparison.OrdinalIgnoreCase))
        {
            _settings.LocalModelEndpoint = endpoint;
            OnPropertyChanged(nameof(LocalModelEndpoint));
            changed = true;
        }

        if (!string.Equals(_settings.LocalModelName, modelName, StringComparison.OrdinalIgnoreCase))
        {
            _settings.LocalModelName = modelName;
            OnPropertyChanged(nameof(LocalModelName));
            changed = true;
        }

        if (changed)
        {
            _settingsService.Save(_settings);
            RefreshPreview();
        }
    }

    private sealed record GemmaRouteProbeResult(bool IsAvailable, string Message, string ModelName);

    private ChatMessageViewModel AddArchivedMessage(
        MessageAuthor author,
        string label,
        IEnumerable<string> paragraphs,
        IEnumerable<AttachmentViewModel>? attachments = null,
        string mode = "conversation",
        string linkedChartId = "")
    {
        var paragraphList = paragraphs.ToList();
        if (author == MessageAuthor.Dolly && ShouldPersonalizeNameDisplay(mode))
        {
            var chartId = string.IsNullOrWhiteSpace(linkedChartId) ? ActiveChartId : linkedChartId;
            var chartContext = new ChartContext(chartId, _patientRegistryService.ResolveDisplayName(chartId));
            var knownPatients = _patientRegistryService.GetAllIdentities();
            paragraphList = paragraphList
                .Select(line => _responsePersonalizationService.Personalize(line, chartContext, knownPatients))
                .ToList();
        }

        var attachmentList = attachments?.ToList() ?? [];
        var message = new ChatMessageViewModel(author, label, paragraphList, attachmentList);

        Messages.Add(message);
        _conversationArchiveService.Append(
            VaultRootPath,
            author,
            label,
            paragraphList,
            attachmentList.Select(attachment => new ConversationAttachmentLog
            {
                DisplayName = attachment.DisplayName,
                Path = attachment.Path,
                Kind = attachment.Kind
            }),
            string.IsNullOrWhiteSpace(linkedChartId) ? ActiveChartId : linkedChartId,
            mode,
            IsSyntheticTestMode,
            _patientRegistryService.GetAllIdentities());

        RecordChronosConversationEvent(author, mode, linkedChartId, paragraphList);

        if (author == MessageAuthor.Dolly && IsAgentTurnCompletionMode(mode))
        {
            _dollyAgentStateService.CompleteTurn(
                VaultRootPath,
                new ChartContext(
                    string.IsNullOrWhiteSpace(linkedChartId) ? ActiveChartId : linkedChartId,
                    ActivePatientDisplayName),
                mode,
                string.Join(" ", paragraphList).Trim());
        }

        OnPropertyChanged(nameof(ConversationArchiveStatus));
        return message;
    }

    private void RecordChronosConversationEvent(
        MessageAuthor author,
        string mode,
        string linkedChartId,
        IReadOnlyList<string> paragraphList)
    {
        try
        {
            var chartId = string.IsNullOrWhiteSpace(linkedChartId) ? ActiveChartId : linkedChartId;
            var chartContext = string.IsNullOrWhiteSpace(chartId)
                ? null
                : new ChartContext(chartId, _patientRegistryService.ResolveDisplayName(chartId));
            var actor = author == MessageAuthor.User ? "User" : AgentDisplayName;
            var summary = paragraphList.Count == 0
                ? mode
                : TruncateForChronos(string.Join(" ", paragraphList));

            _chronosLedgerService.RecordEvent(
                VaultRootPath,
                chartContext,
                author == MessageAuthor.User ? "user_message" : $"dolly_{mode}",
                summary,
                actor,
                "conversation");
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static string TruncateForChronos(string value, int maxLength = 220)
    {
        var cleaned = string.IsNullOrWhiteSpace(value)
            ? "No summary."
            : value.ReplaceLineEndings(" ").Trim();

        return cleaned.Length <= maxLength
            ? cleaned
            : cleaned[..maxLength].TrimEnd() + "...";
    }

    private static bool ShouldPersonalizeNameDisplay(string mode)
    {
        return mode.StartsWith("gemma_action", StringComparison.OrdinalIgnoreCase) ||
               mode.StartsWith("backend_", StringComparison.OrdinalIgnoreCase) ||
               mode.Equals("dolly_api_action_packet_invalid", StringComparison.OrdinalIgnoreCase);
    }

    private void AttachFiles()
    {
        AddAttachmentPaths(_filePickerService.PickFiles());
    }

    public void AddAttachmentPaths(IEnumerable<string> paths)
    {
        var addedAny = false;

        foreach (var path in paths)
        {
            if (Attachments.Any(attachment => string.Equals(attachment.Path, path, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            Attachments.Add(new AttachmentViewModel(path));
            addedAny = true;
        }

        if (addedAny)
        {
            OnStatusChanged();
            RefreshPreview();
        }

        SendMessageCommand.RaiseCanExecuteChanged();
    }

    private void RemoveAttachment(object? parameter)
    {
        if (parameter is not AttachmentViewModel attachment)
        {
            return;
        }

        Attachments.Remove(attachment);
        OnStatusChanged();
        RefreshPreview();
        SendMessageCommand.RaiseCanExecuteChanged();
    }

    private void OpenAttachment(object? parameter)
    {
        if (parameter is not AttachmentViewModel attachment ||
            string.IsNullOrWhiteSpace(attachment.Path) ||
            !File.Exists(attachment.Path))
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = attachment.Path,
            UseShellExecute = true
        });
    }

    private void HandleInitialSetupMessage()
    {
        var userText = PromptText.Trim();
        var sentAttachments = Attachments.ToList();
        if (string.IsNullOrWhiteSpace(userText) && sentAttachments.Count > 0)
        {
            userText = "Uploaded setup item.";
        }

        AddArchivedMessage(
            MessageAuthor.User,
            $"User - {DateTime.Now:h:mm tt}",
            [userText],
            sentAttachments,
            mode: "initial_setup_user");
        PromptText = string.Empty;
        Attachments.Clear();

        var reply = ApplyInitialSetupAnswer(userText, sentAttachments);
        AddArchivedMessage(
            MessageAuthor.Dolly,
            AgentLabel,
            reply,
            mode: IsInitialSetupActive ? "initial_setup_step" : "initial_setup_complete");

        IsThinking = false;
        RefreshPreview();
        SendMessageCommand.RaiseCanExecuteChanged();
    }

    private void StartInitialSetupFromCommand()
    {
        var userText = PromptText.Trim();
        AddArchivedMessage(
            MessageAuthor.User,
            $"User - {DateTime.Now:h:mm tt}",
            [userText],
            mode: "run_setup_request");
        PromptText = string.Empty;

        _settings.IsInitialSetupComplete = false;
        _settings.InitialSetupStep = SetupStepChartManagerName;
        _settings.ActiveUserRole = "Chart Manager";
        SaveSetupProgress();
        OnPropertyChanged(nameof(IsInitialSetupActive));
        OnPropertyChanged(nameof(PromptPlaceholder));

        AddArchivedMessage(
            MessageAuthor.Dolly,
            AgentLabel,
            [
                "Sure. I restarted VitaMR setup.",
                "Who is setting up VitaMR today? This person becomes the Chart Manager with full control."
            ],
            mode: "initial_setup_restart");

        IsThinking = false;
        RefreshPreview();
        SendMessageCommand.RaiseCanExecuteChanged();
    }

    private void HandleSetupRepairCommand()
    {
        var userText = PromptText.Trim();
        AddArchivedMessage(
            MessageAuthor.User,
            $"User - {DateTime.Now:h:mm tt}",
            [userText],
            mode: "setup_repair_request");
        PromptText = string.Empty;

        var issues = DetectSetupRepairIssues();
        if (issues.Count == 0)
        {
            AddArchivedMessage(
                MessageAuthor.Dolly,
                AgentLabel,
                [
                    "I checked setup and do not see obvious accidental values.",
                    "The Chart Manager can still run setup again anytime by typing Run setup."
                ],
                mode: "setup_repair_clean");
            IsThinking = false;
            RefreshPreview();
            SendMessageCommand.RaiseCanExecuteChanged();
            return;
        }

        _pendingSetupRepairConfirmation = true;
        AddArchivedMessage(
            MessageAuthor.Dolly,
            AgentLabel,
            BuildSetupRepairOfferLines(issues),
            mode: "setup_repair_offer");
        IsThinking = false;
        RefreshPreview();
        SendMessageCommand.RaiseCanExecuteChanged();
    }

    private void HandleSetupRepairConfirmation(string userText, ChatMessageViewModel thinkingMessage)
    {
        Messages.Remove(thinkingMessage);
        if (IsAffirmative(userText))
        {
            var lines = ApplySetupRepair();
            _pendingSetupRepairConfirmation = false;
            AddArchivedMessage(
                MessageAuthor.Dolly,
                AgentLabel,
                lines,
                mode: "setup_repair_applied");
            IsThinking = false;
            RefreshPreview();
            SendMessageCommand.RaiseCanExecuteChanged();
            return;
        }

        if (IsNegative(userText))
        {
            _pendingSetupRepairConfirmation = false;
            AddArchivedMessage(
                MessageAuthor.Dolly,
                AgentLabel,
                ["Okay. I left setup unchanged."],
                mode: "setup_repair_canceled");
            IsThinking = false;
            RefreshPreview();
            SendMessageCommand.RaiseCanExecuteChanged();
            return;
        }

        AddArchivedMessage(
            MessageAuthor.Dolly,
            AgentLabel,
            ["I have setup repair paused. Reply yes to apply the safe cleanup, or no to leave setup unchanged."],
            mode: "setup_repair_waiting");
        IsThinking = false;
        RefreshPreview();
        SendMessageCommand.RaiseCanExecuteChanged();
    }

    private static bool IsRunSetupRequest(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return Regex.IsMatch(
            value.Trim(),
            @"\b(run|start|restart|redo|change|update)\s+(the\s+)?(setup|first[- ]run setup|initial setup)\b|\b(setup mode)\b",
            RegexOptions.IgnoreCase);
    }

    private static bool IsRepairSetupRequest(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return Regex.IsMatch(
            value.Trim(),
            @"\b(repair|fix|clean|cleanup|reset)\s+(the\s+)?(setup|first[- ]run|manager setup)\b|\bsetup repair\b",
            RegexOptions.IgnoreCase);
    }

    private static bool IsCleanupChartsRequest(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return Regex.IsMatch(
            value.Trim(),
            @"\b(clean|cleanup|review|show|find)\s+(bad|fake|accidental|test)?\s*(charts|patient files)\b|\bchart cleanup\b",
            RegexOptions.IgnoreCase);
    }

    private IReadOnlyList<string> ApplyInitialSetupAnswer(
        string userText,
        IReadOnlyList<AttachmentViewModel>? sentAttachments = null)
    {
        sentAttachments ??= [];
        var normalized = NormalizeSetupStep(_settings.InitialSetupStep);
        EnsureSetupFiles();

        switch (normalized)
        {
            case SetupStepChartManagerName:
                if (string.IsNullOrWhiteSpace(userText) || IsRunSetupRequest(userText))
                {
                    return
                    [
                        "Setup is ready.",
                        "Who is setting up VitaMR today? This person becomes the Chart Manager with full control."
                    ];
                }

                _settings.ChartManagerName = CleanSetupValue(userText);
                _settings.ActiveUserName = _settings.ChartManagerName;
                _settings.ActiveUserRole = "Chart Manager";
                _settings.VaultOwnerName = _settings.ChartManagerName;
                _settings.VaultOwnerRole = "Chart Manager";
                _settings.InitialSetupStep = SetupStepManagerPassword;
                SaveSetupProgress();
                return
                [
                    $"{_settings.ChartManagerName} is now the Chart Manager.",
                    "That person has full control of VitaMR and can later grant access to others.",
                    "Choose a manager confirmation phrase or password. This protects chart creation and deletion."
                ];

            case SetupStepManagerPassword:
                if (string.IsNullOrWhiteSpace(userText))
                {
                    return ["Please enter a confirmation phrase or password for the Chart Manager."];
                }

                _settings.ChartManagerPassword = CleanSetupValue(userText);
                _settings.VaultOwnerDeletePassword = _settings.ChartManagerPassword;
                _settings.InitialSetupStep = SetupStepManagerProfile;
                SaveSetupProgress();
                return
                [
                    "Saved. I will require that confirmation before protected chart-manager actions.",
                    "Would you like to add a nickname or photo for the Chart Manager now? You can attach an image and type something like nickname Manager, or type skip."
                ];

            case SetupStepManagerProfile:
                if (!IsSkipSetupAnswer(userText))
                {
                    ApplyChartManagerProfileDetails(userText, sentAttachments, allowPlainNickname: true);
                }

                _settings.InitialSetupStep = SetupStepAgentName;
                SaveSetupProgress();
                return
                [
                    BuildChartManagerProfileSavedLine(),
                    "What would you like to call the agent? You can keep Dolly, or type a new name."
                ];

            case SetupStepAgentName:
                if (IsDefaultDollyAnswer(userText))
                {
                    _settings.AgentDisplayName = "Dolly";
                }
                else if (!IsSkipSetupAnswer(userText))
                {
                    _settings.AgentDisplayName = CleanSetupValue(userText);
                }

                _settings.InitialSetupStep = SetupStepAgentTone;
                SaveSetupProgress();
                OnPropertyChanged(nameof(AgentDisplayName));
                return
                [
                    $"Great. I will answer as {AgentDisplayName}.",
                    "Pick a starting personality style: friendly, coach, or straight shooter."
                ];

            case SetupStepAgentTone:
                _settings.AgentTone = NormalizeAgentTone(userText);
                _settings.InitialSetupStep = SetupStepPatientNames;
                SaveSetupProgress();
                return
                [
                    $"Saved. {AgentDisplayName}'s starting style is {_settings.AgentTone}.",
                    "Do you want to add patient chart names now? Type names separated by commas, or type skip."
                ];

            case SetupStepPatientNames:
                if (IsAffirmative(userText))
                {
                    return
                    [
                        "Okay. Type the patient chart names separated by commas, or type skip to keep the current charts as they are."
                    ];
                }

                if (!IsSkipPatientChartSetupAnswer(userText))
                {
                    var added = AddSetupPatientNames(userText);
                    _settings.SetupPatientNames = MergeSetupList(_settings.SetupPatientNames, added);
                    SaveSetupProgress();
                    if (added.Count > 0)
                    {
                        _settings.InitialSetupStep = SetupStepAdditionalUsers;
                        SaveSetupProgress();
                        return
                        [
                            added.Count == 1
                                ? $"I created one starter chart: {added[0]}."
                                : $"I created starter charts for: {string.Join(", ", added)}.",
                            "Should anyone else have access to VitaMR right now? Type their names and access notes, or type skip."
                        ];
                    }
                }

                _settings.InitialSetupStep = SetupStepAdditionalUsers;
                SaveSetupProgress();
                return
                [
                    "No patient charts added right now. I will keep the current charts as they are.",
                    "Should anyone else have access to VitaMR right now? Type their names and access notes, or type skip."
                ];

            case SetupStepAdditionalUsers:
                if (IsAffirmative(userText))
                {
                    return
                    [
                        "Who should have access? Type their names and access notes, or type skip."
                    ];
                }

                if (!IsSkipSetupAnswer(userText))
                {
                    _settings.SetupAdditionalUsers = CleanSetupValue(userText);
                }

                _settings.InitialSetupStep = SetupStepPersonaInterview;
                SaveSetupProgress();
                return
                [
                    IsSkipSetupAnswer(userText)
                        ? "Okay. For now, only the Chart Manager has setup authority."
                        : "Saved. The Chart Manager can revise those access notes later.",
                    "Optional personality setup: when health information feels stressful, should I be gentle, practical, or very direct? You can also type skip."
                ];

            case SetupStepPersonaInterview:
                if (!IsSkipSetupAnswer(userText))
                {
                    _settings.SetupPersonaPreference = CleanSetupValue(userText);
                }

                _settings.IsInitialSetupComplete = true;
                _settings.InitialSetupStep = SetupStepComplete;
                SaveSetupProgress();
                OnPropertyChanged(nameof(IsInitialSetupActive));
                OnPropertyChanged(nameof(PromptPlaceholder));
                return
                [
                    "All set.",
                    $"{AgentDisplayName} knows who manages the vault, what to call the agent, and the starting communication style.",
                    "You can add charts, photos, reminders, vaccine records, and source documents whenever you are ready."
                ];

            default:
                _settings.InitialSetupStep = SetupStepChartManagerName;
                SaveSetupProgress();
                return BuildSetupPromptLines();
        }
    }

    private IReadOnlyList<string> BuildSetupPromptLines()
    {
        return NormalizeSetupStep(_settings.InitialSetupStep) switch
        {
            SetupStepManagerPassword =>
            [
                $"Setup is in progress. Chart Manager: {FirstNonEmpty(_settings.ChartManagerName, "not set")}.",
                "Enter the manager confirmation phrase/password to continue."
            ],
            SetupStepManagerProfile =>
            [
                "Setup is in progress.",
                "Add a Chart Manager nickname or photo now, or type skip."
            ],
            SetupStepAgentName =>
            [
                "Setup is in progress.",
                "What would you like to call the agent? Type Dolly to keep the default, or enter a new name."
            ],
            SetupStepAgentTone =>
            [
                "Setup is in progress.",
                "Pick a starting personality style: friendly, coach, or straight shooter."
            ],
            SetupStepPatientNames =>
            [
                "Setup is in progress.",
                "Add patient chart names now by typing names separated by commas, or type skip."
            ],
            SetupStepAdditionalUsers =>
            [
                "Setup is in progress.",
                "Should anyone else have access to VitaMR right now? Type their names and access notes, or type skip."
            ],
            SetupStepPersonaInterview =>
            [
                "Setup is almost done.",
                "Optional personality setup: when health information feels stressful, should I be gentle, practical, or very direct? You can type skip."
            ],
            _ =>
            [
                "Welcome to VitaMR. Let's do the quick first-run setup.",
                "Who is setting up VitaMR today? This person becomes the Chart Manager with full control."
            ]
        };
    }

    private string BuildSetupPlaceholder()
    {
        return NormalizeSetupStep(_settings.InitialSetupStep) switch
        {
            SetupStepManagerPassword => "Enter Chart Manager confirmation phrase...",
            SetupStepManagerProfile => "Nickname/photo for Chart Manager, or skip...",
            SetupStepAgentName => "Agent name, such as Dolly...",
            SetupStepAgentTone => "friendly, coach, or straight shooter...",
            SetupStepPatientNames => "Patient names separated by commas, or skip...",
            SetupStepAdditionalUsers => "Other users and access notes, or skip...",
            SetupStepPersonaInterview => "gentle, practical, very direct, or skip...",
            _ => "Name of the person setting up VitaMR..."
        };
    }

    private void SaveSetupProgress()
    {
        _settings.InitialSetupStep = NormalizeSetupStep(_settings.InitialSetupStep);
        _settingsService.Save(_settings);
        WriteVaultAccessFile();
        WriteAgentPreferencesFile();
        WriteChartManagerPersonaFile();
        _chronosLedgerService.RecordEvent(
            VaultRootPath,
            null,
            _settings.IsInitialSetupComplete ? "initial_setup_completed" : "initial_setup_progress",
            $"Initial setup saved at step {_settings.InitialSetupStep}.",
            AgentDisplayName,
            "setup");
    }

    private void EnsureSetupFiles()
    {
        Directory.CreateDirectory(Path.Combine(VaultRootPath, "_System"));
        Directory.CreateDirectory(Path.Combine(VaultRootPath, "_System", "Dolly"));
        Directory.CreateDirectory(GetChartManagerFolderPath());
        WriteVaultAccessFile();
        WriteAgentPreferencesFile();
        WriteChartManagerPersonaFile();
    }

    private void WriteVaultAccessFile()
    {
        var systemFolder = Path.Combine(VaultRootPath, "_System");
        Directory.CreateDirectory(systemFolder);
        File.WriteAllText(
            Path.Combine(systemFolder, "Vault_Access.md"),
            NormalizeMarkdownContent($"""
            # Vault Access

            Setup complete: {_settings.IsInitialSetupComplete}
            Setup step: {_settings.InitialSetupStep}

            ## Chart Manager
            Name: {FirstNonEmpty(_settings.ChartManagerName, "not set")}
            Nickname: {FirstNonEmpty(_settings.ChartManagerNickname, "not set")}
            Persona file: {GetChartManagerPersonaRelativePath()}
            Photo: {FirstNonEmpty(GetChartManagerPhotoRelativePath(), "not set")}
            Role: Full control
            Can add charts: yes
            Can delete charts: yes
            Can grant access to others: yes

            ## Active User
            Name: {FirstNonEmpty(_settings.ActiveUserName, _settings.ChartManagerName)}
            Role: {FirstNonEmpty(_settings.ActiveUserRole, "Chart Manager")}

            ## Starter Patient Charts
            {FormatSetupList(_settings.SetupPatientNames)}

            ## Additional Users / Access Notes
            {FirstNonEmpty(_settings.SetupAdditionalUsers, "None yet.")}
            """));
    }

    private void WriteAgentPreferencesFile()
    {
        var dollyFolder = Path.Combine(VaultRootPath, "_System", "Dolly");
        Directory.CreateDirectory(dollyFolder);
        File.WriteAllText(
            Path.Combine(dollyFolder, "Agent_Preferences.md"),
            NormalizeMarkdownContent($"""
            # Agent Preferences

            Agent display name: {AgentDisplayName}
            Starting tone: {FirstNonEmpty(_settings.AgentTone, "Friendly")}

            ## Chart Manager Preference
            {FirstNonEmpty(_settings.ChartManagerPersonaStyle, FirstNonEmpty(_settings.SetupPersonaPreference, "No optional persona interview completed yet."))}

            ## Feedback Loop
            Users can update the agent style over time by saying things like:
            - Be more direct.
            - Be warmer.
            - Give shorter answers.
            - Show more detail when something changes.
            """));
    }

    private void WriteChartManagerPersonaFile()
    {
        var managerFolder = GetChartManagerFolderPath();
        Directory.CreateDirectory(managerFolder);
        Directory.CreateDirectory(Path.Combine(managerFolder, "profile"));

        File.WriteAllText(
            Path.Combine(managerFolder, "Manager_Profile.md"),
            NormalizeMarkdownContent($"""
            # Chart Manager Persona

            Name: {FirstNonEmpty(_settings.ChartManagerName, "not set")}
            Nickname: {FirstNonEmpty(_settings.ChartManagerNickname, "not set")}
            Role: Chart Manager
            Active user role: {FirstNonEmpty(_settings.ActiveUserRole, "Chart Manager")}
            Photo: {FirstNonEmpty(GetChartManagerPhotoRelativePath(), "not set")}

            ## Authority
            Can add patient charts: yes
            Can delete patient charts: yes
            Can grant access to others: yes
            Can review all charts in this VitaMR vault: yes

            ## Communication Style In Manager Role
            {FirstNonEmpty(_settings.ChartManagerPersonaStyle, FirstNonEmpty(_settings.SetupPersonaPreference, "No manager-role style preference saved yet."))}

            ## Boundary
            This manager persona is separate from any patient chart for the same person. A Chart Manager can also have a patient chart, but manager-role preferences and patient-role preferences stay separate.
            """));
    }

    private IReadOnlyList<string> DetectSetupRepairIssues()
    {
        var issues = new List<string>();

        if (LooksLikeSetupControlText(_settings.ChartManagerName))
        {
            issues.Add($"Chart Manager name looks accidental: {_settings.ChartManagerName}");
        }

        if (LooksLikeSetupControlText(_settings.ActiveUserName))
        {
            issues.Add($"Active user name looks accidental: {_settings.ActiveUserName}");
        }

        if (LooksLikeDefaultAgentControlText(_settings.AgentDisplayName))
        {
            issues.Add($"Agent name looks like an instruction instead of a name: {_settings.AgentDisplayName}");
        }

        if (LooksLikePatientChartSkipText(_settings.ActivePatientDisplayName))
        {
            issues.Add($"Active chart label looks like setup control text: {_settings.ActivePatientDisplayName}");
        }

        var accidentalCharts = GetAccidentalChartCandidates();
        if (accidentalCharts.Count > 0)
        {
            issues.Add($"Possible accidental chart files: {string.Join(", ", accidentalCharts.Take(4).Select(patient => patient.PatientDisplayName))}");
        }

        return issues;
    }

    private IEnumerable<string> BuildSetupRepairOfferLines(IReadOnlyList<string> issues)
    {
        yield return "I found setup values that look accidental from testing.";
        foreach (var issue in issues.Take(6))
        {
            yield return $"- {issue}";
        }

        yield return "I can safely repair setup labels and point the active chart back to a real chart.";
        yield return "I will not delete any chart from this repair. Type yes to repair setup, or no to leave it unchanged.";
    }

    private IReadOnlyList<string> ApplySetupRepair()
    {
        var lines = new List<string>();

        if (LooksLikeSetupControlText(_settings.ChartManagerName))
        {
            _settings.ChartManagerName = FirstNonEmpty(_settings.ChartManagerNickname, "Vault Manager");
            _settings.VaultOwnerName = _settings.ChartManagerName;
            lines.Add($"Chart Manager name repaired to {_settings.ChartManagerName}.");
        }

        if (LooksLikeSetupControlText(_settings.ActiveUserName))
        {
            _settings.ActiveUserName = FirstNonEmpty(_settings.ChartManagerNickname, _settings.ChartManagerName, "Vault Manager");
            lines.Add($"Active user repaired to {_settings.ActiveUserName}.");
        }

        if (LooksLikeDefaultAgentControlText(_settings.AgentDisplayName))
        {
            _settings.AgentDisplayName = "Dolly";
            OnPropertyChanged(nameof(AgentDisplayName));
            lines.Add("Agent name repaired to Dolly.");
        }

        if (LooksLikePatientChartSkipText(_settings.ActivePatientDisplayName))
        {
            var replacement = _patientRegistryService.GetAllIdentities()
                .Where(patient => !LooksLikeAccidentalChartName(patient.PatientDisplayName))
                .OrderBy(patient => patient.PatientDisplayName, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
            if (replacement is not null)
            {
                ActiveChartId = replacement.ChartId;
                ActivePatientDisplayName = replacement.PatientDisplayName;
                lines.Add($"Active chart moved back to {replacement.PatientDisplayName}.");
            }
        }

        if (lines.Count == 0)
        {
            lines.Add("I checked setup again and did not find a safe automatic repair to apply.");
        }

        SaveSetupProgress();
        var accidentalCharts = GetAccidentalChartCandidates();
        if (accidentalCharts.Count > 0)
        {
            lines.Add($"I also found possible accidental charts: {string.Join(", ", accidentalCharts.Select(patient => patient.PatientDisplayName))}.");
            lines.Add("To clean those up, type chart cleanup and I will give the Chart Manager confirmation phrase for each one.");
        }

        return lines;
    }

    private IReadOnlyList<PatientIdentityRecord> GetAccidentalChartCandidates()
    {
        return _patientRegistryService.GetAllIdentities()
            .Where(patient => LooksLikeAccidentalChartName(patient.PatientDisplayName))
            .OrderBy(patient => patient.PatientDisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool LooksLikeAccidentalChartName(string value)
    {
        return LooksLikeSetupControlText(value) ||
            LooksLikeDefaultAgentControlText(value) ||
            LooksLikePatientChartSkipText(value);
    }

    private static bool LooksLikeSetupControlText(string value)
    {
        var normalized = CleanSetupValue(value).ToLowerInvariant();
        return normalized is "run setup" or
            "start setup" or
            "restart setup" or
            "setup mode" or
            "redo setup" or
            "change setup";
    }

    private static bool LooksLikeDefaultAgentControlText(string value)
    {
        var normalized = CleanSetupValue(value).ToLowerInvariant();
        return normalized is "keep dolly" or
            "keep as dolly" or
            "keep the name dolly" or
            "use default" or
            "keep default";
    }

    private static bool LooksLikePatientChartSkipText(string value)
    {
        var normalized = CleanSetupValue(value).ToLowerInvariant().Trim('.');
        return normalized is "keep current charts as is" or
            "keep current charts" or
            "keep charts as is" or
            "no new charts" or
            "use existing charts" or
            "leave charts alone" or
            "leave current charts";
    }

    private void ApplyChartManagerProfileDetails(
        string userText,
        IReadOnlyList<AttachmentViewModel> sentAttachments,
        bool allowPlainNickname)
    {
        var nickname = TryExtractNickname(userText);
        if (string.IsNullOrWhiteSpace(nickname) &&
            allowPlainNickname &&
            !string.IsNullOrWhiteSpace(userText) &&
            !userText.Equals("Uploaded setup item.", StringComparison.OrdinalIgnoreCase) &&
            !LooksLikePhotoOnlyProfileText(userText))
        {
            nickname = CleanSetupValue(userText);
        }

        if (!string.IsNullOrWhiteSpace(nickname))
        {
            _settings.ChartManagerNickname = nickname;
        }

        var style = TryExtractManagerStyle(userText);
        if (!string.IsNullOrWhiteSpace(style))
        {
            _settings.ChartManagerPersonaStyle = style;
        }

        var photo = sentAttachments.FirstOrDefault(IsProfilePhotoAttachment);
        if (photo is not null && File.Exists(photo.Path))
        {
            _settings.ChartManagerPhotoPath = SaveChartManagerPhoto(photo.Path);
        }

        _settings.ActiveUserName = FirstNonEmpty(_settings.ChartManagerNickname, _settings.ChartManagerName);
        _settings.ActiveUserRole = "Chart Manager";
    }

    private string BuildChartManagerProfileSavedLine()
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(_settings.ChartManagerNickname))
        {
            parts.Add($"nickname {_settings.ChartManagerNickname}");
        }

        if (!string.IsNullOrWhiteSpace(_settings.ChartManagerPhotoPath))
        {
            parts.Add("photo saved");
        }

        if (!string.IsNullOrWhiteSpace(_settings.ChartManagerPersonaStyle))
        {
            parts.Add($"manager style {_settings.ChartManagerPersonaStyle}");
        }

        return parts.Count == 0
            ? "Okay. I skipped the Chart Manager profile details for now."
            : $"Saved Chart Manager profile details: {string.Join(", ", parts)}.";
    }

    private string SaveChartManagerPhoto(string sourcePath)
    {
        var extension = Path.GetExtension(sourcePath);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".jpg";
        }

        var profileFolder = Path.Combine(GetChartManagerFolderPath(), "profile");
        Directory.CreateDirectory(profileFolder);

        var destinationPath = Path.Combine(profileFolder, $"manager_photo{extension.ToLowerInvariant()}");
        if (!string.Equals(
                Path.GetFullPath(sourcePath),
                Path.GetFullPath(destinationPath),
                StringComparison.OrdinalIgnoreCase))
        {
            File.Copy(sourcePath, destinationPath, overwrite: true);
        }

        return destinationPath;
    }

    private string GetChartManagerFolderPath()
    {
        return Path.Combine(
            VaultRootPath,
            "_System",
            "Users",
            BuildSafeFolderName(FirstNonEmpty(_settings.ChartManagerName, "Chart_Manager")));
    }

    private string GetChartManagerPersonaRelativePath()
    {
        return Path.Combine(
                "_System",
                "Users",
                BuildSafeFolderName(FirstNonEmpty(_settings.ChartManagerName, "Chart_Manager")),
                "Manager_Profile.md")
            .Replace('\\', '/');
    }

    private string GetChartManagerPhotoRelativePath()
    {
        if (string.IsNullOrWhiteSpace(_settings.ChartManagerPhotoPath))
        {
            return string.Empty;
        }

        try
        {
            return Path.GetRelativePath(VaultRootPath, _settings.ChartManagerPhotoPath).Replace('\\', '/');
        }
        catch (ArgumentException)
        {
            return _settings.ChartManagerPhotoPath;
        }
    }

    private IReadOnlyList<string> AddSetupPatientNames(string value)
    {
        var names = SplitSetupNames(value);
        var added = new List<string>();

        foreach (var name in names)
        {
            var identity = _patientRegistryService.GetOrCreateIdentityByName(name);
            var chartContext = new ChartContext(identity.ChartId, identity.PatientDisplayName);
            new EntityScaffoldingService().EnsureScaffold(VaultRootPath, chartContext);
            _settings.MostRecentlyCreatedChartId = identity.ChartId;
            added.Add(identity.PatientDisplayName);
        }

        if (added.Count > 0)
        {
            var last = _patientRegistryService.TryResolveIdentityByName(added[^1]);
            if (last is not null)
            {
                ActiveChartId = last.ChartId;
                ActivePatientDisplayName = last.PatientDisplayName;
            }
        }

        return added;
    }

    private static IReadOnlyList<string> SplitSetupNames(string value)
    {
        return value
            .Split([',', ';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(CleanSetupValue)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Where(name => !IsSkipSetupAnswer(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string MergeSetupList(string existing, IEnumerable<string> additions)
    {
        var values = SplitSetupNames(existing)
            .Concat(additions)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return string.Join(", ", values);
    }

    private static string FormatSetupList(string value)
    {
        var names = SplitSetupNames(value);
        return names.Count == 0
            ? "None yet."
            : string.Join(Environment.NewLine, names.Select(name => $"- {name}"));
    }

    private static string NormalizeSetupStep(string value)
    {
        return value switch
        {
            SetupStepManagerPassword or
            SetupStepManagerProfile or
            SetupStepAgentName or
            SetupStepAgentTone or
            SetupStepPatientNames or
            SetupStepAdditionalUsers or
            SetupStepPersonaInterview or
            SetupStepComplete => value,
            _ => SetupStepChartManagerName
        };
    }

    private static string NormalizeAgentTone(string value)
    {
        var normalized = CleanSetupValue(value).ToLowerInvariant();
        if (normalized.Contains("straight", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("direct", StringComparison.OrdinalIgnoreCase))
        {
            return "Straight shooter";
        }

        if (normalized.Contains("coach", StringComparison.OrdinalIgnoreCase))
        {
            return "Coach";
        }

        return "Friendly";
    }

    private static string CleanSetupValue(string value)
    {
        return Regex.Replace(value.Trim(), @"\s+", " ");
    }

    private static bool IsSkipSetupAnswer(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        return normalized is "skip" or "skip for now" or "later" or "no" or "none" or "done" or "not now";
    }

    private static bool IsSkipPatientChartSetupAnswer(string value)
    {
        if (IsSkipSetupAnswer(value))
        {
            return true;
        }

        var normalized = CleanSetupValue(value).ToLowerInvariant();
        return normalized is "keep current charts" or
            "keep current charts as is" or
            "keep charts as is" or
            "no new charts" or
            "use existing charts" or
            "leave charts alone" or
            "leave current charts";
    }

    private static bool IsDefaultDollyAnswer(string value)
    {
        var normalized = CleanSetupValue(value).ToLowerInvariant();
        return normalized is "dolly" or
            "keep dolly" or
            "keep as dolly" or
            "keep the name dolly" or
            "default" or
            "use default" or
            "keep default";
    }

    private static bool LooksLikePhotoOnlyProfileText(string value)
    {
        var normalized = CleanSetupValue(value).ToLowerInvariant();
        return normalized.Contains("photo", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("picture", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("image", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("upload", StringComparison.OrdinalIgnoreCase);
    }

    private static string TryExtractNickname(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var match = Regex.Match(
            value,
            @"\b(?:nickname|nick\s*name|call\s+(?:me|them|her|him)|goes\s+by|aka)\s*(?:is|:|=)?\s+(?<name>[A-Za-z][A-Za-z0-9' -]{0,40})",
            RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            return string.Empty;
        }

        var nickname = CleanSetupValue(match.Groups["name"].Value);
        nickname = Regex.Replace(
            nickname,
            @"\b(?:and|with|for|photo|picture|image|please|thanks).*$",
            string.Empty,
            RegexOptions.IgnoreCase).Trim();
        return nickname;
    }

    private static string TryExtractManagerStyle(string value)
    {
        var normalized = CleanSetupValue(value).ToLowerInvariant();
        if (normalized.Contains("straight", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("direct", StringComparison.OrdinalIgnoreCase))
        {
            return "Straight shooter";
        }

        if (normalized.Contains("coach", StringComparison.OrdinalIgnoreCase))
        {
            return "Coach";
        }

        if (normalized.Contains("friendly", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("warm", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("gentle", StringComparison.OrdinalIgnoreCase))
        {
            return "Friendly";
        }

        return string.Empty;
    }

    private async Task<PatientIntakeResolution> HandleAddNewPatientWorkflowAsync(
        string userText,
        IReadOnlyList<AttachmentViewModel> sentAttachments,
        ChatMessageViewModel thinkingMessage)
    {
        _pendingPatientIntake ??= new PendingPatientIntake();

        if (_pendingPatientIntake.AwaitingIdentityConfirmation)
        {
            if (!_pendingPatientIntake.TryConfirmCandidate(userText))
            {
                if (IsNegative(userText))
                {
                    _pendingPatientIntake = null;
                    Messages.Remove(thinkingMessage);
                    AddArchivedMessage(
                        MessageAuthor.Dolly,
                        AgentLabel,
                        ["Okay, I canceled that new-patient draft. Nothing was created."],
                        mode: "add_patient_intake_canceled");
                    IsThinking = false;
                    RefreshPreview();
                    SendMessageCommand.RaiseCanExecuteChanged();
                    return new PatientIntakeResolution(false, null, userText);
                }

                Messages.Remove(thinkingMessage);
                AddArchivedMessage(
                    MessageAuthor.Dolly,
                    AgentLabel,
                    BuildPatientIdentityQuestionLines(_pendingPatientIntake),
                    sentAttachments,
                    mode: "add_patient_identity_confirm");
                IsThinking = false;
                RefreshPreview();
                SendMessageCommand.RaiseCanExecuteChanged();
                return new PatientIntakeResolution(false, null, userText);
            }
        }
        else if (_pendingPatientIntake.AwaitingCreateConfirmation)
        {
            if (IsChartManagerCreateConfirmation(userText))
            {
                return FinishPatientIntakeChartCreation(userText, thinkingMessage);
            }

            if (IsNegative(userText))
            {
                _pendingPatientIntake = null;
                Messages.Remove(thinkingMessage);
                AddArchivedMessage(
                    MessageAuthor.Dolly,
                    AgentLabel,
                    ["Okay, I canceled that new-patient draft. Nothing was created."],
                    mode: "add_patient_intake_canceled");
                IsThinking = false;
                RefreshPreview();
                SendMessageCommand.RaiseCanExecuteChanged();
                return new PatientIntakeResolution(false, null, userText);
            }

            if (IsAffirmative(userText) || NormalizeConfirmation(userText) is "create chart" or "create the chart")
            {
                Messages.Remove(thinkingMessage);
                AddArchivedMessage(
                    MessageAuthor.Dolly,
                    AgentLabel,
                    BuildChartManagerCreateConfirmationLines(_pendingPatientIntake),
                    mode: "add_patient_manager_confirm");
                IsThinking = false;
                RefreshPreview();
                SendMessageCommand.RaiseCanExecuteChanged();
                return new PatientIntakeResolution(false, null, userText);
            }

            _pendingPatientIntake.MergeCorrections(userText);
            _pendingPatientIntake.AwaitingCreateConfirmation = false;
        }

        _pendingPatientIntake.RememberSourceText(userText);
        _pendingPatientIntake.Merge(userText);
        var localIntakeText = BuildLocalPatientIntakeText(userText, sentAttachments);
        _pendingPatientIntake.MergeCandidateNames(ExtractPatientIdentityCandidates(localIntakeText));

        var missing = _pendingPatientIntake.GetMissingRequiredFields();
        if (missing.Count > 0)
        {
            UpdateDollyStatus(thinkingMessage, "I am reading the new-patient details locally and checking for the missing fields.");
            RefreshPreview();

            var localExtraction = await _patientIntakeExtractionService.ExtractAsync(
                LocalModelEndpoint,
                localIntakeText);

            if (localExtraction.WasAvailable)
            {
                _pendingPatientIntake.Merge(localExtraction);
                _pendingPatientIntake.MergeCandidateNames(localExtraction.CandidateNames);
                missing = _pendingPatientIntake.GetMissingRequiredFields();
                _chronosLedgerService.RecordEvent(
                    VaultRootPath,
                    null,
                    "local_patient_intake_extracted",
                    $"Local intake helper read new-patient demographics with {localExtraction.ModelName}. Missing fields: {(missing.Count == 0 ? "none" : string.Join(", ", missing))}.",
                    "C#",
                    "local_intake");
            }
        }

        if (missing.Count > 0)
        {
            Messages.Remove(thinkingMessage);
            AddArchivedMessage(
                MessageAuthor.Dolly,
                AgentLabel,
                BuildPatientIntakeQuestionLines(_pendingPatientIntake, missing),
                sentAttachments,
                mode: "add_patient_intake");
            IsThinking = false;
            RefreshPreview();
            SendMessageCommand.RaiseCanExecuteChanged();
            return new PatientIntakeResolution(false, null, userText);
        }

        if (_pendingPatientIntake.ShouldAskIdentityConfirmation)
        {
            _pendingPatientIntake.AwaitingIdentityConfirmation = true;
            Messages.Remove(thinkingMessage);
            AddArchivedMessage(
                MessageAuthor.Dolly,
                AgentLabel,
                BuildPatientIdentityQuestionLines(_pendingPatientIntake),
                sentAttachments,
                mode: "add_patient_identity_confirm");
            IsThinking = false;
            RefreshPreview();
            SendMessageCommand.RaiseCanExecuteChanged();
            return new PatientIntakeResolution(false, null, userText);
        }

        _pendingPatientIntake.AwaitingCreateConfirmation = true;
        Messages.Remove(thinkingMessage);
        AddArchivedMessage(
            MessageAuthor.Dolly,
            AgentLabel,
            BuildPatientIntakeReviewLines(_pendingPatientIntake, sentAttachments),
            sentAttachments,
            mode: "add_patient_review");
        IsThinking = false;
        RefreshPreview();
        SendMessageCommand.RaiseCanExecuteChanged();
        return new PatientIntakeResolution(false, null, userText);
    }

    private PatientIntakeResolution FinishPatientIntakeChartCreation(
        string userText,
        ChatMessageViewModel thinkingMessage)
    {
        if (_pendingPatientIntake is null)
        {
            return new PatientIntakeResolution(false, null, userText);
        }

        var identity = _patientRegistryService.GetOrCreateIdentityByName(_pendingPatientIntake.FullName);
        _settings.MostRecentlyCreatedChartId = identity.ChartId;
        identity.DateOfBirth = _pendingPatientIntake.DateOfBirth;
        identity.Address = _pendingPatientIntake.Address;
        identity.PhoneNumber = _pendingPatientIntake.PhoneNumber;
        identity.Email = _pendingPatientIntake.Email;
        identity.RelationshipNotes = _pendingPatientIntake.RelationshipNotes;
        identity.SocialSecurityNumber = _pendingPatientIntake.SocialSecurityNumber;
        identity.PhotoPath = _pendingPatientIntake.PhotoPath;
        _patientRegistryService.SaveIdentityRecord(identity);

        ActiveChartId = identity.ChartId;
        ActivePatientDisplayName = identity.PatientDisplayName;
        VaultWriteStatus = "New patient chart ready";
        UpdateDollyStatus(thinkingMessage, "I confirmed the chart details. I am creating the chart and moving any attached notes through the safe intake path now.");

        var sourceText = string.IsNullOrWhiteSpace(userText)
            ? _pendingPatientIntake.BuildSourceText()
            : _pendingPatientIntake.BuildSourceText();
        var chartContext = new ChartContext(identity.ChartId, identity.PatientDisplayName);
        _pendingPatientIntake = null;
        SelectedOmniboxAction = "Ask / Update Existing Patient";
        return new PatientIntakeResolution(true, chartContext, sourceText);
    }

    private static string BuildLocalPatientIntakeText(
        string userText,
        IReadOnlyList<AttachmentViewModel> sentAttachments)
    {
        var builder = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(userText))
        {
            builder.AppendLine(userText);
        }

        foreach (var attachment in sentAttachments)
        {
            if (string.IsNullOrWhiteSpace(attachment.Path) ||
                !File.Exists(attachment.Path) ||
                !IsPlainTextAttachment(attachment.Path))
            {
                continue;
            }

            try
            {
                builder.AppendLine();
                builder.AppendLine($"Attachment: {Path.GetFileName(attachment.Path)}");
                builder.AppendLine(File.ReadAllText(attachment.Path));
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        return builder.ToString();
    }

    private static bool IsPlainTextAttachment(string path)
    {
        var extension = Path.GetExtension(path);
        return extension.Equals(".txt", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".md", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".csv", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".json", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsProfilePhotoAttachment(AttachmentViewModel attachment)
    {
        var extension = Path.GetExtension(attachment.Path);
        return extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".gif", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".bmp", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".webp", StringComparison.OrdinalIgnoreCase);
    }

    private bool TryApplyChartManagerProfileUpdate(
        string userText,
        IReadOnlyList<AttachmentViewModel> sentAttachments,
        out IReadOnlyList<string> messageLines)
    {
        messageLines = [];
        var hasPhoto = sentAttachments.Any(IsProfilePhotoAttachment);
        var nickname = TryExtractNickname(userText);
        var style = TryExtractManagerStyle(userText);

        if (!LooksLikeChartManagerProfileUpdate(userText, hasPhoto, nickname, style))
        {
            return false;
        }

        ApplyChartManagerProfileDetails(userText, sentAttachments, allowPlainNickname: false);
        SaveSetupProgress();

        _chronosLedgerService.RecordEvent(
            VaultRootPath,
            null,
            "chart_manager_profile_updated",
            $"Chart Manager profile updated for {ResolveChartManagerName()}.",
            AgentDisplayName,
            "manager_profile");
        TrackActionPacketVisibility(
            GetActiveChart(),
            "executed",
            "chart_manager_profile_updated: manager persona details saved outside patient charts.");

        messageLines =
        [
            BuildChartManagerProfileSavedLine(),
            "I saved that under the Chart Manager persona, separate from any patient chart."
        ];
        return true;
    }

    private bool LooksLikeChartManagerProfileUpdate(
        string userText,
        bool hasPhoto,
        string nickname,
        string style)
    {
        var normalized = CleanSetupValue(userText).ToLowerInvariant();
        var mentionsManager = normalized.Contains("chart manager", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("manager", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains(ResolveChartManagerName().ToLowerInvariant(), StringComparison.OrdinalIgnoreCase) ||
            (!string.IsNullOrWhiteSpace(_settings.ChartManagerNickname) &&
                normalized.Contains(_settings.ChartManagerNickname.Trim().ToLowerInvariant(), StringComparison.OrdinalIgnoreCase));

        var profileAction = normalized.Contains("profile", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("photo", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("picture", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("image", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("nickname", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("call me", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("goes by", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("persona", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("tone", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("style", StringComparison.OrdinalIgnoreCase);

        return mentionsManager &&
            (profileAction || hasPhoto || !string.IsNullOrWhiteSpace(nickname) || !string.IsNullOrWhiteSpace(style));
    }

    private bool TryApplyPatientPhotoUpdate(
        IReadOnlyList<AttachmentViewModel> sentAttachments,
        out IReadOnlyList<string> messageLines)
    {
        messageLines = [];
        if (_pendingPhotoUpdate is null)
        {
            return false;
        }

        var photo = sentAttachments.FirstOrDefault(IsProfilePhotoAttachment);
        if (photo is null || !File.Exists(photo.Path))
        {
            return false;
        }

        var identity = _patientRegistryService.GetAllIdentities()
            .FirstOrDefault(patient => patient.ChartId.Equals(_pendingPhotoUpdate.ChartContext.ChartId, StringComparison.OrdinalIgnoreCase));
        if (identity is null)
        {
            messageLines = ["I could not find that chart in the sealed registry anymore, so I left the photo unchanged."];
            return true;
        }

        identity.PhotoPath = SaveProfilePhotoToChart(photo.Path, _pendingPhotoUpdate.ChartContext);
        _patientRegistryService.SaveIdentityRecord(identity);
        OnActivePatientVisualChanged();
        _chronosLedgerService.RecordEvent(
            VaultRootPath,
            _pendingPhotoUpdate.ChartContext,
            "patient_photo_updated",
            $"Chart photo updated for {identity.PatientDisplayName}.",
            AgentDisplayName,
            "profile_photo");
        TrackActionPacketVisibility(
            _pendingPhotoUpdate.ChartContext,
            "executed",
            $"patient_photo_updated: chart photo set for {ExtractFirstNameForDisplay(identity.PatientDisplayName)}.");

        messageLines =
        [
            $"Done. I set {ExtractFirstNameForDisplay(identity.PatientDisplayName)}'s chart photo.",
            "That image is linked as the chart photo; I did not process it as a medical document."
        ];
        return true;
    }

    private string SaveProfilePhotoToChart(string sourcePath, ChartContext chartContext)
    {
        var extension = Path.GetExtension(sourcePath);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".jpg";
        }

        var profileFolder = Path.Combine(VaultRootPath, chartContext.ChartFolderName, "profile");
        Directory.CreateDirectory(profileFolder);

        var destinationPath = Path.Combine(profileFolder, $"chart_photo{extension.ToLowerInvariant()}");
        if (!string.Equals(
                Path.GetFullPath(sourcePath),
                Path.GetFullPath(destinationPath),
                StringComparison.OrdinalIgnoreCase))
        {
            File.Copy(sourcePath, destinationPath, overwrite: true);
        }

        return destinationPath;
    }

    private static string ExtensionFromContentType(string contentType)
    {
        return contentType.Trim().ToLowerInvariant() switch
        {
            "image/png" => ".png",
            "image/gif" => ".gif",
            "image/bmp" => ".bmp",
            "image/webp" => ".webp",
            _ => ".jpg"
        };
    }

    private static string ContentTypeFromExtension(string extension)
    {
        return extension.Trim().ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            ".webp" => "image/webp",
            _ => "image/jpeg"
        };
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best-effort cleanup for temporary mobile chart-photo files.
        }
    }

    private void HandleDeletePatientWorkflow(string userText, ChatMessageViewModel thinkingMessage)
    {
        var normalized = userText.Trim();

        if (TryHandlePendingPatientDeleteResponse(normalized, thinkingMessage))
        {
            return;
        }

        if (IsCleanupChartsRequest(normalized))
        {
            Messages.Remove(thinkingMessage);
            AddArchivedMessage(
                MessageAuthor.Dolly,
                AgentLabel,
                BuildChartCleanupLines(),
                mode: "chart_cleanup_review");
            IsThinking = false;
            RefreshPreview();
            SendMessageCommand.RaiseCanExecuteChanged();
            return;
        }

        var exactConfirmation = _patientRegistryService.GetAllIdentities()
            .FirstOrDefault(patient => normalized.Equals(BuildChartManagerDeletePhrase(patient.PatientDisplayName), StringComparison.OrdinalIgnoreCase));
        if (exactConfirmation is not null)
        {
            var moveResult = MovePatientToWastebasket(exactConfirmation);
            Messages.Remove(thinkingMessage);
            AddArchivedMessage(
                MessageAuthor.Dolly,
                AgentLabel,
                [moveResult],
                mode: "delete_patient_file");
            _pendingPatientDelete = null;
            SelectedOmniboxAction = "Ask / Update Existing Patient";
            IsThinking = false;
            RefreshPreview();
            SendMessageCommand.RaiseCanExecuteChanged();
            return;
        }

        var patientName = ExtractPatientName(normalized);

        if (string.IsNullOrWhiteSpace(patientName))
        {
            patientName = normalized;
        }

        var patient = _patientRegistryService.TryResolveIdentityByName(patientName);

        if (patient is null)
        {
            Messages.Remove(thinkingMessage);
            AddArchivedMessage(
                MessageAuthor.Dolly,
                AgentLabel,
                ["Which patient file should I move to the 7-day wastebasket? Please provide the full patient name."],
                mode: "delete_patient_file");
            IsThinking = false;
            RefreshPreview();
            SendMessageCommand.RaiseCanExecuteChanged();
            return;
        }

        _pendingPatientDelete = new PendingPatientDelete(patient);
        Messages.Remove(thinkingMessage);
        AddArchivedMessage(
            MessageAuthor.Dolly,
            AgentLabel,
            [
                $"{ResolveChartManagerName()} is the chart manager for this vault.",
                $"I found {patient.PatientDisplayName}, DOB: {FormatKnownOrMissing(patient.DateOfBirth)}.",
                $"To move this chart to the 7-day wastebasket, type: {BuildChartManagerPendingDeletePhrase()}"
            ],
            mode: "delete_patient_file");
        IsThinking = false;
        RefreshPreview();
        SendMessageCommand.RaiseCanExecuteChanged();
    }

    private bool TryHandlePendingPatientDeleteResponse(string normalized, ChatMessageViewModel thinkingMessage)
    {
        if (_pendingPatientDelete is null)
        {
            return false;
        }

        if (normalized.Equals(BuildChartManagerPendingDeletePhrase(), StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals(BuildChartManagerDeletePhrase(_pendingPatientDelete.Patient.PatientDisplayName), StringComparison.OrdinalIgnoreCase))
        {
            var moveResult = MovePatientToWastebasket(_pendingPatientDelete.Patient);
            Messages.Remove(thinkingMessage);
            AddArchivedMessage(
                MessageAuthor.Dolly,
                AgentLabel,
                [moveResult],
                mode: "delete_patient_file");
            _pendingPatientDelete = null;
            SelectedOmniboxAction = "Ask / Update Existing Patient";
            IsThinking = false;
            RefreshPreview();
            SendMessageCommand.RaiseCanExecuteChanged();
            return true;
        }

        if (IsNegative(normalized))
        {
            var patientName = _pendingPatientDelete.Patient.PatientDisplayName;
            _pendingPatientDelete = null;
            Messages.Remove(thinkingMessage);
            AddArchivedMessage(
                MessageAuthor.Dolly,
                AgentLabel,
                [$"Okay. I left {patientName} in the active roster."],
                mode: "delete_patient_file_canceled");
            IsThinking = false;
            RefreshPreview();
            SendMessageCommand.RaiseCanExecuteChanged();
            return true;
        }

        if (IsKeepCleanupAsIsRequest(normalized))
        {
            var patientName = _pendingPatientDelete.Patient.PatientDisplayName;
            _pendingPatientDelete = null;
            Messages.Remove(thinkingMessage);
            AddArchivedMessage(
                MessageAuthor.Dolly,
                AgentLabel,
                [$"Okay. I left {patientName} in the active roster."],
                mode: "chart_cleanup_kept");
            IsThinking = false;
            RefreshPreview();
            SendMessageCommand.RaiseCanExecuteChanged();
            return true;
        }

        if (IsCleanupProceedRequest(normalized))
        {
            Messages.Remove(thinkingMessage);
            AddArchivedMessage(
                MessageAuthor.Dolly,
                AgentLabel,
                [
                    $"Okay. I can clean up this chart: {_pendingPatientDelete.Patient.PatientDisplayName}.",
                    "This moves it to the 7-day wastebasket, not permanent deletion.",
                    $"To confirm, type exactly: {BuildChartManagerPendingDeletePhrase()}",
                    "Or type keep as is to leave it alone."
                ],
                mode: "chart_cleanup_confirmation_ready");
            IsThinking = false;
            RefreshPreview();
            SendMessageCommand.RaiseCanExecuteChanged();
            return true;
        }

        if (normalized.StartsWith(ChartManagerPassword, StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("DELETE", StringComparison.OrdinalIgnoreCase))
        {
            Messages.Remove(thinkingMessage);
            AddArchivedMessage(
                MessageAuthor.Dolly,
                AgentLabel,
                [
                    "I did not move anything.",
                    $"For this cleanup candidate, type exactly: {BuildChartManagerPendingDeletePhrase()}",
                    "You can also type no to cancel."
                ],
                mode: "delete_patient_file_waiting");
            IsThinking = false;
            RefreshPreview();
            SendMessageCommand.RaiseCanExecuteChanged();
            return true;
        }

        return false;
    }

    private IReadOnlyList<string> BuildChartCleanupLines()
    {
        var candidates = GetAccidentalChartCandidates();
        _pendingPatientDelete = candidates.Count == 1
            ? new PendingPatientDelete(candidates[0])
            : null;

        if (candidates.Count == 0)
        {
            return
            [
                "I do not see any obvious accidental chart names.",
                "If there is a specific chart to move to the 7-day wastebasket, use Delete Patient File and give the patient name."
            ];
        }

        var lines = new List<string>
        {
            candidates.Count == 1
                ? "I found one chart that looks like setup/test cleanup material."
                : $"I found {candidates.Count} charts that look like setup/test cleanup material."
        };

        var index = 1;
        foreach (var patient in candidates.Take(8))
        {
            lines.Add($"{index}. {patient.PatientDisplayName} ({patient.ChartId})");
            index++;
        }

        lines.Add("Choose one:");
        lines.Add("- Type cleanup to continue with the protected cleanup.");
        lines.Add("- Type keep as is to leave everything unchanged.");
        lines.Add("If you choose cleanup, I will ask for the exact Chart Manager confirmation phrase before anything moves.");
        return lines;
    }

    private static bool IsCleanupProceedRequest(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = NormalizeConfirmation(value);
        return normalized.Equals("cleanup", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("clean up", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("cleanup 1", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("clean up 1", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("yes cleanup", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("go ahead", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("continue", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsKeepCleanupAsIsRequest(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = NormalizeConfirmation(value);
        return normalized.Equals("keep as is", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("keep current charts as is", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("leave as is", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("leave it", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("do not clean up", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("dont clean up", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("cancel cleanup", StringComparison.OrdinalIgnoreCase);
    }

    private bool CanSendMessage()
    {
        if (IsInitialSetupActive)
        {
            return !string.IsNullOrWhiteSpace(PromptText) ||
                   Attachments.Count > 0;
        }

        if (IsThinking)
        {
            return !string.IsNullOrWhiteSpace(PromptText) &&
                   SelectedOmniboxAction == "Ask / Update Existing Patient";
        }

        return !IsThinking &&
               (!string.IsNullOrWhiteSpace(PromptText) ||
                Attachments.Count > 0 ||
               SelectedOmniboxAction is "Add New Patient" or "Delete Patient File");
    }

    private async void HandleConcurrentChatMessage()
    {
        var userText = PromptText.Trim();
        AddArchivedMessage(
            MessageAuthor.User,
            $"User - {DateTime.Now:h:mm tt}",
            [userText],
            mode: "concurrent_chat");
        PromptText = string.Empty;

        var thinkingMessage = new ChatMessageViewModel(
            MessageAuthor.Dolly,
            AgentLabel,
            [$"I am keeping the main task running. Hold a sec, I am asking {AgentDisplayName} from the safe session context."]);
        Messages.Add(thinkingMessage);

        RefreshPreview();
        SendMessageCommand.RaiseCanExecuteChanged();

        try
        {
            var chartContext = GetActiveChart();
            var localSessionContext = BuildLocalSessionContextForGemma(chartContext);
            var packetResult = await AskGemmaForActionPacketAsync(
                userText,
                chartContext,
                localSessionContext);

            if (!packetResult.WasValid || packetResult.Packet is null)
            {
                FinishConcurrentGemmaActionMessage(
                    thinkingMessage,
                    [
                        "I could not map that cleanly while the main task is running.",
                        "The main task is still moving. Try asking again with the patient or task named."
                    ],
                    "concurrent_gemma_action_invalid",
                    chartContext);
                return;
            }

            if (IsBackendOwnedChartAnswer(packetResult.Packet))
            {
                if (IsVaccineListQuestion(userText))
                {
                    FinishConcurrentGemmaActionMessage(
                        thinkingMessage,
                        [
                            .. BuildVaccineAnswerLines(chartContext),
                            "I answered from the vaccine table while the other work keeps running."
                        ],
                        "concurrent_vaccine_table_answer",
                        chartContext);
                    return;
                }

                var answer = await AskGemmaForConcurrentAnswerAsync(
                    chartContext,
                    userText,
                    localSessionContext);

                FinishConcurrentGemmaActionMessage(
                    thinkingMessage,
                    [
                        string.IsNullOrWhiteSpace(answer)
                            ? BuildGemmaUnavailableFallback(chartContext, localSessionContext)
                            : answer,
                        "I answered from Dolly's local chart memory while the API keeps processing the upload. The new document may change this after ingest finishes."
                    ],
                    "concurrent_local_chart_answer",
                    chartContext);
                return;
            }

            await DispatchGemmaActionPacketAsync(
                packetResult.Packet,
                userText,
                thinkingMessage,
                chartContext,
                localSessionContext,
                packetResult.Attempts,
                releaseThinkingWhenDone: false);
        }
        catch (Exception exception)
        {
            var chartContext = GetActiveChart();
            FinishConcurrentGemmaActionMessage(
                thinkingMessage,
                [
                    "I hit a snag answering that side question, but I did not stop the main task.",
                    $"Side-chat status: {HumanizeException(exception)}"
                ],
                "concurrent_gemma_action_error",
                chartContext);
        }
    }

    public async Task<MobileChatResponse> HandleMobileChatAsync(
        MobileChatRequest request,
        MobileTrustedDevice device,
        CancellationToken cancellationToken = default)
    {
        if (!Application.Current.Dispatcher.CheckAccess())
        {
            var operation = Application.Current.Dispatcher.InvokeAsync(
                () => HandleMobileChatAsync(request, device, cancellationToken),
                DispatcherPriority.Normal,
                cancellationToken);
            return await await operation.Task;
        }

        var requestId = $"MOB-{DateTimeOffset.Now:yyyyMMddHHmmss}";
        var userText = string.IsNullOrWhiteSpace(request.Message)
            ? "Mobile chat request."
            : request.Message.Trim();
        var chartContext = ResolveMobileChartContext(request.ChartId);
        var pendingSwitchKey = string.IsNullOrWhiteSpace(device.DeviceId) ? device.DeviceName : device.DeviceId;

        AddArchivedMessage(
            MessageAuthor.User,
            $"Android - {DateTime.Now:h:mm tt}",
            [request.WasVoiceInput ? $"Voice: {userText}" : userText],
            mode: "mobile_chat_request",
            linkedChartId: chartContext.ChartId);

        if (_pendingMobileChartSwitches.TryGetValue(pendingSwitchKey, out var pendingSwitch))
        {
            if (IsYesConfirmation(userText))
            {
                _pendingMobileChartSwitches.Remove(pendingSwitchKey);
                ActiveChartId = pendingSwitch.ChartId;
                ActivePatientDisplayName = pendingSwitch.LocalDisplayName;
                var switchedLines = new[]
                {
                    $"Switched mobile focus to {pendingSwitch.LocalDisplayName}.",
                    "Ask the question again and I will answer under that chart."
                };
                AddArchivedMessage(
                    MessageAuthor.Dolly,
                    AgentLabel,
                    switchedLines,
                    mode: "mobile_chart_switch_confirmed",
                    linkedChartId: pendingSwitch.ChartId);
                return BuildMobileChatResponse(requestId, "answered", "desktop_mobile_chart_switch", switchedLines, pendingSwitch);
            }

            if (IsNoConfirmation(userText))
            {
                _pendingMobileChartSwitches.Remove(pendingSwitchKey);
                var cancelLines = new[]
                {
                    $"Okay, I stayed on {chartContext.LocalDisplayName}.",
                    "No chart focus changed."
                };
                AddArchivedMessage(
                    MessageAuthor.Dolly,
                    AgentLabel,
                    cancelLines,
                    mode: "mobile_chart_switch_cancelled",
                    linkedChartId: chartContext.ChartId);
                return BuildMobileChatResponse(requestId, "answered", "desktop_mobile_chart_switch_cancelled", cancelLines, chartContext);
            }
        }

        var mentionedChart = ResolveMentionedMobileChart(userText);
        if (mentionedChart is not null &&
            !mentionedChart.ChartId.Equals(chartContext.ChartId, StringComparison.OrdinalIgnoreCase))
        {
            _pendingMobileChartSwitches[pendingSwitchKey] = mentionedChart;
            var switchLines = new[]
            {
                $"You asked about {mentionedChart.LocalDisplayName}, but mobile focus is currently {chartContext.LocalDisplayName}.",
                $"Reply yes to switch this mobile conversation to {mentionedChart.LocalDisplayName}, or no to stay on {chartContext.LocalDisplayName}."
            };
            AddArchivedMessage(
                MessageAuthor.Dolly,
                AgentLabel,
                switchLines,
                mode: "mobile_chart_switch_prompt",
                linkedChartId: chartContext.ChartId);
            return BuildMobileChatResponse(requestId, "needs_confirmation", "desktop_mobile_chart_switch_prompt", switchLines, chartContext);
        }

        if (mentionedChart is not null &&
            mentionedChart.ChartId.Equals(chartContext.ChartId, StringComparison.OrdinalIgnoreCase) &&
            (IsMobileChartFocusRequest(userText) || IsMobileCurrentChartQuestion(userText)))
        {
            var alreadyFocusedLines = new[]
            {
                $"Yes. Mobile focus is {chartContext.LocalDisplayName}.",
                $"Chart ID: {chartContext.ChartId}."
            };
            AddArchivedMessage(
                MessageAuthor.Dolly,
                AgentLabel,
                alreadyFocusedLines,
                mode: "mobile_current_chart_answer",
                linkedChartId: chartContext.ChartId);
            return BuildMobileChatResponse(requestId, "answered", "desktop_csharp_current_chart", alreadyFocusedLines, chartContext);
        }

        if (IsMobileCurrentChartQuestion(userText))
        {
            var currentChartLines = new[]
            {
                $"Mobile focus is {chartContext.LocalDisplayName}.",
                $"Chart ID: {chartContext.ChartId}."
            };
            AddArchivedMessage(
                MessageAuthor.Dolly,
                AgentLabel,
                currentChartLines,
                mode: "mobile_current_chart_answer",
                linkedChartId: chartContext.ChartId);
            return BuildMobileChatResponse(requestId, "answered", "desktop_csharp_current_chart", currentChartLines, chartContext);
        }

        var unknownRegistryName = DetectUnknownMobileRegistryName(userText);
        if (!string.IsNullOrWhiteSpace(unknownRegistryName))
        {
            var unknownNameLines = new[]
            {
                $"{unknownRegistryName} is not a patient chart in the sealed registry.",
                $"Mobile focus is still {chartContext.LocalDisplayName} ({chartContext.ChartId}). I will not treat that name as a chart unless it is added to the registry."
            };
            AddArchivedMessage(
                MessageAuthor.Dolly,
                AgentLabel,
                unknownNameLines,
                mode: "mobile_unknown_registry_name",
                linkedChartId: chartContext.ChartId);
            return BuildMobileChatResponse(requestId, "answered", "desktop_csharp_registry_guard", unknownNameLines, chartContext);
        }

        if (TryResolveMobilePacketRequest(userText, out var mobilePacketType, out var mobilePacketMode))
        {
            var packet = await HandleMobileChartPacketAsync(
                new MobileChartPacketRequest
                {
                    ChartId = chartContext.ChartId,
                    PacketType = mobilePacketType,
                    Mode = mobilePacketMode
                },
                device,
                cancellationToken);
            var packetLines = packet.Status.Equals("ready", StringComparison.OrdinalIgnoreCase)
                ? new[]
                {
                    $"I sent {packet.Title} for {packet.PatientDisplayName} to the chart packets on your phone.",
                    "Open Charts, choose that person, and tap the packet button to view or refresh it."
                }
                : new[] { packet.Message };
            AddArchivedMessage(
                MessageAuthor.Dolly,
                AgentLabel,
                packetLines,
                mode: "mobile_packet_sent",
                linkedChartId: chartContext.ChartId);
            return BuildMobileChatResponse(requestId, packet.Status, "desktop_csharp_mobile_packet", packetLines, chartContext, packet);
        }

        if (IsDataHunterBasicStartRequest(userText))
        {
            _pendingDataHunterBasicChartId = chartContext.ChartId;
            _pendingDataHunterExpansionChartId = string.Empty;
            _pendingDataHunterExpansionKey = string.Empty;
            _dataHunterQuestionsAnsweredThisSession = 0;
            _dataHunterContinuousInterviewEnabled = false;
            _dataHunterPausedForSession = false;
            var questionLines = BuildDataHunterQuestStartLines(chartContext);
            AddArchivedMessage(
                MessageAuthor.Dolly,
                AgentLabel,
                questionLines,
                mode: "mobile_data_hunter_basic_question",
                linkedChartId: chartContext.ChartId);
            return BuildMobileChatResponse(requestId, "answered", "desktop_csharp_data_hunter_basic", questionLines, chartContext);
        }

        if (_pendingDataHunterBasicChartId.Equals(chartContext.ChartId, StringComparison.OrdinalIgnoreCase) &&
            TryHandleDataHunterSessionControl(chartContext, userText, out var controlLines))
        {
            AddArchivedMessage(
                MessageAuthor.Dolly,
                AgentLabel,
                controlLines,
                mode: "mobile_data_hunter_session_control",
                linkedChartId: chartContext.ChartId);
            return BuildMobileChatResponse(requestId, "answered", "desktop_csharp_data_hunter_basic", controlLines, chartContext);
        }

        if (_pendingDataHunterExpansionChartId.Equals(chartContext.ChartId, StringComparison.OrdinalIgnoreCase) &&
            TryCaptureDataHunterExpansionAnswer(chartContext, userText, "mobile_chat", out var expansionLines))
        {
            AddArchivedMessage(
                MessageAuthor.Dolly,
                AgentLabel,
                expansionLines,
                mode: "mobile_data_hunter_expansion_answer_saved",
                linkedChartId: chartContext.ChartId);
            return BuildMobileChatResponse(requestId, "answered", "desktop_csharp_data_hunter_basic", expansionLines, chartContext);
        }

        if (_pendingDataHunterBasicChartId.Equals(chartContext.ChartId, StringComparison.OrdinalIgnoreCase) &&
            TryCaptureDataHunterBasicAnswer(chartContext, userText, "mobile_chat", out var dataHunterLines))
        {
            if (GetNextDataHunterBasicQuestionIndex(LoadDataHunterPersonalDataMap(chartContext)) is null)
            {
                _pendingDataHunterBasicChartId = string.Empty;
            }

            AddArchivedMessage(
                MessageAuthor.Dolly,
                AgentLabel,
                dataHunterLines,
                mode: "mobile_data_hunter_basic_answer_saved",
                linkedChartId: chartContext.ChartId);
            return BuildMobileChatResponse(requestId, "answered", "desktop_csharp_data_hunter_basic", dataHunterLines, chartContext);
        }

        if (IsThinking)
        {
            var busyLines = new[]
            {
                "VitaMR desktop is busy with another chart task right now.",
                "I received the mobile message but did not start a second chart action. Try again after the current task finishes."
            };
            AddArchivedMessage(
                MessageAuthor.Dolly,
                AgentLabel,
                busyLines,
                mode: "mobile_chat_busy",
                linkedChartId: chartContext.ChartId);
            return BuildMobileChatResponse(requestId, "busy", "desktop_busy", busyLines, chartContext);
        }

        IsThinking = true;
        var thinkingMessage = new ChatMessageViewModel(
            MessageAuthor.Dolly,
            AgentLabel,
            [$"Android message received from {device.DeviceName}. I am answering from the desktop vault."]);
        Messages.Add(thinkingMessage);
        RefreshPreview();
        SendMessageCommand.RaiseCanExecuteChanged();

        try
        {
            IReadOnlyList<string> responseLines;
            var route = "desktop_local_context";

            if (IsDreamRunnerRequest(userText))
            {
                VaultWriteStatus = "Running Dream Runner audit";
                UpdateDollyStatus(thinkingMessage, "Android asked for Dream Runner. I am running the report-only audit now.");
                RefreshPreview();
                await Task.Yield();

                var taskId = StartDollyTask(chartContext, "Dream Runner audit", "checking required sterile wiki files");
                var auditResult = _dreamRunnerAuditService.AuditChart(VaultRootPath, chartContext);
                _lastDreamRunnerAuditResult = auditResult;
                CompleteDollyTask(chartContext, taskId, auditResult.Status, $"Open gaps: {auditResult.OpenCareGaps}; conflicts: {auditResult.ActiveConflicts}.");
                responseLines = BuildDreamRunnerAuditMessageLines(auditResult).ToList();
                route = "desktop_csharp_dream_runner";
            }
            else if (IsRosterQuestionText(userText))
            {
                responseLines = BuildRosterDisplayLines().ToList();
                route = "desktop_csharp_roster";
            }
            else if (IsTaskBoardRequest(userText))
            {
                _lastDollyTaskBoardResult = _dollyTaskBoardService.BuildStatus(VaultRootPath, chartContext);
                responseLines = BuildDollyTaskBoardMessageLines(_lastDollyTaskBoardResult).ToList();
                route = "desktop_csharp_task_board";
            }
            else if (IsVaccineListQuestion(userText))
            {
                responseLines = BuildVaccineAnswerLines(chartContext);
                route = "desktop_csharp_vaccine_table";
            }
            else
            {
                var localSessionContext = BuildLocalSessionContextForGemma(chartContext);
                if (string.IsNullOrWhiteSpace(localSessionContext))
                {
                    responseLines =
                    [
                        "I received the mobile message, but this chart does not have a local sterile context packet ready yet.",
                        "Open VitaMR on desktop and let Dolly refresh the chart context, then try again."
                    ];
                    route = "desktop_context_unavailable";
                }
                else
                {
                    var answer = await AskGemmaForConcurrentAnswerAsync(chartContext, userText, localSessionContext);
                    responseLines =
                    [
                        string.IsNullOrWhiteSpace(answer)
                            ? BuildGemmaUnavailableFallback(chartContext, localSessionContext)
                            : answer
                    ];
                    route = "desktop_local_dolly_chat";
                }
            }

            FinishGemmaActionMessage(
                thinkingMessage,
                responseLines,
                "mobile_chat_answer",
                chartContext);
            return BuildMobileChatResponse(requestId, "answered", route, responseLines, chartContext);
        }
        catch (Exception exception)
        {
            var errorLines = new[]
            {
                "I received the mobile message, but the desktop bridge hit a snag before answering.",
                $"Mobile bridge status: {HumanizeException(exception)}"
            };
            FinishGemmaActionMessage(
                thinkingMessage,
                errorLines,
                "mobile_chat_error",
                chartContext);
            return BuildMobileChatResponse(requestId, "error", "desktop_mobile_bridge_error", errorLines, chartContext);
        }
    }

    public async Task<MobileChatResponse> HandleMobilePersonalChatAsync(
        MobilePersonalChatRequest request,
        MobileTrustedDevice device,
        CancellationToken cancellationToken = default)
    {
        if (!Application.Current.Dispatcher.CheckAccess())
        {
            var operation = Application.Current.Dispatcher.InvokeAsync(
                () => HandleMobilePersonalChatAsync(request, device, cancellationToken),
                DispatcherPriority.Normal,
                cancellationToken);
            return await await operation.Task;
        }

        var requestId = $"PERS-{DateTimeOffset.Now:yyyyMMddHHmmss}";
        var note = string.IsNullOrWhiteSpace(request.Message) ? "(empty personal item)" : request.Message.Trim();
        var createdAt = string.IsNullOrWhiteSpace(request.CreatedAt) ? DateTimeOffset.Now.ToString("O") : request.CreatedAt.Trim();
        var captureId = string.IsNullOrWhiteSpace(request.LocalId) ? Guid.NewGuid().ToString("N") : request.LocalId.Trim();
        var capture = SavePersonalVaultCapture(
            captureId,
            "personal",
            "personal_text",
            note,
            createdAt,
            device.DeviceId,
            device.DeviceName);
        RouteYouTubeProjectCapture(capture);

        if (TryBuildYouTubeVideoExport(note, out var exportLines))
        {
            AddArchivedMessage(
                MessageAuthor.Dolly,
                AgentLabel,
                exportLines,
                mode: "mobile_personal_youtube_export",
                linkedChartId: string.Empty);

            RefreshPersonalVault();

            return new MobileChatResponse
            {
                RequestId = requestId,
                Status = "answered",
                Route = "desktop_personal_youtube_export",
                Reply = string.Join(Environment.NewLine + Environment.NewLine, exportLines),
                Lines = exportLines.ToList(),
                ActiveChartId = string.Empty,
                ActivePatientDisplayName = "Personal"
            };
        }

        var sensitiveWarning = LooksSensitiveForPersonalCloud(note)
            ? "This may be sensitive. I saved it in Personal Mode, but use Local Lockbox for sensitive medical, financial, legal, password, identity, or deeply private notes."
            : string.Empty;
        var responseText = await _geminiSmallTalkService.AnswerPersonalAsync(
            SelectedAiProvider.Equals("Gemini", StringComparison.OrdinalIgnoreCase),
            GeminiFastModelName,
            ScrubPersonalModeTextForCloud(note),
            capture.TopicHint,
            capture.CaptureType,
            cancellationToken);

        var lines = string.IsNullOrWhiteSpace(sensitiveWarning)
            ? new[] { responseText }
            : new[] { sensitiveWarning, responseText };
        AddArchivedMessage(
            MessageAuthor.Dolly,
            AgentLabel,
            lines,
            mode: "mobile_personal_gemini_answer",
            linkedChartId: string.Empty);

        RefreshPersonalVault();

        return new MobileChatResponse
        {
            RequestId = requestId,
            Status = "answered",
            Route = "desktop_personal_gemini_fast",
            Reply = string.Join(Environment.NewLine + Environment.NewLine, lines),
            Lines = lines.ToList(),
            ActiveChartId = string.Empty,
            ActivePatientDisplayName = "Personal"
        };
    }

    private ChartContext ResolveMobileChartContext(string chartId)
    {
        if (!string.IsNullOrWhiteSpace(chartId))
        {
            var normalizedChartId = NormalizeChartId(chartId);
            var identity = _patientRegistryService.GetAllIdentities()
                .FirstOrDefault(patient => patient.ChartId.Equals(normalizedChartId, StringComparison.OrdinalIgnoreCase));
            if (identity is not null)
            {
                return new ChartContext(identity.ChartId, identity.PatientDisplayName);
            }
        }

        return GetActiveChart();
    }

    public Task<MobileChartPacketResponse> HandleMobileChartPacketAsync(
        MobileChartPacketRequest request,
        MobileTrustedDevice device,
        CancellationToken cancellationToken = default)
    {
        if (!Application.Current.Dispatcher.CheckAccess())
        {
            var operation = Application.Current.Dispatcher.InvokeAsync(
                () => HandleMobileChartPacketAsync(request, device, cancellationToken),
                DispatcherPriority.Normal,
                cancellationToken);
            return operation.Task.Unwrap();
        }

        var chartContext = ResolveMobileChartContext(request.ChartId);
        var packetType = NormalizeMobilePacketType(request.PacketType);
        var mode = string.IsNullOrWhiteSpace(request.Mode) ? "current" : request.Mode.Trim();

        var lines = packetType.ToLowerInvariant() switch
        {
            "notes" => BuildMobileNotesPacketLines(chartContext, mode),
            "vaccines" => BuildVaccineAnswerLines(chartContext),
            "labs" => BuildMobileSpecialtyPacketLines(chartContext, "Labs_Master", "latest labs", "No lab packet is available yet."),
            "imaging" => BuildMobileSpecialtyPacketLines(chartContext, "Imaging_Radiology", "recent imaging readings", "No imaging packet is available yet."),
            "questions" => BuildMobileQuestionsPacketLines(chartContext),
            "other" => BuildMobileOtherPacketLines(chartContext, mode),
            _ => [$"{ToMobilePacketTitle(packetType)} packets are not wired yet."]
        };
        var packet = new MobileChartPacketResponse
        {
            Status = "ready",
            ChartId = chartContext.ChartId,
            PatientDisplayName = chartContext.LocalDisplayName,
            PacketType = packetType,
            Mode = mode,
            CreatedAt = DateTimeOffset.Now,
            Title = ToMobilePacketTitle(packetType, mode),
            Body = string.Join(Environment.NewLine, lines),
            Message = $"{ToMobilePacketTitle(packetType, mode)} is ready for {chartContext.LocalDisplayName}."
        };

        _chronosLedgerService.RecordEvent(
            VaultRootPath,
            chartContext,
            "mobile_packet_built",
            $"Mobile packet built: {packet.PacketType} ({packet.Mode}).",
            device.DeviceName,
            "mobile");

        AddMobilePacketConversationTrail(chartContext, packet, device);
        RefreshPreview();

        return Task.FromResult(packet);
    }

    public Task<MobileChartPhotoResponse> HandleMobileChartPhotoAsync(
        MobileChartPhotoRequest request,
        MobileTrustedDevice device,
        CancellationToken cancellationToken = default)
    {
        if (!Application.Current.Dispatcher.CheckAccess())
        {
            var operation = Application.Current.Dispatcher.InvokeAsync(
                () => HandleMobileChartPhotoAsync(request, device, cancellationToken),
                DispatcherPriority.Normal,
                cancellationToken);
            return operation.Task.Unwrap();
        }

        var chartContext = ResolveMobileChartContext(request.ChartId);
        var identity = _patientRegistryService.GetAllIdentities()
            .FirstOrDefault(patient => patient.ChartId.Equals(chartContext.ChartId, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(request.Base64Data))
        {
            if (identity is null)
            {
                return Task.FromResult(new MobileChartPhotoResponse
                {
                    Status = "not_found",
                    ChartId = chartContext.ChartId,
                    PatientDisplayName = chartContext.LocalDisplayName,
                    Message = "VitaMR could not find that chart in the sealed registry, so the photo was not saved."
                });
            }

            try
            {
                var tempPath = Path.Combine(
                    Path.GetTempPath(),
                    $"vitamr-chart-photo-{Guid.NewGuid():N}{ExtensionFromContentType(request.ContentType)}");
                File.WriteAllBytes(tempPath, Convert.FromBase64String(request.Base64Data));
                try
                {
                    identity.PhotoPath = SaveProfilePhotoToChart(tempPath, chartContext);
                    _patientRegistryService.SaveIdentityRecord(identity);
                    OnActivePatientVisualChanged();
                    _chronosLedgerService.RecordEvent(
                        VaultRootPath,
                        chartContext,
                        "patient_photo_updated_from_mobile",
                        $"Chart photo updated from {device.DeviceName}.",
                        device.DeviceName,
                        "mobile_profile_photo");
                }
                finally
                {
                    TryDeleteFile(tempPath);
                }
            }
            catch (Exception exception) when (exception is FormatException or IOException or UnauthorizedAccessException)
            {
                return Task.FromResult(new MobileChartPhotoResponse
                {
                    Status = "error",
                    ChartId = chartContext.ChartId,
                    PatientDisplayName = chartContext.LocalDisplayName,
                    Message = $"VitaMR could not save that chart photo: {exception.Message}"
                });
            }
        }

        var photoPath = identity is not null &&
                        !string.IsNullOrWhiteSpace(identity.PhotoPath) &&
                        File.Exists(identity.PhotoPath)
            ? identity.PhotoPath
            : string.Empty;

        if (string.IsNullOrWhiteSpace(photoPath))
        {
            return Task.FromResult(new MobileChartPhotoResponse
            {
                Status = "no_photo",
                ChartId = chartContext.ChartId,
                PatientDisplayName = chartContext.LocalDisplayName,
                HasPhoto = false,
                Message = "No chart photo is saved yet."
            });
        }

        return Task.FromResult(new MobileChartPhotoResponse
        {
            Status = "ready",
            ChartId = chartContext.ChartId,
            PatientDisplayName = chartContext.LocalDisplayName,
            HasPhoto = true,
            ContentType = ContentTypeFromExtension(Path.GetExtension(photoPath)),
            Base64Data = Convert.ToBase64String(File.ReadAllBytes(photoPath)),
            Message = "Chart photo ready."
        });
    }

    private void AddMobilePacketConversationTrail(
        ChartContext chartContext,
        MobileChartPacketResponse packet,
        MobileTrustedDevice device)
    {
        var actionLine = packet.PacketType.Equals("other", StringComparison.OrdinalIgnoreCase)
            ? $"Phone requested {packet.Title} for {chartContext.LocalDisplayName}."
            : $"Phone synced {packet.Title} for {chartContext.LocalDisplayName}.";
        var detailLine = string.IsNullOrWhiteSpace(packet.Mode) || packet.Mode.Equals("current", StringComparison.OrdinalIgnoreCase)
            ? "Mode: current"
            : $"Mode: {packet.Mode}";
        var bodyLineCount = packet.Body
            .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Length;

        AddArchivedMessage(
            MessageAuthor.User,
            $"Android - {DateTime.Now:h:mm tt}",
            [
                actionLine,
                detailLine,
                $"Device: {device.DeviceName}"
            ],
            mode: "mobile_packet_request",
            linkedChartId: chartContext.ChartId);

        AddArchivedMessage(
            MessageAuthor.Dolly,
            AgentLabel,
            [
                $"I prepared {packet.Title} for the phone.",
                $"Saved a read-only travel record with {bodyLineCount} line{(bodyLineCount == 1 ? string.Empty : "s")} of sterile chart text.",
                "No raw source files were changed."
            ],
            mode: "mobile_packet_ready",
            linkedChartId: chartContext.ChartId);
    }

    public Task<MobileVitaMasteryResponse> HandleMobileVitaMasteryAsync(
        MobileVitaMasteryRequest request,
        MobileTrustedDevice device,
        CancellationToken cancellationToken = default)
    {
        if (!Application.Current.Dispatcher.CheckAccess())
        {
            var operation = Application.Current.Dispatcher.InvokeAsync(
                () => HandleMobileVitaMasteryAsync(request, device, cancellationToken),
                DispatcherPriority.Normal,
                cancellationToken);
            return operation.Task.Unwrap();
        }

        var chartContext = ResolveMobileChartContext(request.ChartId);
        var identity = _patientRegistryService.GetAllIdentities()
            .FirstOrDefault(patient => patient.ChartId.Equals(chartContext.ChartId, StringComparison.OrdinalIgnoreCase));
        var result = _vitaMasteryService.Evaluate(VaultRootPath, chartContext, identity);
        if (chartContext.ChartId.Equals(ActiveChartId, StringComparison.OrdinalIgnoreCase))
        {
            _lastVitaMasteryResult = result;
            OnPropertyChanged(nameof(VitaMasterySummary));
            OnPropertyChanged(nameof(VitaMasteryPercent));
            OnPropertyChanged(nameof(VitaMasteryMicroQuestSummary));
            OnPropertyChanged(nameof(VitaMasteryPointsSummary));
            OnPropertyChanged(nameof(DataHunterBasicPercent));
            OnPropertyChanged(nameof(DataHunterProgressSummary));
            OnPropertyChanged(nameof(DataHunterStageSummary));
            OnPropertyChanged(nameof(DataHunterPointsSummary));
            OnPropertyChanged(nameof(DataHunterNextQuestion));
            OnPropertyChanged(nameof(DataHunterFirstMasterTargetSummary));
            OnPropertyChanged(nameof(DataHunterFirstMasterTargetDetail));
            OnPropertyChanged(nameof(DataHunterMasterEvidenceSummary));
        }

        var dataHunterMap = LoadDataHunterPersonalDataMap(chartContext);
        var dataHunterMasterQuests = GenerateDataHunterMasterQuests(dataHunterMap);
        var dataHunterXpTotal = CalculateDataHunterBasicXp(dataHunterMap);
        var nextBasicQuestionIndex = GetNextDataHunterBasicQuestionIndex(dataHunterMap);
        var dataHunterStage = nextBasicQuestionIndex is null
            ? "Master"
            : "Basic";
        var nextMasterQuest = dataHunterMasterQuests.FirstOrDefault();
        var acceptedMasterQuestCount = dataHunterMasterQuests.Count(IsDataHunterMasterQuestAccepted);
        var pendingMasterQuestCount = dataHunterMasterQuests.Count - acceptedMasterQuestCount;
        var dataHunterQuestion = dataHunterStage.Equals("Master", StringComparison.OrdinalIgnoreCase)
            ? dataHunterMasterQuests.FirstOrDefault()?.Title ?? "Basic interview complete. Add accepted records to advance Data Master."
            : GetDataHunterNextQuestion(chartContext);
        var dataHunterQuestStatus = dataHunterStage.Equals("Master", StringComparison.OrdinalIgnoreCase)
            ? nextMasterQuest?.Status ?? DataHunterMasterQuestStatusPendingEvidence
            : FirstNonEmpty(
                nextMasterQuest is null ? string.Empty : $"Master queued: {nextMasterQuest.Title}",
                GetDataHunterNextQuestStatus(chartContext));

        return Task.FromResult(new MobileVitaMasteryResponse
        {
            Status = "ready",
            ChartId = chartContext.ChartId,
            PatientDisplayName = chartContext.LocalDisplayName,
            PercentComplete = result.PercentComplete,
            CurrentStage = VitaMasteryResult.FormatStage(result.CurrentStage),
            EarnedPoints = result.EarnedPoints,
            PossiblePoints = result.PossiblePoints,
            SummaryLine = result.SummaryLine,
            DataHunterTitle = DataHunterTitle,
            DataHunterStage = dataHunterStage,
            DataHunterPercent = CalculateDataHunterBasicPercent(chartContext),
            DataHunterQuestion = dataHunterQuestion,
            DataHunterQuestCategory = GetDataHunterNextQuestCategory(chartContext),
            DataHunterQuestStatus = dataHunterQuestStatus,
            DataHunterQuestXp = GetDataHunterNextQuestXp(chartContext),
            DataHunterXpTotal = dataHunterXpTotal,
            DataHunterMasterTarget = nextMasterQuest?.Title ?? string.Empty,
            DataHunterMasterTargetDetail = nextMasterQuest is null
                ? "Basic clues can create Master Hunt evidence targets."
                : $"{BuildDataHunterQuestClueLine(nextMasterQuest)}. {FirstNonEmpty(nextMasterQuest.AcceptedEvidenceSummary, nextMasterQuest.EvidenceHint)}",
            DataHunterMasterAcceptedCount = acceptedMasterQuestCount,
            DataHunterMasterPendingCount = pendingMasterQuestCount,
            DataHunterQuestControlPrompt = "You can answer, say later, not sure, does not apply, pause, or keep going.",
            DataHunterPrompt = dataHunterStage.Equals("Master", StringComparison.OrdinalIgnoreCase)
                ? "Data Master quests are gathering targets from your Personal Data Map. They do not award points until evidence is accepted into the vault."
                : nextMasterQuest is null
                    ? "Answer this Data Hunter question in chat. Dolly will use it to build your personal record map."
                    : $"Answer this question when ready. A Data Master target is already queued: {nextMasterQuest.Title}.",
            ActiveMicroQuests = result.ActiveMicroQuests
                .Select(quest => new MobileVitaMasteryQuestResponse
                {
                    QuestId = quest.Quest.QuestId,
                    Title = quest.Quest.Title,
                    Stage = VitaMasteryResult.FormatStage(quest.Quest.Stage),
                    Status = quest.Status.ToString(),
                    EvidenceHint = quest.Quest.EvidenceHint,
                    Points = quest.Quest.Points
                })
                .Concat(dataHunterMasterQuests.Select(quest => new MobileVitaMasteryQuestResponse
                {
                    QuestId = quest.QuestId,
                    Title = quest.Title,
                    Stage = "Master",
                    Status = quest.Status,
                    EvidenceHint = quest.EvidenceHint,
                    Points = 0
                }))
                .ToList()
        });
    }

    public Task<MobileChatResponse> HandleMobileDataHunterQuestAsync(
        MobileDataHunterQuestRequest request,
        MobileTrustedDevice device,
        CancellationToken cancellationToken = default)
    {
        if (!Application.Current.Dispatcher.CheckAccess())
        {
            var operation = Application.Current.Dispatcher.InvokeAsync(
                () => HandleMobileDataHunterQuestAsync(request, device, cancellationToken),
                DispatcherPriority.Normal,
                cancellationToken);
            return operation.Task.Unwrap();
        }

        var requestId = $"DHQ-{DateTimeOffset.Now:yyyyMMddHHmmss}";
        var chartContext = ResolveMobileChartContext(request.ChartId);
        _pendingDataHunterBasicChartId = chartContext.ChartId;
        _pendingDataHunterExpansionChartId = string.Empty;
        _pendingDataHunterExpansionKey = string.Empty;
        _dataHunterQuestionsAnsweredThisSession = 0;
        _dataHunterContinuousInterviewEnabled = false;
        _dataHunterPausedForSession = false;

        var lines = BuildDataHunterQuestStartLines(chartContext);
        AddArchivedMessage(
            MessageAuthor.Dolly,
            AgentLabel,
            lines,
            mode: "mobile_data_hunter_quest_started",
            linkedChartId: chartContext.ChartId);

        _chronosLedgerService.RecordEvent(
            VaultRootPath,
            chartContext,
            "mobile_data_hunter_quest_started",
            $"Phone requested Data Hunter next quest from {device.DeviceName}.",
            device.DeviceName,
            "data_hunter");

        RefreshDataHunterProperties();
        RefreshPreview();

        return Task.FromResult(BuildMobileChatResponse(
            requestId,
            "answered",
            "desktop_csharp_data_hunter_quest",
            lines,
            chartContext));
    }

    public Task<MobileOfflineItemResponse> HandleMobileOfflineItemAsync(
        MobileOfflineItemRequest request,
        MobileTrustedDevice device,
        CancellationToken cancellationToken = default)
    {
        if (!Application.Current.Dispatcher.CheckAccess())
        {
            var operation = Application.Current.Dispatcher.InvokeAsync(
                () => HandleMobileOfflineItemAsync(request, device, cancellationToken),
                DispatcherPriority.Normal,
                cancellationToken);
            return operation.Task.Unwrap();
        }

        var chartContext = ResolveMobileChartContext(request.ChartId);
        var kind = string.IsNullOrWhiteSpace(request.Kind) ? "text" : request.Kind.Trim();
        var note = string.IsNullOrWhiteSpace(request.Note) ? "(empty saved phone item)" : request.Note.Trim();
        var createdAt = string.IsNullOrWhiteSpace(request.CreatedAt) ? "unknown time" : request.CreatedAt.Trim();
        var contextQuestion = "Should I save only, review, summarize, or add this to the chart after confirmation?";
        PhoneInboxItems.Insert(0, new PhoneInboxItemViewModel(
            request.LocalId,
            createdAt,
            chartContext.ChartId,
            chartContext.LocalDisplayName,
            kind,
            note,
            string.IsNullOrWhiteSpace(request.FileName) ? string.Empty : request.FileName.Trim(),
            PhoneInboxWaitingStatus,
            string.Empty));
        var userLines = new[]
        {
            $"Synced saved phone {kind} for {chartContext.LocalDisplayName}.",
            $"Saved on phone: {createdAt}.",
            note
        };
        var dollyLines = new[]
        {
            $"I received a saved phone {kind} for {chartContext.LocalDisplayName}.",
            contextQuestion
        };

        AddArchivedMessage(
            MessageAuthor.User,
            $"Android saved item - {DateTime.Now:h:mm tt}",
            userLines,
            mode: "mobile_offline_item_received",
            linkedChartId: chartContext.ChartId);
        AddArchivedMessage(
            MessageAuthor.Dolly,
            AgentLabel,
            dollyLines,
            mode: "mobile_offline_item_context_prompt",
            linkedChartId: chartContext.ChartId);

        _chronosLedgerService.RecordEvent(
            VaultRootPath,
            chartContext,
            "mobile_offline_item_received",
            $"Saved phone {kind} received from {device.DeviceName}: {TruncateForChronos(note, 120)}",
            device.DeviceName,
            "mobile");

        RefreshPreview();

        return Task.FromResult(new MobileOfflineItemResponse
        {
            Status = "received",
            Message = $"Saved phone {kind} received for {chartContext.LocalDisplayName}.",
            NeedsContext = true,
            ContextQuestion = contextQuestion
        });
    }

    public Task<MobilePersonalItemResponse> HandleMobilePersonalItemAsync(
        MobilePersonalItemRequest request,
        MobileTrustedDevice device,
        CancellationToken cancellationToken = default)
    {
        if (!Application.Current.Dispatcher.CheckAccess())
        {
            var operation = Application.Current.Dispatcher.InvokeAsync(
                () => HandleMobilePersonalItemAsync(request, device, cancellationToken),
                DispatcherPriority.Normal,
                cancellationToken);
            return operation.Task.Unwrap();
        }

        var kind = string.IsNullOrWhiteSpace(request.Kind) ? "personal_text" : request.Kind.Trim();
        var note = string.IsNullOrWhiteSpace(request.Note) ? "(empty personal item)" : request.Note.Trim();
        var createdAt = string.IsNullOrWhiteSpace(request.CreatedAt) ? DateTimeOffset.Now.ToString("O") : request.CreatedAt.Trim();
        var lane = kind.Equals("pet_note", StringComparison.OrdinalIgnoreCase)
            ? "pets"
            : kind.Equals("lockbox_note", StringComparison.OrdinalIgnoreCase)
                ? "lockbox"
                : "personal";
        var folder = GetPersonalVaultFolder();
        Directory.CreateDirectory(folder);

        var captureId = string.IsNullOrWhiteSpace(request.LocalId) ? Guid.NewGuid().ToString("N") : request.LocalId.Trim();
        var capture = SavePersonalVaultCapture(
            captureId,
            lane,
            kind,
            note,
            createdAt,
            device.DeviceId,
            device.DeviceName);
        RouteYouTubeProjectCapture(capture);

        var message = lane switch
        {
            "pets" => "Saved pet note to the desktop Personal Vault. It remains animal/pet context, not a human medical chart.",
            "lockbox" => "Saved Lockbox note to the trusted desktop. It was not sent to Gemini or medical chart evidence.",
            _ when IsPersonalSessionKind(kind) => "Saved phone Personal session snapshot to the desktop Personal Vault. It is sync history, not medical chart evidence.",
            _ => "Saved personal note to the desktop Personal Vault. It remains separate from medical records."
        };

        AddArchivedMessage(
            MessageAuthor.Dolly,
            AgentLabel,
            [message, TruncateForChronos(note, 180)],
            mode: lane switch
            {
                "pets" => "mobile_pet_note_saved",
                "lockbox" => "mobile_lockbox_note_saved",
                _ when IsPersonalSessionKind(kind) => "mobile_personal_session_saved",
                _ => "mobile_personal_note_saved"
            },
            linkedChartId: string.Empty);

        RefreshPreview();
        RefreshPersonalVault();
        if (GetUnwovenPersonalVaultCaptures().Count >= PersonalVaultWeaveThreshold)
        {
            WeavePersonalVault();
        }
        else
        {
            RefreshPersonalMemorySummary();
        }

        return Task.FromResult(new MobilePersonalItemResponse
        {
            Status = lane switch
            {
                "pets" => "pet_vault_synced",
                "lockbox" => "lockbox_vault_synced",
                _ => "personal_vault_synced"
            },
            Message = message,
            StoredAs = lane
        });
    }

    private PersonalVaultCapture SavePersonalVaultCapture(
        string captureId,
        string lane,
        string kind,
        string note,
        string createdAt,
        string deviceId,
        string deviceName)
    {
        var folder = GetPersonalVaultFolder();
        Directory.CreateDirectory(folder);
        var normalizedLane = NormalizePersonalVaultLane(lane);
        var capture = BuildPersonalVaultCapture(
            captureId,
            normalizedLane,
            kind,
            note,
            createdAt,
            DateTimeOffset.Now,
            deviceId,
            deviceName);
        var entry = JsonSerializer.Serialize(capture, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        File.AppendAllLines(Path.Combine(folder, $"{normalizedLane}-notes.jsonl"), [entry]);
        if (!normalizedLane.Equals("lockbox", StringComparison.OrdinalIgnoreCase))
        {
            File.AppendAllLines(Path.Combine(folder, PersonalVaultTaggedCaptureFileName), [entry]);
        }

        return capture;
    }

    private void RouteYouTubeProjectCapture(PersonalVaultCapture capture)
    {
        if (!ShouldRouteToYouTubeProject(capture))
        {
            EnsureYouTubeProjectFramework();
            return;
        }

        var projectFolder = EnsureYouTubeProjectFramework();
        var normalized = NormalizeConfirmation(capture.Note);
        if (LooksLikeFutureVideoIdea(normalized))
        {
            AppendMarkdownCapture(
                Path.Combine(projectFolder, "Future_Video_Ideas.md"),
                "Future Video Ideas",
                capture);
            return;
        }

        var video = GetOrCreateCurrentYouTubeVideo(capture);
        var videoFolder = GetYouTubeVideoFolder(projectFolder, video);
        Directory.CreateDirectory(videoFolder);
        EnsureYouTubeVideoFiles(videoFolder, video);

        AppendMarkdownCapture(Path.Combine(videoFolder, "Raw_Captures.md"), "Raw Captures", capture);
        AppendMarkdownCapture(Path.Combine(videoFolder, ResolveYouTubeVideoSectionFile(normalized)), ResolveYouTubeVideoSectionTitle(normalized), capture);
    }

    private static bool ShouldRouteToYouTubeProject(PersonalVaultCapture capture)
    {
        if (!NormalizePersonalVaultLane(capture.Lane).Equals("personal", StringComparison.OrdinalIgnoreCase) ||
            IsPersonalSessionKind(capture.Kind))
        {
            return false;
        }

        var normalized = NormalizeConfirmation(capture.Note);
        return ContainsAny(
            normalized,
            "youtube",
            "video",
            "thumbnail",
            "title",
            "hook",
            "intro",
            "micro transformation",
            "microtransformation",
            "micro story",
            "setup",
            "progress",
            "payoff",
            "early detection",
            "longevity",
            "healthspan",
            "vitamr");
    }

    private static bool LooksLikeFutureVideoIdea(string normalized)
    {
        return ContainsAny(normalized, "next video", "future video", "another video", "second video", "later video");
    }

    private string EnsureYouTubeProjectFramework()
    {
        var projectFolder = GetYouTubeProjectFolder();
        var videosFolder = Path.Combine(projectFolder, "videos");
        Directory.CreateDirectory(videosFolder);
        WriteIfMissing(
            Path.Combine(projectFolder, "Project_Index.md"),
            BuildYouTubeProjectIndexMarkdown());
        WriteIfMissing(
            Path.Combine(projectFolder, "Strategy.md"),
            BuildYouTubeProjectStrategyMarkdown());
        WriteIfMissing(
            Path.Combine(projectFolder, "Future_Video_Ideas.md"),
            "# Future Video Ideas" + Environment.NewLine + Environment.NewLine);
        return projectFolder;
    }

    private YouTubeVideoContext GetOrCreateCurrentYouTubeVideo(PersonalVaultCapture capture)
    {
        var projectFolder = EnsureYouTubeProjectFramework();
        var currentPath = Path.Combine(projectFolder, YouTubeCurrentVideoFileName);
        var current = ReadYouTubeVideoContext(currentPath);
        if (current is not null)
        {
            current.LastTouchedAt = DateTimeOffset.Now.ToString("O");
            File.WriteAllText(currentPath, JsonSerializer.Serialize(current, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
            return current;
        }

        var title = InferYouTubeWorkingTitle(capture.Note);
        var video = new YouTubeVideoContext
        {
            VideoId = NextYouTubeVideoId(projectFolder),
            Slug = SlugifyPersonalVaultTopic(title),
            WorkingTitle = title,
            Status = "active",
            CreatedAt = DateTimeOffset.Now.ToString("O"),
            LastTouchedAt = DateTimeOffset.Now.ToString("O"),
            ParentProject = YouTubeProjectSlug,
            MicroTransformation = "From prevention-only or occasional checkup thinking to continuous early detection as a practical longevity practice."
        };
        File.WriteAllText(currentPath, JsonSerializer.Serialize(video, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
        return video;
    }

    private static YouTubeVideoContext? ReadYouTubeVideoContext(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        return RunCatching(() => JsonSerializer.Deserialize<YouTubeVideoContext>(File.ReadAllText(path), new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }

    private static T? RunCatching<T>(Func<T> action)
    {
        try
        {
            return action();
        }
        catch
        {
            return default;
        }
    }

    private static string InferYouTubeWorkingTitle(string note)
    {
        var explicitTopic = ExtractExplicitPersonalVaultTopic(note);
        if (!string.IsNullOrWhiteSpace(explicitTopic))
        {
            return explicitTopic;
        }

        return "Early Detection Changed How I Think About Longevity";
    }

    private static string NextYouTubeVideoId(string projectFolder)
    {
        var year = DateTimeOffset.Now.Year;
        var videosFolder = Path.Combine(projectFolder, "videos");
        Directory.CreateDirectory(videosFolder);
        var max = Directory.GetDirectories(videosFolder, $"YTV-{year}-*")
            .Select(path => Regex.Match(Path.GetFileName(path), $@"YTV-{year}-(?<n>\d{{4}})"))
            .Where(match => match.Success)
            .Select(match => int.TryParse(match.Groups["n"].Value, out var n) ? n : 0)
            .DefaultIfEmpty(0)
            .Max();
        return $"YTV-{year}-{max + 1:0000}";
    }

    private string GetYouTubeProjectFolder()
    {
        return Path.Combine(GetPersonalVaultFolder(), "projects", YouTubeProjectSlug);
    }

    private static string GetYouTubeVideoFolder(string projectFolder, YouTubeVideoContext video)
    {
        return Path.Combine(projectFolder, "videos", $"{video.VideoId}_{video.Slug}");
    }

    private static void EnsureYouTubeVideoFiles(string videoFolder, YouTubeVideoContext video)
    {
        WriteIfMissing(Path.Combine(videoFolder, "Video_Brief.md"), BuildYouTubeVideoBriefMarkdown(video));
        WriteIfMissing(Path.Combine(videoFolder, "Micro_Transformation.md"), $"# Micro Transformation{Environment.NewLine}{Environment.NewLine}{video.MicroTransformation}{Environment.NewLine}");
        WriteIfMissing(Path.Combine(videoFolder, "Topic_Title_Ideas.md"), "# Topic / Title Ideas" + Environment.NewLine + Environment.NewLine);
        WriteIfMissing(Path.Combine(videoFolder, "Thumbnail_Ideas.md"), "# Thumbnail Ideas" + Environment.NewLine + Environment.NewLine);
        WriteIfMissing(Path.Combine(videoFolder, "Intro_Hook_Ideas.md"), "# Intro / Hook Ideas" + Environment.NewLine + Environment.NewLine);
        WriteIfMissing(Path.Combine(videoFolder, "Body_Micro_Stories.md"), "# Body Micro-Stories" + Environment.NewLine + Environment.NewLine + "Use Setup -> Progress -> Payoff for each body idea." + Environment.NewLine);
        WriteIfMissing(Path.Combine(videoFolder, "Evidence_To_Check.md"), "# Evidence To Check" + Environment.NewLine + Environment.NewLine);
        WriteIfMissing(Path.Combine(videoFolder, "VitaMR_Positioning.md"), "# VitaMR Positioning" + Environment.NewLine + Environment.NewLine);
        WriteIfMissing(Path.Combine(videoFolder, "Raw_Captures.md"), "# Raw Captures" + Environment.NewLine + Environment.NewLine);
    }

    private static string ResolveYouTubeVideoSectionFile(string normalized)
    {
        if (ContainsAny(normalized, "title", "topic", "proven"))
        {
            return "Topic_Title_Ideas.md";
        }

        if (ContainsAny(normalized, "thumbnail", "visual", "image"))
        {
            return "Thumbnail_Ideas.md";
        }

        if (ContainsAny(normalized, "intro", "hook", "opening", "first 15 seconds"))
        {
            return "Intro_Hook_Ideas.md";
        }

        if (ContainsAny(normalized, "setup", "progress", "payoff", "micro story", "body", "core idea"))
        {
            return "Body_Micro_Stories.md";
        }

        if (ContainsAny(normalized, "evidence", "research", "study", "source", "prove", "check"))
        {
            return "Evidence_To_Check.md";
        }

        if (ContainsAny(normalized, "vitamr", "dolly", "software", "tool", "agentic"))
        {
            return "VitaMR_Positioning.md";
        }

        if (ContainsAny(normalized, "micro transformation", "microtransformation", "transformation"))
        {
            return "Micro_Transformation.md";
        }

        return "Video_Brief.md";
    }

    private static string ResolveYouTubeVideoSectionTitle(string normalized)
    {
        return Path.GetFileNameWithoutExtension(ResolveYouTubeVideoSectionFile(normalized)).Replace('_', ' ');
    }

    private static void AppendMarkdownCapture(string path, string title, PersonalVaultCapture capture)
    {
        WriteIfMissing(path, $"# {title}{Environment.NewLine}{Environment.NewLine}");
        File.AppendAllText(
            path,
            $"{Environment.NewLine}## {FormatPersonalVaultDate(capture.CreatedAt)} - {capture.CaptureType.Replace('_', ' ')}{Environment.NewLine}" +
            $"- Capture: `{capture.CaptureId}`{Environment.NewLine}" +
            $"- Source: {FirstNonEmpty(capture.DeviceName, "Phone companion")}{Environment.NewLine}" +
            $"{Environment.NewLine}{capture.Note.Trim()}{Environment.NewLine}");
    }

    private bool TryBuildYouTubeVideoExport(string note, out string[] lines)
    {
        lines = [];
        var normalized = NormalizeConfirmation(note);
        if (!ContainsAny(normalized, "export context", "gpt context", "all notes", "give me notes", "video notes", "title ideas", "thumbnail notes", "hook notes", "intro notes", "body micro"))
        {
            return false;
        }

        var projectFolder = EnsureYouTubeProjectFramework();
        var current = ReadYouTubeVideoContext(Path.Combine(projectFolder, YouTubeCurrentVideoFileName));
        if (current is null)
        {
            lines = ["I do not have an active YouTube video yet. Start by telling me the video idea or micro-transformation, and I will create the active video context."];
            return true;
        }

        var videoFolder = GetYouTubeVideoFolder(projectFolder, current);
        EnsureYouTubeVideoFiles(videoFolder, current);
        var export = BuildYouTubeVideoContextPack(videoFolder, current, normalized);
        lines = [export];
        return true;
    }

    private static string BuildYouTubeVideoContextPack(string videoFolder, YouTubeVideoContext video, string normalizedRequest)
    {
        var requestedFiles = ResolveYouTubeExportFiles(normalizedRequest).ToList();
        var builder = new StringBuilder();
        builder.AppendLine($"# GPT Context Pack: {video.VideoId}");
        builder.AppendLine();
        builder.AppendLine($"Working title: {video.WorkingTitle}");
        builder.AppendLine($"Parent project: {video.ParentProject}");
        builder.AppendLine();
        builder.AppendLine("## Main Transformation");
        builder.AppendLine("Increasing life and healthspan.");
        builder.AppendLine();
        builder.AppendLine("## Focus");
        builder.AppendLine("Early detection as a practical longevity lever. VitaMR/Dolly is the primary tool being shared; YouTube is the awareness channel.");
        builder.AppendLine();

        foreach (var fileName in requestedFiles)
        {
            var path = Path.Combine(videoFolder, fileName);
            if (!File.Exists(path))
            {
                continue;
            }

            builder.AppendLine();
            builder.AppendLine(File.ReadAllText(path).Trim());
            builder.AppendLine();
        }

        return builder.ToString().Trim();
    }

    private static IEnumerable<string> ResolveYouTubeExportFiles(string normalizedRequest)
    {
        if (ContainsAny(normalizedRequest, "title ideas", "title notes", "topic ideas"))
        {
            return ["Video_Brief.md", "Micro_Transformation.md", "Topic_Title_Ideas.md"];
        }

        if (ContainsAny(normalizedRequest, "thumbnail"))
        {
            return ["Video_Brief.md", "Micro_Transformation.md", "Thumbnail_Ideas.md"];
        }

        if (ContainsAny(normalizedRequest, "intro", "hook"))
        {
            return ["Video_Brief.md", "Micro_Transformation.md", "Intro_Hook_Ideas.md"];
        }

        if (ContainsAny(normalizedRequest, "body", "micro story", "setup", "progress", "payoff"))
        {
            return ["Video_Brief.md", "Micro_Transformation.md", "Body_Micro_Stories.md"];
        }

        return
        [
            "Video_Brief.md",
            "Micro_Transformation.md",
            "Topic_Title_Ideas.md",
            "Thumbnail_Ideas.md",
            "Intro_Hook_Ideas.md",
            "Body_Micro_Stories.md",
            "Evidence_To_Check.md",
            "VitaMR_Positioning.md",
            "Raw_Captures.md"
        ];
    }

    private static void WriteIfMissing(string path, string contents)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        if (!File.Exists(path))
        {
            File.WriteAllText(path, contents);
        }
    }

    private static string FormatPersonalVaultDate(string value)
    {
        return DateTimeOffset.TryParse(value, out var parsed)
            ? parsed.LocalDateTime.ToString("yyyy-MM-dd HH:mm")
            : FirstNonEmpty(value, "unknown time");
    }

    private static string BuildYouTubeProjectIndexMarkdown()
    {
        return """
            # LT Transformation - Early Detection

            ## Overall Transformational Goal

            This optional Personal Vault project template helps a user organize a longevity-focused transformation arc around early detection, health-data visibility, and readiness. It is intentionally generic in the public repo and should be customized locally with synthetic or personal notes only after the user trusts their own environment.

            Current champion topic: early detection as a practical longevity lever.

            Primary tool: VitaMR/Dolly.

            Primary channel: YouTube, to increase awareness of early detection for longevity and introduce VitaMR to interested builders and users.

            Each video is a micro-transformation that should support the larger transformation arc.
            """;
    }

    private static string BuildYouTubeProjectStrategyMarkdown()
    {
        return """
            # Strategy

            ## Overall Transformational Goal
            Use early detection and health-data visibility as a longevity readiness theme. Public builds should treat this as an editable template, not as a claim about any real person.

            ## Focus
            Champion early detection as an underused longevity lever.

            ## Tool
            VitaMR/Dolly is an open-source, agent-first medical record prototype for organizing records and user-controlled context.

            ## Main Quest
            Explore how a user might organize educational content around early detection, health-data visibility, and longevity readiness.

            ## Video Structure
            Each video should define a micro-transformation, then develop topic/title ideas, thumbnail ideas, intro/hook ideas, and body ideas using micro-stories with Setup, Progress, and Payoff.
            """;
    }

    private static string BuildYouTubeVideoBriefMarkdown(YouTubeVideoContext video)
    {
        return $"""
            # Video Brief - {video.VideoId}

            Working title: {video.WorkingTitle}

            Status: {video.Status}

            ## Micro-Transformation
            {video.MicroTransformation}

            ## Relation To Larger Arc
            This video should advance the larger transformation: increasing life and healthspan through better early detection, with VitaMR/Dolly as the tool being shared.
            """;
    }

    private void RefreshPersonalVault()
    {
        var searchText = PersonalVaultSearchText.Trim();
        var allRows = LoadPersonalVaultRows().ToList();
        RefreshPersonalVaultTopicFilters(allRows);
        var selectedTopic = SelectedPersonalVaultTopic;
        var rows = allRows
            .Where(row => string.IsNullOrWhiteSpace(searchText) ||
                          row.Lane.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                          row.Kind.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                          row.TopicHint.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                          row.CaptureType.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                          row.Note.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                          row.DeviceName.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                          row.CreatedAt.Contains(searchText, StringComparison.OrdinalIgnoreCase))
            .Where(row => selectedTopic.Equals("All Topics", StringComparison.OrdinalIgnoreCase) ||
                          row.TopicHint.Equals(selectedTopic, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(row => ParsePhoneInboxDate(row.CreatedAt))
            .Take(75)
            .ToList();

        PersonalVaultItems.Clear();
        foreach (var row in rows)
        {
            PersonalVaultItems.Add(row);
        }

        var totalCount = allRows.Count;
        var unwovenCount = GetUnwovenPersonalVaultCaptures().Count;
        var topicSuffix = selectedTopic.Equals("All Topics", StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : $" | topic: {selectedTopic}";
        var weaveSuffix = $" | {unwovenCount} unwoven | auto-organize at {PersonalVaultWeaveThreshold}";
        PersonalVaultStatus = totalCount == 0
            ? "No synced Personal, Lockbox, or Pets notes yet."
            : string.IsNullOrWhiteSpace(searchText) && selectedTopic.Equals("All Topics", StringComparison.OrdinalIgnoreCase)
                ? $"{totalCount} synced Personal Vault note(s). Lockbox notes are excluded from cloud AI and wiki weaving.{weaveSuffix}"
                : $"{PersonalVaultItems.Count} match(es) out of {totalCount} Personal Vault note(s).{topicSuffix}{weaveSuffix}";

        RefreshPreview();
    }

    private void RefreshPersonalVaultTopicFilters(IReadOnlyList<PersonalVaultItemViewModel> rows)
    {
        var topics = rows
            .Select(row => row.TopicHint)
            .Where(topic => !string.IsNullOrWhiteSpace(topic))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(topic => topic, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var selected = SelectedPersonalVaultTopic;
        PersonalVaultTopicFilters.Clear();
        PersonalVaultTopicFilters.Add("All Topics");
        foreach (var topic in topics)
        {
            PersonalVaultTopicFilters.Add(topic);
        }

        if (!selected.Equals("All Topics", StringComparison.OrdinalIgnoreCase) &&
            !topics.Contains(selected, StringComparer.OrdinalIgnoreCase))
        {
            _selectedPersonalVaultTopic = "All Topics";
            OnPropertyChanged(nameof(SelectedPersonalVaultTopic));
        }
    }

    private IEnumerable<PersonalVaultItemViewModel> LoadPersonalVaultRows()
    {
        var folder = GetPersonalVaultFolder();
        foreach (var lane in new[] { "personal", "lockbox", "pets" })
        {
            var path = Path.Combine(folder, $"{lane}-notes.jsonl");
            foreach (var document in ReadJsonLines(path))
            {
                var root = document.RootElement;
                var note = ReadJsonString(root, "note");
                if (string.IsNullOrWhiteSpace(note))
                {
                    continue;
                }

                yield return new PersonalVaultItemViewModel(
                    lane,
                    ReadJsonString(root, "kind"),
                    note,
                    FirstNonEmpty(ReadJsonString(root, "topicHint"), InferPersonalVaultTopic(lane, ReadJsonString(root, "kind"), note).TopicHint),
                    FirstNonEmpty(ReadJsonString(root, "captureType"), InferPersonalVaultCaptureType(lane, ReadJsonString(root, "kind"), note)),
                    FirstNonEmpty(ReadJsonString(root, "createdAt"), ReadJsonString(root, "receivedAt")),
                    ReadJsonString(root, "receivedAt"),
                    ReadJsonString(root, "deviceName"));
            }
        }
    }

    private void WeavePersonalVault()
    {
        var captures = LoadPersonalVaultCaptures()
            .Where(ShouldIncludeInPersonalVaultDerivedPages)
            .ToList();
        if (captures.Count == 0)
        {
            PersonalVaultStatus = "No Personal Vault captures to organize yet.";
            return;
        }

        var unwoven = GetUnwovenPersonalVaultCaptures(captures);
        var batchId = $"PVW-{DateTimeOffset.Now:yyyyMMddHHmmss}";
        var folder = GetPersonalVaultFolder();
        var wikiFolder = Path.Combine(folder, "wiki");
        var timelineFolder = Path.Combine(folder, "timeline");
        Directory.CreateDirectory(wikiFolder);
        Directory.CreateDirectory(timelineFolder);

        var groups = captures
            .GroupBy(capture => string.IsNullOrWhiteSpace(capture.TopicSlug) ? SlugifyPersonalVaultTopic(capture.TopicHint) : capture.TopicSlug, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var group in groups)
        {
            var topicCaptures = group
                .OrderBy(capture => ParsePhoneInboxDate(capture.CreatedAt))
                .ToList();
            var title = FirstNonEmpty(topicCaptures.FirstOrDefault()?.TopicHint ?? string.Empty, HumanizePersonalVaultSlug(group.Key));
            File.WriteAllText(
                Path.Combine(wikiFolder, $"{group.Key}.md"),
                BuildPersonalVaultTopicMarkdown(title, topicCaptures));
        }

        File.WriteAllText(
            Path.Combine(wikiFolder, "Personal_Index.md"),
            BuildPersonalVaultIndexMarkdown(groups));

        File.WriteAllText(
            Path.Combine(wikiFolder, PersonalVaultMemorySummaryFileName),
            BuildPersonalMemorySummaryMarkdown(captures));

        var monthlyGroups = captures
            .GroupBy(capture => ToPersonalVaultMonthKey(capture.CreatedAt), StringComparer.OrdinalIgnoreCase)
            .Where(group => !string.IsNullOrWhiteSpace(group.Key))
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();
        foreach (var group in monthlyGroups)
        {
            File.WriteAllText(
                Path.Combine(timelineFolder, $"{group.Key}.md"),
                BuildPersonalVaultMonthMarkdown(group.Key, group
                    .OrderBy(capture => ParsePhoneInboxDate(capture.CreatedAt))
                    .ToList()));
        }

        var yearlyGroups = captures
            .GroupBy(capture => ToPersonalVaultYearKey(capture.CreatedAt), StringComparer.OrdinalIgnoreCase)
            .Where(group => !string.IsNullOrWhiteSpace(group.Key))
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();
        foreach (var group in yearlyGroups)
        {
            File.WriteAllText(
                Path.Combine(timelineFolder, $"{group.Key}.md"),
                BuildPersonalVaultYearMarkdown(group.Key, group
                    .OrderBy(capture => ParsePhoneInboxDate(capture.CreatedAt))
                    .ToList()));
        }

        File.WriteAllText(
            Path.Combine(timelineFolder, "Personal_Timeline_Index.md"),
            BuildPersonalVaultTimelineIndexMarkdown(yearlyGroups, monthlyGroups));

        var batchRecord = JsonSerializer.Serialize(new
        {
            batchId,
            createdAt = DateTimeOffset.Now,
            captureCount = captures.Count,
            newlyWovenCount = unwoven.Count,
            topics = groups.Select(group => group.Key).ToList(),
            years = yearlyGroups.Select(group => group.Key).ToList(),
            months = monthlyGroups.Select(group => group.Key).ToList(),
            captureIds = unwoven.Select(capture => capture.CaptureId).ToList()
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        File.AppendAllLines(Path.Combine(folder, PersonalVaultBatchFileName), [batchRecord]);

        PersonalVaultStatus = $"Organized {captures.Count} Personal Vault capture(s) into {groups.Count} topic page(s), {yearlyGroups.Count} year page(s), and {monthlyGroups.Count} month page(s).";
        RefreshPersonalVault();
    }

    public Task<MobilePersonalSummaryResponse> HandleMobilePersonalSummaryAsync(
        MobileTrustedDevice device,
        CancellationToken cancellationToken = default)
    {
        if (!Application.Current.Dispatcher.CheckAccess())
        {
            var operation = Application.Current.Dispatcher.InvokeAsync(
                () => HandleMobilePersonalSummaryAsync(device, cancellationToken),
                DispatcherPriority.Normal,
                cancellationToken);
            return operation.Task.Unwrap();
        }

        var summary = RefreshPersonalMemorySummary();
        return Task.FromResult(new MobilePersonalSummaryResponse
        {
            Status = string.IsNullOrWhiteSpace(summary) ? "empty" : "ready",
            UpdatedAt = DateTimeOffset.Now.ToString("O"),
            Summary = summary,
            Message = string.IsNullOrWhiteSpace(summary)
                ? "No Personal memory summary is available yet."
                : "Personal memory summary ready for phone Personal Mode."
        });
    }

    private string RefreshPersonalMemorySummary()
    {
        var captures = LoadPersonalVaultCaptures()
            .Where(ShouldIncludeInPersonalVaultDerivedPages)
            .ToList();
        var summary = BuildPersonalMemorySummaryMarkdown(captures);
        var wikiFolder = Path.Combine(GetPersonalVaultFolder(), "wiki");
        Directory.CreateDirectory(wikiFolder);
        File.WriteAllText(Path.Combine(wikiFolder, PersonalVaultMemorySummaryFileName), summary);
        return summary;
    }

    private IReadOnlyList<PersonalVaultCapture> GetUnwovenPersonalVaultCaptures(IReadOnlyList<PersonalVaultCapture>? captures = null)
    {
        captures ??= LoadPersonalVaultCaptures().ToList();
        var processedIds = LoadProcessedPersonalVaultCaptureIds();
        return captures
            .Where(ShouldIncludeInPersonalVaultDerivedPages)
            .Where(capture => !processedIds.Contains(capture.CaptureId))
            .ToList();
    }

    private static bool ShouldIncludeInPersonalVaultDerivedPages(PersonalVaultCapture capture)
    {
        return !NormalizePersonalVaultLane(capture.Lane).Equals("lockbox", StringComparison.OrdinalIgnoreCase) &&
            !IsPersonalSessionKind(capture.Kind) &&
            !capture.CaptureType.Equals("phone_session", StringComparison.OrdinalIgnoreCase);
    }

    private IReadOnlyList<PersonalVaultCapture> LoadPersonalVaultCaptures()
    {
        var folder = GetPersonalVaultFolder();
        var taggedPath = Path.Combine(folder, PersonalVaultTaggedCaptureFileName);
        var captures = ReadPersonalVaultCaptureFile(taggedPath).ToList();
        if (captures.Count > 0)
        {
            return captures;
        }

        return new[] { "personal", "pets" }
            .SelectMany(lane => ReadPersonalVaultCaptureFile(Path.Combine(folder, $"{lane}-notes.jsonl"), lane))
            .ToList();
    }

    private IEnumerable<PersonalVaultCapture> ReadPersonalVaultCaptureFile(string path, string fallbackLane = "personal")
    {
        foreach (var document in ReadJsonLines(path))
        {
            var root = document.RootElement;
            var note = ReadJsonString(root, "note");
            if (string.IsNullOrWhiteSpace(note))
            {
                continue;
            }

            var lane = FirstNonEmpty(ReadJsonString(root, "lane"), fallbackLane);
            var kind = FirstNonEmpty(ReadJsonString(root, "kind"), lane.Equals("pets", StringComparison.OrdinalIgnoreCase) ? "pet_note" : "personal_text");
            var topic = InferPersonalVaultTopic(lane, kind, note);
            yield return new PersonalVaultCapture(
                FirstNonEmpty(ReadJsonString(root, "captureId"), ReadJsonString(root, "localId"), Guid.NewGuid().ToString("N")),
                NormalizePersonalVaultLane(lane),
                kind,
                note,
                FirstNonEmpty(ReadJsonString(root, "topicHint"), topic.TopicHint),
                FirstNonEmpty(ReadJsonString(root, "topicSlug"), topic.TopicSlug),
                FirstNonEmpty(ReadJsonString(root, "captureType"), InferPersonalVaultCaptureType(lane, kind, note)),
                FirstNonEmpty(ReadJsonString(root, "createdAt"), ReadJsonString(root, "receivedAt")),
                ReadJsonString(root, "receivedAt"),
                ReadJsonString(root, "deviceId"),
                ReadJsonString(root, "deviceName"));
        }
    }

    private HashSet<string> LoadProcessedPersonalVaultCaptureIds()
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var document in ReadJsonLines(Path.Combine(GetPersonalVaultFolder(), PersonalVaultBatchFileName)))
        {
            if (!document.RootElement.TryGetProperty("captureIds", out var captureIds) ||
                captureIds.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var captureId in captureIds.EnumerateArray())
            {
                var value = captureId.ToString().Trim();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    ids.Add(value);
                }
            }
        }

        return ids;
    }

    private static PersonalVaultCapture BuildPersonalVaultCapture(
        string captureId,
        string lane,
        string kind,
        string note,
        string createdAt,
        DateTimeOffset receivedAt,
        string deviceId,
        string deviceName)
    {
        var normalizedLane = NormalizePersonalVaultLane(lane);
        var topic = InferPersonalVaultTopic(normalizedLane, kind, note);
        return new PersonalVaultCapture(
            captureId,
            normalizedLane,
            kind,
            note,
            topic.TopicHint,
            topic.TopicSlug,
            InferPersonalVaultCaptureType(normalizedLane, kind, note),
            createdAt,
            receivedAt.ToString("O"),
            deviceId,
            deviceName);
    }

    private static PersonalVaultTopic InferPersonalVaultTopic(string lane, string kind, string note)
    {
        if (NormalizePersonalVaultLane(lane).Equals("pets", StringComparison.OrdinalIgnoreCase))
        {
            return new PersonalVaultTopic("Pets", "Pets");
        }

        if (NormalizePersonalVaultLane(lane).Equals("lockbox", StringComparison.OrdinalIgnoreCase))
        {
            return new PersonalVaultTopic("Lockbox", "Lockbox");
        }

        if (IsPersonalSessionKind(kind))
        {
            return new PersonalVaultTopic("Phone Sessions", "Phone_Sessions");
        }

        var normalized = NormalizeConfirmation(note);
        var explicitTopic = ExtractExplicitPersonalVaultTopic(note);
        if (!string.IsNullOrWhiteSpace(explicitTopic))
        {
            return new PersonalVaultTopic(explicitTopic, SlugifyPersonalVaultTopic(explicitTopic));
        }

        if (ContainsAny(normalized, "dolly project", "vitamr", "codex", "personal vault", "data hunter"))
        {
            return new PersonalVaultTopic("Dolly Project", "Dolly_Project");
        }

        if (ContainsAny(normalized, "shopping", "grocery", "groceries", "buy ", "pick up", "store", "order "))
        {
            return new PersonalVaultTopic("Shopping List", "Shopping_List");
        }

        if (ContainsAny(normalized, "youtube", "video idea", "channel", "thumbnail", "script", "shorts"))
        {
            return new PersonalVaultTopic("YouTube Ideas", "YouTube_Ideas");
        }

        if (ContainsAny(normalized, "home", "house", "garage", "clean", "repair", "laundry"))
        {
            return new PersonalVaultTopic("Home", "Home");
        }

        if (ContainsAny(normalized, "plan", "goal", "future", "schedule", "trip", "budget"))
        {
            return new PersonalVaultTopic("Life Planning", "Life_Planning");
        }

        return new PersonalVaultTopic("Inbox", "Inbox");
    }

    private static string ExtractExplicitPersonalVaultTopic(string note)
    {
        var match = Regex.Match(
            note,
            @"\b(?:for|about|on|under|to)\s+(?:my\s+)?(?<topic>[A-Za-z][A-Za-z0-9 '&-]{2,40}?)(?:\s+(?:project|list|ideas?|notes?))?(?:[.?!,:;]|$)",
            RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            return string.Empty;
        }

        var topic = Regex.Replace(match.Groups["topic"].Value, @"\s+", " ").Trim();
        if (topic.Length < 3 ||
            ContainsAny(NormalizeConfirmation(topic), "later", "desktop", "phone", "medical mode", "personal mode", "pets mode"))
        {
            return string.Empty;
        }

        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(topic.ToLowerInvariant());
    }

    private static string InferPersonalVaultCaptureType(string lane, string kind, string note)
    {
        if (NormalizePersonalVaultLane(lane).Equals("pets", StringComparison.OrdinalIgnoreCase))
        {
            return "pet_note";
        }

        if (NormalizePersonalVaultLane(lane).Equals("lockbox", StringComparison.OrdinalIgnoreCase))
        {
            return "lockbox_note";
        }

        if (IsPersonalSessionKind(kind))
        {
            return "phone_session";
        }

        var normalized = NormalizeConfirmation(note);
        if (ContainsAny(normalized, "buy ", "shopping", "grocery", "groceries", "pick up", "order "))
        {
            return "list_item";
        }

        if (ContainsAny(normalized, "idea", "what if", "could", "maybe", "concept"))
        {
            return "idea";
        }

        if (ContainsAny(normalized, "todo", "to do", "task", "remember to", "need to"))
        {
            return "task";
        }

        if (ContainsAny(normalized, "plan", "goal", "schedule"))
        {
            return "plan";
        }

        return "note";
    }

    private static bool IsPersonalSessionKind(string kind)
    {
        return kind.Equals("personal_session", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksSensitiveForPersonalCloud(string note)
    {
        var normalized = NormalizeConfirmation(note);
        return ContainsAny(
            normalized,
            "password",
            "passcode",
            "social security",
            "ssn",
            "bank account",
            "credit card",
            "legal",
            "lawsuit",
            "tax",
            "medical",
            "diagnosis",
            "prescription",
            "private",
            "secret");
    }

    private static string ScrubPersonalModeTextForCloud(string note)
    {
        var scrubbed = Regex.Replace(note, @"\b\d{3}-\d{2}-\d{4}\b", "[redacted-id]");
        scrubbed = Regex.Replace(scrubbed, @"\b(?:\d[ -]*?){13,16}\b", "[redacted-card-or-number]");
        scrubbed = Regex.Replace(scrubbed, @"(?i)\b(password|passcode|pin)\s*[:=]\s*\S+", "$1: [redacted]");
        return scrubbed.Trim();
    }

    private static string NormalizePersonalVaultLane(string lane)
    {
        return lane.Trim().ToLowerInvariant() switch
        {
            "pets" => "pets",
            "lockbox" => "lockbox",
            _ => "personal"
        };
    }

    private static string SlugifyPersonalVaultTopic(string value)
    {
        var cleaned = Regex.Replace(value, @"[^A-Za-z0-9]+", "_").Trim('_');
        return string.IsNullOrWhiteSpace(cleaned) ? "Inbox" : cleaned;
    }

    private static string HumanizePersonalVaultSlug(string slug)
    {
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(slug.Replace('_', ' ').ToLowerInvariant());
    }

    private static string BuildPersonalVaultTopicMarkdown(string title, IReadOnlyList<PersonalVaultCapture> captures)
    {
        var builder = new StringBuilder();
        builder.AppendLine("---");
        builder.AppendLine("document_type: \"Personal_Vault_Topic\"");
        builder.AppendLine($"topic: \"{EscapeYamlValue(title)}\"");
        builder.AppendLine($"updated_at: \"{DateTimeOffset.Now:O}\"");
        builder.AppendLine("medical_chart_evidence: false");
        builder.AppendLine("---");
        builder.AppendLine();
        builder.AppendLine($"# {title}");
        builder.AppendLine();
        builder.AppendLine("> Derived from Personal Vault tagged captures. Raw captures remain preserved in JSONL.");
        builder.AppendLine();
        foreach (var group in captures.GroupBy(capture => capture.CaptureType).OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
        {
            builder.AppendLine($"## {CultureInfo.InvariantCulture.TextInfo.ToTitleCase(group.Key.Replace('_', ' '))}");
            foreach (var capture in group.OrderBy(capture => ParsePhoneInboxDate(capture.CreatedAt)))
            {
                builder.AppendLine($"- {capture.CreatedAt}: {capture.Note}");
            }

            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static string BuildPersonalVaultIndexMarkdown(IReadOnlyList<IGrouping<string, PersonalVaultCapture>> groups)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Personal Vault Index");
        builder.AppendLine();
        builder.AppendLine("> Personal/Pets memory stays outside the medical chart and evidence scoring.");
        builder.AppendLine();
        foreach (var group in groups)
        {
            var title = FirstNonEmpty(group.FirstOrDefault()?.TopicHint ?? string.Empty, HumanizePersonalVaultSlug(group.Key));
            builder.AppendLine($"- [[{group.Key}|{title}]] - {group.Count()} capture(s)");
        }

        return builder.ToString();
    }

    private static string BuildPersonalMemorySummaryMarkdown(IReadOnlyList<PersonalVaultCapture> captures)
    {
        var memoryCaptures = captures
            .Where(capture => !capture.Lane.Equals("lockbox", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(capture => ParsePhoneInboxDate(capture.CreatedAt))
            .ToList();
        var builder = new StringBuilder();
        builder.AppendLine("---");
        builder.AppendLine("document_type: \"Personal_Memory_Summary\"");
        builder.AppendLine($"updated_at: \"{DateTimeOffset.Now:O}\"");
        builder.AppendLine("medical_chart_evidence: false");
        builder.AppendLine("---");
        builder.AppendLine();
        builder.AppendLine("# Personal Memory Summary");
        builder.AppendLine();
        builder.AppendLine("> Phone-safe Personal/Pets memory summary for on-the-go Dolly context. Lockbox and medical chart evidence are excluded.");
        builder.AppendLine();
        if (memoryCaptures.Count == 0)
        {
            builder.AppendLine("No Personal Vault memory has been synced yet.");
            return builder.ToString();
        }

        builder.AppendLine("## Current Topics");
        foreach (var group in memoryCaptures
                     .GroupBy(capture => FirstNonEmpty(capture.TopicHint, "Inbox"), StringComparer.OrdinalIgnoreCase)
                     .OrderByDescending(group => group.Max(capture => ParsePhoneInboxDate(capture.CreatedAt)))
                     .Take(8))
        {
            var latest = group.Max(capture => ParsePhoneInboxDate(capture.CreatedAt));
            var latestLabel = latest == DateTimeOffset.MinValue
                ? "unknown date"
                : latest.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            builder.AppendLine($"- {group.Key}: {group.Count()} capture(s), latest {latestLabel}.");
        }

        builder.AppendLine();
        builder.AppendLine("## Recent Memory");
        foreach (var capture in memoryCaptures.Take(12))
        {
            var date = ParsePhoneInboxDate(capture.CreatedAt);
            var dateLabel = date == DateTimeOffset.MinValue
                ? FirstNonEmpty(capture.CreatedAt, "unknown date")
                : date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            builder.AppendLine($"- {dateLabel} | {capture.TopicHint} | {capture.CaptureType.Replace('_', ' ')}: {TruncateForChronos(capture.Note, 220)}");
        }

        return builder.ToString();
    }

    private static string BuildPersonalVaultTimelineIndexMarkdown(
        IReadOnlyList<IGrouping<string, PersonalVaultCapture>> yearlyGroups,
        IReadOnlyList<IGrouping<string, PersonalVaultCapture>> monthlyGroups)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Personal Timeline Index");
        builder.AppendLine();
        builder.AppendLine("> Derived from Personal Vault tagged captures. Lockbox notes are excluded from this timeline.");
        builder.AppendLine();
        builder.AppendLine("## Years");
        foreach (var group in yearlyGroups)
        {
            builder.AppendLine($"- [[{group.Key}|{group.Key}]] - {group.Count()} capture(s)");
        }

        builder.AppendLine();
        builder.AppendLine("## Months");
        foreach (var group in monthlyGroups)
        {
            builder.AppendLine($"- [[{group.Key}|{HumanizePersonalVaultMonthKey(group.Key)}]] - {group.Count()} capture(s)");
        }

        return builder.ToString();
    }

    private static string BuildPersonalVaultYearMarkdown(string yearKey, IReadOnlyList<PersonalVaultCapture> captures)
    {
        var builder = new StringBuilder();
        builder.AppendLine("---");
        builder.AppendLine("document_type: \"Personal_Vault_Timeline_Year\"");
        builder.AppendLine($"year: \"{EscapeYamlValue(yearKey)}\"");
        builder.AppendLine($"updated_at: \"{DateTimeOffset.Now:O}\"");
        builder.AppendLine("medical_chart_evidence: false");
        builder.AppendLine("---");
        builder.AppendLine();
        builder.AppendLine($"# Personal Timeline - {yearKey}");
        builder.AppendLine();
        builder.AppendLine("> Date-bounded Personal/Pets context only. Raw captures remain preserved in JSONL.");
        builder.AppendLine();
        foreach (var group in captures.GroupBy(capture => ToPersonalVaultMonthKey(capture.CreatedAt)).OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
        {
            builder.AppendLine($"## {HumanizePersonalVaultMonthKey(group.Key)}");
            AppendPersonalVaultTimelineRows(builder, group.OrderBy(capture => ParsePhoneInboxDate(capture.CreatedAt)));
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static string BuildPersonalVaultMonthMarkdown(string monthKey, IReadOnlyList<PersonalVaultCapture> captures)
    {
        var builder = new StringBuilder();
        builder.AppendLine("---");
        builder.AppendLine("document_type: \"Personal_Vault_Timeline_Month\"");
        builder.AppendLine($"month: \"{EscapeYamlValue(monthKey)}\"");
        builder.AppendLine($"updated_at: \"{DateTimeOffset.Now:O}\"");
        builder.AppendLine("medical_chart_evidence: false");
        builder.AppendLine("---");
        builder.AppendLine();
        builder.AppendLine($"# Personal Timeline - {HumanizePersonalVaultMonthKey(monthKey)}");
        builder.AppendLine();
        builder.AppendLine("> Date-bounded Personal/Pets context only. Raw captures remain preserved in JSONL.");
        builder.AppendLine();
        foreach (var group in captures.GroupBy(capture => capture.TopicHint).OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
        {
            builder.AppendLine($"## {FirstNonEmpty(group.Key, "Inbox")}");
            AppendPersonalVaultTimelineRows(builder, group.OrderBy(capture => ParsePhoneInboxDate(capture.CreatedAt)));
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static void AppendPersonalVaultTimelineRows(StringBuilder builder, IEnumerable<PersonalVaultCapture> captures)
    {
        foreach (var capture in captures)
        {
            var date = ParsePhoneInboxDate(capture.CreatedAt);
            var dateLabel = date == DateTimeOffset.MinValue
                ? FirstNonEmpty(capture.CreatedAt, "unknown date")
                : date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            builder.AppendLine($"- {dateLabel} | {capture.TopicHint} | {capture.CaptureType.Replace('_', ' ')}: {capture.Note}");
        }
    }

    private static string ToPersonalVaultYearKey(string createdAt)
    {
        var date = ParsePhoneInboxDate(createdAt);
        return date == DateTimeOffset.MinValue ? string.Empty : date.ToString("yyyy", CultureInfo.InvariantCulture);
    }

    private static string ToPersonalVaultMonthKey(string createdAt)
    {
        var date = ParsePhoneInboxDate(createdAt);
        return date == DateTimeOffset.MinValue ? string.Empty : date.ToString("yyyy-MM", CultureInfo.InvariantCulture);
    }

    private static string HumanizePersonalVaultMonthKey(string monthKey)
    {
        return DateTimeOffset.TryParseExact(
            $"{monthKey}-01",
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeLocal,
            out var date)
            ? date.ToString("MMMM yyyy", CultureInfo.InvariantCulture)
            : monthKey;
    }

    private static string EscapeYamlValue(string value)
    {
        return (value ?? string.Empty).Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    private void RefreshPhoneInbox()
    {
        var actionStatuses = LoadPhoneInboxActionStatuses();
        var rows = new List<PhoneInboxItemViewModel>();
        rows.AddRange(LoadPhoneCaptureInboxRows(actionStatuses));
        rows.AddRange(LoadPhoneOfflineInboxRows(actionStatuses));

        PhoneInboxItems.Clear();
        foreach (var row in rows
                     .Where(row => !IsPhoneInboxArchived(row.Status))
                     .OrderByDescending(row => ParsePhoneInboxDate(row.CreatedAt))
                     .Take(50))
        {
            PhoneInboxItems.Add(row);
        }

        RefreshPreview();
    }

    private IEnumerable<PhoneInboxItemViewModel> LoadPhoneCaptureInboxRows(IReadOnlyDictionary<string, string> actionStatuses)
    {
        var path = Path.Combine(GetMobileAppDataFolder(), "Mobile_Capture_Requests.jsonl");
        foreach (var document in ReadJsonLines(path))
        {
            var root = document.RootElement;
            var captureId = ReadJsonString(root, "captureId");
            if (string.IsNullOrWhiteSpace(captureId))
            {
                continue;
            }

            var chartContext = ResolveMobileChartContext(ReadJsonString(root, "chartId"));
            var fileName = ReadJsonString(root, "fileName");
            var note = ReadJsonString(root, "note");
            var captureType = FirstNonEmpty(ReadJsonString(root, "captureType"), "attachment");
            var row = new PhoneInboxItemViewModel(
                captureId,
                ReadJsonString(root, "createdAt"),
                chartContext.ChartId,
                chartContext.LocalDisplayName,
                captureType,
                string.IsNullOrWhiteSpace(note) ? $"Attachment saved: {fileName}" : note,
                fileName,
                actionStatuses.TryGetValue(captureId, out var status) ? status : PhoneInboxWaitingStatus,
                ReadJsonString(root, "savedPath"));
            row.MasterHuntHint = BuildPhoneInboxMasterHuntHint(row);
            yield return row;
        }
    }

    private IEnumerable<PhoneInboxItemViewModel> LoadPhoneOfflineInboxRows(IReadOnlyDictionary<string, string> actionStatuses)
    {
        var path = Path.Combine(GetMobileAppDataFolder(), "Mobile_Packet_Requests.jsonl");
        foreach (var document in ReadJsonLines(path))
        {
            var root = document.RootElement;
            if (!ReadJsonString(root, "kind").Equals("offline_item", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var id = ReadJsonString(root, "id");
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            var chartContext = ResolveMobileChartContext(ReadJsonString(root, "chartId"));
            var row = new PhoneInboxItemViewModel(
                id,
                ReadJsonString(root, "createdAt"),
                chartContext.ChartId,
                chartContext.LocalDisplayName,
                "text",
                ReadJsonString(root, "text"),
                string.Empty,
                actionStatuses.TryGetValue(id, out var status) ? status : PhoneInboxWaitingStatus,
                string.Empty);
            row.MasterHuntHint = BuildPhoneInboxMasterHuntHint(row);
            yield return row;
        }
    }

    private Dictionary<string, string> LoadPhoneInboxActionStatuses()
    {
        var statuses = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var document in ReadJsonLines(_phoneInboxActionPath))
        {
            var root = document.RootElement;
            var localId = ReadJsonString(root, "localId");
            var status = ReadJsonString(root, "status");
            if (!string.IsNullOrWhiteSpace(localId) && !string.IsNullOrWhiteSpace(status))
            {
                statuses[localId] = status;
            }
        }

        return statuses;
    }

    private static IEnumerable<JsonDocument> ReadJsonLines(string path)
    {
        if (!File.Exists(path))
        {
            yield break;
        }

        foreach (var line in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            JsonDocument? document = null;
            try
            {
                document = JsonDocument.Parse(line);
            }
            catch (JsonException)
            {
            }

            if (document is not null)
            {
                yield return document;
            }
        }
    }

    private static string ReadJsonString(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var property) && property.ValueKind != JsonValueKind.Null
            ? property.ToString().Trim()
            : string.Empty;
    }

    private static DateTimeOffset ParsePhoneInboxDate(string value)
    {
        return DateTimeOffset.TryParse(value, out var parsed) ? parsed : DateTimeOffset.MinValue;
    }

    private static string GetMobileAppDataFolder()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VitaMR");
    }

    private static string GetPersonalVaultFolder()
    {
        return Path.Combine(GetMobileAppDataFolder(), "PersonalVault");
    }

    private void HandlePhoneInboxAction(object? parameter)
    {
        if (parameter is not string value)
        {
            return;
        }

        var parts = value.Split('|', 2);
        if (parts.Length != 2)
        {
            return;
        }

        var action = parts[0].Trim();
        var localId = parts[1].Trim();
        var item = PhoneInboxItems.FirstOrDefault(row => row.LocalId.Equals(localId, StringComparison.OrdinalIgnoreCase));
        if (item is null)
        {
            return;
        }

        item.Status = action switch
        {
            "review" => "Marked for review",
            "summarize" => "Summary requested",
            "save" => "Save only",
            "add" => "Awaiting chart-add confirmation",
            "accept" => "Accepted as Master evidence",
            "archive" => "Done / archived",
            _ => item.Status
        };
        var masterEvidenceLine = action.Equals("accept", StringComparison.OrdinalIgnoreCase)
            ? AcceptPhoneInboxItemAsDataHunterMasterEvidence(item)
            : string.Empty;
        RecordPhoneInboxAction(item, action);

        if (action.Equals("archive", StringComparison.OrdinalIgnoreCase))
        {
            PhoneInboxItems.Remove(item);
            AddArchivedMessage(
                MessageAuthor.Dolly,
                AgentLabel,
                [
                    $"Done. I archived this phone {item.Kind} from the active inbox.",
                    BuildPhoneInboxSourceLine(item)
                ],
                mode: "phone_inbox_archive",
                linkedChartId: item.ChartId);
            RefreshPreview();
            return;
        }

        var chartContext = new ChartContext(item.ChartId, item.PatientDisplayName);
        string[] lines = action switch
        {
            "review" =>
            [
                $"I queued this phone {item.Kind} for review.",
                BuildPhoneInboxSourceLine(item),
                item.Note
            ],
            "summarize" =>
            [
                $"I queued a summary request for this phone {item.Kind}.",
                "No chart update will be made until the summary is reviewed.",
                BuildPhoneInboxSourceLine(item),
                item.Note
            ],
            "save" =>
            [
                $"I will keep this phone {item.Kind} saved only.",
                "It stays in the desktop phone inbox and raw/mobile queue.",
                BuildPhoneInboxSourceLine(item)
            ],
            "add" =>
            [
                $"I can prepare this phone {item.Kind} for {item.PatientDisplayName}'s chart.",
                "Please confirm in chat before I add anything to the sterile chart.",
                BuildPhoneInboxSourceLine(item),
                item.Note
            ],
            "accept" =>
            [
                $"Accepted this phone {item.Kind} as Master Hunt evidence for {item.PatientDisplayName}.",
                FirstNonEmpty(masterEvidenceLine, "No matching Master target was found yet, so I kept the evidence accepted but did not change a target."),
                "This advances evidence tracking only; it does not rewrite extracted chart facts.",
                BuildPhoneInboxSourceLine(item),
                item.Note
            ],
            _ => Array.Empty<string>()
        };

        if (lines.Length > 0)
        {
            AddArchivedMessage(
                MessageAuthor.Dolly,
                AgentLabel,
                lines,
                mode: $"phone_inbox_{action}",
                linkedChartId: chartContext.ChartId);
        }

        RefreshPreview();
    }

    private void RecordPhoneInboxAction(PhoneInboxItemViewModel item, string action)
    {
        var folder = Path.GetDirectoryName(_phoneInboxActionPath);
        if (!string.IsNullOrWhiteSpace(folder))
        {
            Directory.CreateDirectory(folder);
        }

        var line = JsonSerializer.Serialize(new
        {
            localId = item.LocalId,
            action,
            status = item.Status,
            chartId = item.ChartId,
            patientDisplayName = item.PatientDisplayName,
            kind = item.Kind,
            fileName = item.FileName,
            sourcePath = item.SourcePath,
            actionedAt = DateTimeOffset.Now
        });
        File.AppendAllLines(_phoneInboxActionPath, [line]);
    }

    private bool TryHandleHealthspanModeCommand(string userText, out IReadOnlyList<string> responseLines)
    {
        responseLines = [];
        var normalized = NormalizeConfirmation(userText);
        if (!ContainsAny(normalized, "healthspan", "longevity", "record assistant", "coaching mode", "performance mode", "experimental longevity"))
        {
            return false;
        }

        if (ContainsAny(normalized, "turn off", "switch off", "disable", "record assistant", "records only", "medical records only", "vault only"))
        {
            ApplyHealthspanMode("Record Assistant Mode", false);
            responseLines =
            [
                "Done. I switched back to Record Assistant Mode.",
                "I will focus on organizing, searching, summarizing, and protecting records. I will not push healthspan coaching while this mode is off."
            ];
            return true;
        }

        if (ContainsAny(normalized, "elite", "experimental", "explore"))
        {
            ApplyHealthspanMode("Elite / Experimental Longevity Mode", true);
            responseLines =
            [
                "Done. I set Healthspan Mode to Elite / Experimental Longevity Mode.",
                "I can be more rigorous about tracking and research awareness, while keeping it separate from medical advice, diagnosis, prescribing, triage, and test ordering."
            ];
            return true;
        }

        if (ContainsAny(normalized, "performance", "perform", "100", "beyond", "accountability", "direct"))
        {
            ApplyHealthspanMode("Longevity Performance Mode", true);
            responseLines =
            [
                "Done. I set Healthspan Mode to Longevity Performance Mode.",
                "That means more direct accountability is allowed, but I will still stay respectful and separate coaching from medical advice."
            ];
            return true;
        }

        if (ContainsAny(normalized, "turn on", "switch on", "enable", "support", "healthspan on"))
        {
            ApplyHealthspanMode("Healthspan Support Mode", true);
            responseLines =
            [
                "Done. I turned on Healthspan Support Mode.",
                "Choose how you want Dolly to coach: Support, Perform, or Explore."
            ];
            return true;
        }

        return false;
    }

    private void EnableHealthspanModeWithChoicePrompt()
    {
        ApplyHealthspanMode("Healthspan Support Mode", true);
        AddArchivedMessage(
            MessageAuthor.Dolly,
            $"{AgentDisplayName} - {DateTime.Now:h:mm tt}",
            [
                "Healthspan Mode is on.",
                "Choose how you want Dolly to coach: Support, Perform, or Explore.",
                "Support uses records and goals to guide healthspan optimization. Perform adds clearer accountability. Explore tracks advanced research-aware ideas separately from medical advice."
            ],
            mode: "healthspan_mode_choice_prompt",
            linkedChartId: ActiveChartId);
    }

    private void ApplyHealthspanMode(string mode, bool enabled)
    {
        _settings.EnableHealthspanMode = enabled;
        _settings.HealthspanCoachingMode = enabled ? mode : "Record Assistant Mode";
        _settingsService.Save(_settings);
        OnPropertyChanged(nameof(EnableHealthspanMode));
        OnPropertyChanged(nameof(HealthspanCoachingMode));
        OnPropertyChanged(nameof(IsRecordsModeSelected));
        OnPropertyChanged(nameof(IsHealthspanModeSelected));
        OnPropertyChanged(nameof(IsHealthspanSupportSelected));
        OnPropertyChanged(nameof(IsLongevityPerformanceSelected));
        OnPropertyChanged(nameof(IsExperimentalLongevitySelected));
        OnPropertyChanged(nameof(HealthspanIntensityVisibility));
        OnPropertyChanged(nameof(HealthspanModeSummary));
        OnPropertyChanged(nameof(HealthspanModeDetail));
        OnPropertyChanged(nameof(HealthspanStarBrush));
        _chronosLedgerService.RecordEvent(
            VaultRootPath,
            GetActiveChart(),
            "healthspan_mode_changed",
            $"{HealthspanModeSummary}. {HealthspanModeDetail}",
            AgentLabel,
            "healthspan_mode");
        VaultWriteStatus = "Healthspan mode updated";
    }

    private void ApplyLifeMode(string? mode)
    {
        var normalized = NormalizeLifeMode(mode);
        if (_settings.ActiveLifeMode == normalized)
        {
            return;
        }

        _settings.ActiveLifeMode = normalized;
        _settingsService.Save(_settings);
        OnLifeModeChanged();
        _chronosLedgerService.RecordEvent(
            VaultRootPath,
            GetActiveChart(),
            "life_mode_changed",
            $"{normalized} Mode selected. {ActiveLifeModeDetail}",
            AgentLabel,
            "life_mode");
        VaultWriteStatus = $"{normalized} Mode active";
    }

    public Task<MobileLifeModeResponse> HandleMobileLifeModeAsync(
        MobileLifeModeRequest request,
        MobileTrustedDevice device,
        CancellationToken cancellationToken = default)
    {
        if (!Application.Current.Dispatcher.CheckAccess())
        {
            var operation = Application.Current.Dispatcher.InvokeAsync(
                () => HandleMobileLifeModeAsync(request, device, cancellationToken),
                DispatcherPriority.Normal,
                cancellationToken);
            return operation.Task.Unwrap();
        }

        ApplyLifeMode(request.Mode);
        var mode = ActiveLifeMode;

        return Task.FromResult(new MobileLifeModeResponse
        {
            Status = "life_mode_synced",
            Mode = mode,
            Message = $"Desktop switched to {mode} Mode."
        });
    }

    private void OnLifeModeChanged()
    {
        OnPropertyChanged(nameof(ActiveLifeMode));
        OnPropertyChanged(nameof(IsMedicalLifeModeSelected));
        OnPropertyChanged(nameof(IsPersonalLifeModeSelected));
        OnPropertyChanged(nameof(IsLockboxLifeModeSelected));
        OnPropertyChanged(nameof(IsPetsLifeModeSelected));
        OnPropertyChanged(nameof(ActiveLifeModeDetail));
        RefreshPreview();
    }

    private static string NormalizeLifeMode(string? mode)
    {
        return mode?.Trim().ToLowerInvariant() switch
        {
            "personal" => "Personal",
            "lockbox" or "secret" or "secret mode" or "local lockbox" => "Lockbox",
            "pets" => "Pets",
            _ => "Medical"
        };
    }

    private bool TryHandleHealthspanMotivationLadder(string userText, out IReadOnlyList<string> responseLines)
    {
        responseLines = [];
        if (string.IsNullOrWhiteSpace(_pendingHealthspanMotivationStep))
        {
            if (!IsHealthspanMotivationLadderStartRequest(userText))
            {
                return false;
            }

            if (!EnableHealthspanMode)
            {
                responseLines =
                [
                    "Healthspan Mode is off right now.",
                    "Switch to Healthspan Support, Longevity Performance, or Elite / Experimental mode first, then I can run the Motivation Ladder."
                ];
                return true;
            }

            _pendingHealthspanMotivationAnswers.Clear();
            _pendingHealthspanMotivationStep = "goal_area";
            responseLines =
            [
                "Okay. I will run a short Healthspan Motivation Ladder for this chart.",
                "What part of your healthspan do you most want to protect or improve right now? For example: energy, strength, mobility, sleep, mood, clear thinking, pain reduction, prevention, independence, longevity, or medical-record organization."
            ];
            return true;
        }

        responseLines = CaptureHealthspanMotivationAnswer(userText);
        return true;
    }

    private IReadOnlyList<string> CaptureHealthspanMotivationAnswer(string userText)
    {
        var answer = userText.Trim();
        switch (_pendingHealthspanMotivationStep)
        {
            case "goal_area":
                _pendingHealthspanMotivationAnswers["GoalArea"] = answer;
                _pendingHealthspanMotivationStep = "importance";
                return ["On a scale from 1-10, how important is this to you right now?"];
            case "importance":
                _pendingHealthspanMotivationAnswers["Importance"] = answer;
                _pendingHealthspanMotivationStep = "importance_why";
                return ["Why did you choose that number and not a lower one?"];
            case "importance_why":
                _pendingHealthspanMotivationAnswers["ImportanceWhy"] = answer;
                _pendingHealthspanMotivationStep = "confidence";
                return ["On a scale from 1-10, how confident do you feel that you could take one small step?"];
            case "confidence":
                _pendingHealthspanMotivationAnswers["Confidence"] = answer;
                _pendingHealthspanMotivationStep = "confidence_help";
                return ["What would help move your confidence up by one point?"];
            case "confidence_help":
                _pendingHealthspanMotivationAnswers["ConfidenceHelp"] = answer;
                _pendingHealthspanMotivationStep = "readiness";
                return ["On a scale from 1-10, how ready are you to take action now?"];
            case "readiness":
                _pendingHealthspanMotivationAnswers["Readiness"] = answer;
                _pendingHealthspanMotivationStep = "next_step";
                return ["What feels like the smallest useful next step?"];
            case "next_step":
                _pendingHealthspanMotivationAnswers["NextStep"] = answer;
                _pendingHealthspanMotivationStep = "support";
                return ["What kind of support would make this easier?"];
            case "support":
                _pendingHealthspanMotivationAnswers["Support"] = answer;
                _pendingHealthspanMotivationStep = "confirm";
                return BuildHealthspanMotivationSummaryLines(includeConfirmationPrompt: true);
            case "confirm":
                if (IsAffirmative(answer))
                {
                    var path = SaveHealthspanMotivationLadder(GetActiveChart());
                    _pendingHealthspanMotivationStep = string.Empty;
                    _pendingHealthspanMotivationAnswers.Clear();
                    return
                    [
                        "Saved. I added this Motivation Ladder summary to the chart's healthspan system notes.",
                        $"Saved file: {Path.GetFileName(path)}"
                    ];
                }

                _pendingHealthspanMotivationStep = string.Empty;
                _pendingHealthspanMotivationAnswers.Clear();
                return ["Okay. I did not save that Motivation Ladder summary."];
            default:
                _pendingHealthspanMotivationStep = string.Empty;
                _pendingHealthspanMotivationAnswers.Clear();
                return ["I reset the Motivation Ladder because the step state was unclear. Say \"start motivation ladder\" when you want to try again."];
        }
    }

    private IReadOnlyList<string> BuildHealthspanMotivationSummaryLines(bool includeConfirmationPrompt)
    {
        var lines = new List<string>
        {
            "Here is what I heard:",
            $"- You care about {GetPendingMotivationAnswer("GoalArea")} because {GetPendingMotivationAnswer("ImportanceWhy")}.",
            $"- You feel most ready to {GetPendingMotivationAnswer("NextStep")}.",
            $"- Your confidence is {GetPendingMotivationAnswer("Confidence")}/10 and your readiness is {GetPendingMotivationAnswer("Readiness")}/10.",
            $"- Support that could help: {GetPendingMotivationAnswer("Support")}."
        };

        if (includeConfirmationPrompt)
        {
            lines.Add("Did I understand you correctly? Reply yes to save this, or no to discard it.");
        }

        return lines;
    }

    private string SaveHealthspanMotivationLadder(ChartContext chartContext)
    {
        var systemFolder = Path.Combine(VaultRootPath, chartContext.ChartFolderName, "wiki", "system");
        Directory.CreateDirectory(systemFolder);
        var path = Path.Combine(systemFolder, "Healthspan_Motivation_Ladder.md");
        var builder = new StringBuilder();
        builder.AppendLine("# Healthspan Motivation Ladder");
        builder.AppendLine();
        builder.AppendLine($"> Mode: {HealthspanModeSummary}");
        builder.AppendLine($"> Captured: {DateTimeOffset.Now:O}");
        builder.AppendLine($"> Scope: user-stated motivation and readiness, not medical advice or verified chart evidence.");
        builder.AppendLine();
        foreach (var line in BuildHealthspanMotivationSummaryLines(includeConfirmationPrompt: false))
        {
            builder.AppendLine(line);
        }

        builder.AppendLine();
        builder.AppendLine("## Raw Answers");
        builder.AppendLine();
        foreach (var pair in _pendingHealthspanMotivationAnswers)
        {
            builder.AppendLine($"- {pair.Key}: {pair.Value}");
        }

        File.WriteAllText(path, builder.ToString());
        _chronosLedgerService.RecordEvent(
            VaultRootPath,
            chartContext,
            "healthspan_motivation_ladder_saved",
            $"Saved Motivation Ladder summary for {chartContext.LocalDisplayName}: {GetPendingMotivationAnswer("GoalArea")}.",
            AgentLabel,
            "healthspan_motivation");
        return path;
    }

    private string GetPendingMotivationAnswer(string key)
    {
        return _pendingHealthspanMotivationAnswers.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : "not answered";
    }

    private static bool IsHealthspanMotivationLadderStartRequest(string value)
    {
        var normalized = NormalizeConfirmation(value);
        return ContainsAny(normalized, "motivation ladder", "motivation assessment", "healthspan assessment", "start motivation", "readiness assessment");
    }

    private string AcceptPhoneInboxItemAsDataHunterMasterEvidence(PhoneInboxItemViewModel item)
    {
        var chartContext = new ChartContext(item.ChartId, item.PatientDisplayName);
        var map = LoadDataHunterPersonalDataMap(chartContext);
        var quests = GenerateDataHunterMasterQuests(map);
        var matchedQuest = FindDataHunterMasterQuestForPhoneInboxItem(quests, item);
        if (matchedQuest is null)
        {
            map.MasterQuests = quests;
            SaveDataHunterPersonalDataMap(chartContext, map);
            RefreshDataHunterProperties();
            return string.Empty;
        }

        matchedQuest.Status = "Accepted evidence received";
        matchedQuest.AcceptedEvidenceSummary = BuildPhoneInboxEvidenceSummary(item);
        matchedQuest.AcceptedEvidenceSourceId = item.LocalId;
        matchedQuest.AcceptedAt = DateTimeOffset.Now;
        map.MasterQuests = quests
            .Select(quest => quest.QuestId.Equals(matchedQuest.QuestId, StringComparison.OrdinalIgnoreCase) ? matchedQuest : quest)
            .ToList();
        SaveDataHunterPersonalDataMap(chartContext, map);
        RefreshDataHunterProperties();

        _chronosLedgerService.RecordEvent(
            VaultRootPath,
            chartContext,
            "data_hunter_master_evidence_accepted",
            $"Accepted phone inbox evidence for Master Hunt target: {matchedQuest.Title}.",
            "phone_inbox",
            "data_hunter");

        return $"Master target updated: {matchedQuest.Title}. Evidence XP remains governed by vault acceptance rules.";
    }

    private string BuildPhoneInboxMasterHuntHint(PhoneInboxItemViewModel item)
    {
        var chartContext = new ChartContext(item.ChartId, item.PatientDisplayName);
        var quests = GenerateDataHunterMasterQuests(LoadDataHunterPersonalDataMap(chartContext));
        var matchedQuest = FindDataHunterMasterQuestForPhoneInboxItem(quests, item);
        if (matchedQuest is null)
        {
            return "Master Hunt: no matching target yet. Accepting will keep source history without awarding target progress.";
        }

        var state = IsDataHunterMasterQuestAccepted(matchedQuest)
            ? "already satisfied"
            : "pending evidence";
        return $"Master Hunt match: {matchedQuest.Title} ({state}). {BuildDataHunterQuestClueLine(matchedQuest)}.";
    }

    private static DataHunterMasterQuest? FindDataHunterMasterQuestForPhoneInboxItem(
        IReadOnlyList<DataHunterMasterQuest> quests,
        PhoneInboxItemViewModel item)
    {
        var evidenceText = NormalizeConfirmation($"{item.Kind} {item.Title} {item.Detail} {item.SourceDetail} {item.Note} {item.FileName} {item.SourcePath}");
        var matchOrder = new (string QuestIdPart, string[] Needles)[]
        {
            ("doctor", ["doctor", "pcp", "primary care", "clinic", "specialist", "visit", "provider"]),
            ("specialist", ["specialist", "cardiology", "dermatology", "neurology", "orthopedic", "gastro", "endocr"]),
            ("pharmacy", ["pharmacy", "medication", "medications", "prescription", "rx", "cvs", "walgreens", "rite aid"]),
            ("lab", ["lab", "labs", "bloodwork", "blood work", "quest", "labcorp", "result"]),
            ("imaging", ["imaging", "radiology", "xray", "x-ray", "mri", "ct", "ultrasound", "mammogram", "cd"]),
            ("vaccine", ["vaccine", "vaccination", "immunization", "shot"]),
            ("paper", ["paper", "scan", "scanned", "folder", "binder", "printout", "document"]),
            ("portal", ["portal", "mychart", "epic", "cerner", "athena", "export"]),
            ("hospital", ["hospital", "er", "ed", "emergency", "discharge", "admission", "inpatient"])
        };

        foreach (var (questIdPart, needles) in matchOrder)
        {
            if (!ContainsAny(evidenceText, needles))
            {
                continue;
            }

            var matched = quests.FirstOrDefault(quest =>
                quest.QuestId.Contains(questIdPart, StringComparison.OrdinalIgnoreCase) ||
                quest.Title.Contains(questIdPart, StringComparison.OrdinalIgnoreCase) ||
                quest.Target.Contains(questIdPart, StringComparison.OrdinalIgnoreCase));
            if (matched is not null)
            {
                return matched;
            }
        }

        return quests.FirstOrDefault(quest =>
            quest.QuestId.Contains("record_location", StringComparison.OrdinalIgnoreCase) ||
            quest.Status.Contains("Needs Basic", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsDataHunterMasterQuestAccepted(DataHunterMasterQuest quest)
    {
        return !string.IsNullOrWhiteSpace(quest.AcceptedEvidenceSummary) ||
            !string.IsNullOrWhiteSpace(quest.AcceptedEvidenceSourceId) ||
            quest.AcceptedAt.HasValue ||
            quest.Status.Contains("Accepted", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildDataHunterQuestClueLine(DataHunterMasterQuest quest)
    {
        if (!string.IsNullOrWhiteSpace(quest.SourceClueSummary))
        {
            return $"Clue: {quest.SourceClueSummary}";
        }

        return $"Target: {quest.Target}";
    }

    private static string BuildPhoneInboxEvidenceSummary(PhoneInboxItemViewModel item)
    {
        return string.Join(
            " | ",
            new[]
            {
                item.Kind,
                item.FileName,
                item.Note,
                item.SourcePath
            }.Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static string BuildPhoneInboxSourceLine(PhoneInboxItemViewModel item)
    {
        if (string.IsNullOrWhiteSpace(item.SourcePath))
        {
            return "Source: saved phone text item.";
        }

        return $"Source: {item.SourcePath}";
    }

    private static bool IsPhoneInboxArchived(string status)
    {
        return status.Contains("archived", StringComparison.OrdinalIgnoreCase);
    }

    private IReadOnlyList<string> BuildMobileNotesPacketLines(ChartContext chartContext, string mode)
    {
        var encountersFolder = Path.Combine(VaultRootPath, chartContext.ChartFolderName, "wiki", "encounters");
        if (!Directory.Exists(encountersFolder))
        {
            return
            [
                $"I do not see sterile encounter notes filed for {ExtractFirstNameForDisplay(chartContext.LocalDisplayName)} yet.",
                "When notes are ingested, Dolly will package them here from the chart wiki, not from raw files."
            ];
        }

        var normalizedMode = NormalizeConfirmation(mode);
        var files = Directory.GetFiles(encountersFolder, "*.md")
            .Where(path => MobileNoteMatchesMode(path, normalizedMode))
            .OrderByDescending(path => ExtractDateForMobileNote(path))
            .ThenBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
            .Take(normalizedMode.Contains("recent", StringComparison.OrdinalIgnoreCase) ? 10 : 1)
            .ToList();

        if (files.Count == 0)
        {
            return
            [
                $"I do not see sterile encounter notes filed for {ExtractFirstNameForDisplay(chartContext.LocalDisplayName)} yet.",
                "When notes are ingested, Dolly will package them here from the chart wiki, not from raw files."
            ];
        }

        var lines = new List<string>
        {
            mode.Equals("synthesized", StringComparison.OrdinalIgnoreCase)
                ? $"{chartContext.LocalDisplayName} has these synthesized sterile note summaries:"
                : $"{chartContext.LocalDisplayName} has these sterile chart notes:"
        };

        foreach (var file in files)
        {
            var content = File.ReadAllText(file);
            var title = ExtractMarkdownTitle(content, Path.GetFileNameWithoutExtension(file));
            var date = ExtractFrontMatterValue(content, "date_of_service");
            var type = ExtractFrontMatterValue(content, "document_type");
            var risk = ExtractFrontMatterValue(content, "risk_level");
            var gestalt = ExtractClinicalGestalt(content);
            var plan = ExtractMobileNoteSectionBullets(content, "Assessment & Plan", 4);
            var pending = ExtractMobileNoteSectionBullets(content, "Pending Items", 3);

            lines.Add(string.Empty);
            lines.Add($"## {title}");
            if (!string.IsNullOrWhiteSpace(date) || !string.IsNullOrWhiteSpace(type) || !string.IsNullOrWhiteSpace(risk))
            {
                lines.Add($"Date: {BlankIfMissing(date)} | Type: {BlankIfMissing(type)} | Risk: {BlankIfMissing(risk)}");
            }

            if (!string.IsNullOrWhiteSpace(gestalt))
            {
                lines.Add($"Summary: {gestalt}");
            }

            if (plan.Count > 0)
            {
                lines.Add("Plan:");
                lines.AddRange(plan.Select(item => $"- {item}"));
            }

            if (pending.Count > 0)
            {
                lines.Add("Pending:");
                lines.AddRange(pending.Select(item => $"- {item}"));
            }
        }

        return lines;
    }

    private IReadOnlyList<string> BuildMobileSpecialtyPacketLines(
        ChartContext chartContext,
        string specialtyFileName,
        string label,
        string emptyMessage)
    {
        var specialtyPath = Path.Combine(VaultRootPath, chartContext.ChartFolderName, "wiki", "specialty", $"{specialtyFileName}.md");
        if (!File.Exists(specialtyPath))
        {
            return [emptyMessage, "Sync the desktop specialty files after records are ingested, then try travel sync again."];
        }

        var content = File.ReadAllText(specialtyPath);
        var rows = BuildMobileSpecialtyRows(chartContext, content, specialtyFileName, maxRows: 8);
        if (rows.Count == 0)
        {
            return [emptyMessage, $"The {label} specialty page exists but does not have portable rows yet."];
        }

        return
        [
            $"{chartContext.LocalDisplayName}: {label}",
            string.Empty,
            .. rows
        ];
    }

    private IReadOnlyList<string> BuildMobileQuestionsPacketLines(ChartContext chartContext)
    {
        var wikiFolder = Path.Combine(VaultRootPath, chartContext.ChartFolderName, "wiki");
        var lines = new List<string>
        {
            $"{chartContext.LocalDisplayName}: saved questions and things to ask",
            string.Empty
        };

        AddFileHighlights(lines, Path.Combine(wikiFolder, "Care_Gaps.md"), "Open Care Gaps", 8);
        AddFileHighlights(lines, Path.Combine(wikiFolder, "Dolly_Working_Summary.md"), "Working Summary Questions", 8);
        AddFileHighlights(lines, Path.Combine(wikiFolder, "Health_Preferences.md"), "Preference Check-ins", 8);

        if (lines.Count <= 2)
        {
            lines.Add("No saved question packet is available yet.");
            lines.Add("As Dolly captures user questions and chart review items, this packet becomes a portable list for later phone-agent access.");
        }

        return lines;
    }

    private IReadOnlyList<string> BuildMobileOtherPacketLines(ChartContext chartContext, string request)
    {
        var normalizedRequest = NormalizeConfirmation(request);
        if (string.IsNullOrWhiteSpace(normalizedRequest) || normalizedRequest.Equals("current", StringComparison.OrdinalIgnoreCase))
        {
            return ["Tell VitaMR what record to load, such as last echo, CPET, medication list, last cardiology note, or last three notes."];
        }

        if (Regex.IsMatch(normalizedRequest, @"\b(cardio|heart|echo|ekg|ecg|cpet)\b", RegexOptions.IgnoreCase))
        {
            return BuildMobileSpecialtyPacketLines(chartContext, "Cardiology", request, "No cardiology packet is available yet.");
        }

        if (Regex.IsMatch(normalizedRequest, @"\b(psych|psychiatry|behavior|depression|anxiety)\b", RegexOptions.IgnoreCase))
        {
            return BuildMobileSpecialtyPacketLines(chartContext, "Psychiatry_Behavioral", request, "No psychiatry packet is available yet.");
        }

        if (Regex.IsMatch(normalizedRequest, @"\b(lab|cbc|cmp|a1c|glucose|troponin)\b", RegexOptions.IgnoreCase))
        {
            return BuildMobileSpecialtyPacketLines(chartContext, "Labs_Master", request, "No lab packet is available yet.");
        }

        if (Regex.IsMatch(normalizedRequest, @"\b(med|meds|medication|medications|medicine|prescription|prescriptions)\b", RegexOptions.IgnoreCase))
        {
            return BuildMobileIndexSectionPacketLines(chartContext, "Medications", "medication list", "No medication list is available yet.");
        }

        if (Regex.IsMatch(normalizedRequest, @"\b(image|imaging|xray|x ray|ct|mri|ultrasound|radiology)\b", RegexOptions.IgnoreCase))
        {
            return BuildMobileSpecialtyPacketLines(chartContext, "Imaging_Radiology", request, "No imaging packet is available yet.");
        }

        var wikiFolder = Path.Combine(VaultRootPath, chartContext.ChartFolderName, "wiki");
        var matches = Directory.Exists(wikiFolder)
            ? Directory.GetFiles(wikiFolder, "*.md", SearchOption.AllDirectories)
                .Where(path => IsPortableWikiPath(path))
                .Select(path => new { Path = path, Content = File.ReadAllText(path) })
                .Where(item => RequestMatchesContent(normalizedRequest, item.Path, item.Content))
                .OrderByDescending(item => ExtractDateForMobileNote(item.Path))
                .Take(5)
                .ToList()
            : [];

        if (matches.Count == 0)
        {
            return
            [
                $"No portable match was found for: {request}",
                "Try a more specific request like last cardiology note, last echo, latest labs, recent imaging, or medication list."
            ];
        }

        var lines = new List<string> { $"{chartContext.LocalDisplayName}: {request}", string.Empty };
        foreach (var match in matches)
        {
            lines.Add($"## {ExtractMarkdownTitle(match.Content, Path.GetFileNameWithoutExtension(match.Path))}");
            lines.AddRange(ExtractUsefulMarkdownLines(match.Content, 8));
            lines.Add(string.Empty);
        }

        return lines;
    }

    private IReadOnlyList<string> BuildMobileSpecialtyRows(
        ChartContext chartContext,
        string specialtyContent,
        string specialtyFileName,
        int maxRows)
    {
        var rows = ExtractMarkdownTableRows(specialtyContent)
            .Take(maxRows)
            .ToList();
        if (rows.Count == 0)
        {
            return ExtractUsefulMarkdownLines(specialtyContent, maxRows);
        }

        var lines = new List<string>();
        foreach (var row in rows)
        {
            var date = CellOrBlank(row, 0);
            var eventType = CellOrBlank(row, 2);
            var finding = CellOrBlank(row, 3);
            var status = CellOrBlank(row, 4);
            var confidence = CellOrBlank(row, 5);
            var wikiLink = CellOrBlank(row, 6);
            var encounterName = ExtractEncounterName(wikiLink) ?? ExtractEncounterName(finding);
            var encounterContent = ReadEncounterContent(chartContext, encounterName);
            var heading = string.IsNullOrWhiteSpace(date)
                ? eventType
                : $"{date} - {eventType}";

            lines.Add($"## {BlankIfMissing(heading)}");
            if (!string.IsNullOrWhiteSpace(status) || !string.IsNullOrWhiteSpace(confidence))
            {
                lines.Add($"Status: {BlankIfMissing(status)} | Routing: {BlankIfMissing(confidence)}");
            }

            if (!string.IsNullOrWhiteSpace(encounterContent))
            {
                var gestalt = ExtractClinicalGestalt(encounterContent);
                if (!string.IsNullOrWhiteSpace(gestalt))
                {
                    lines.Add($"Summary: {gestalt}");
                }

                var sectionName = specialtyFileName.Equals("Labs_Master", StringComparison.OrdinalIgnoreCase)
                    ? "Labs / Results"
                    : "Imaging";
                var sectionLines = ExtractMobileSectionLines(encounterContent, sectionName, 8);
                if (sectionLines.Count > 0)
                {
                    lines.Add($"{sectionName}:");
                    lines.AddRange(sectionLines.Select(item => $"- {item}"));
                }
            }
            else
            {
                var cleanFinding = NormalizeMobilePacketText(finding);
                if (!string.IsNullOrWhiteSpace(cleanFinding))
                {
                    lines.Add(cleanFinding);
                }
            }

            lines.Add(string.Empty);
        }

        return lines;
    }

    private IReadOnlyList<string> BuildMobileIndexSectionPacketLines(
        ChartContext chartContext,
        string sectionName,
        string label,
        string emptyMessage)
    {
        var indexPath = Path.Combine(VaultRootPath, chartContext.ChartFolderName, "wiki", "Index.md");
        if (!File.Exists(indexPath))
        {
            return [emptyMessage, "The chart index is not available yet."];
        }

        var content = File.ReadAllText(indexPath);
        var section = ExtractMarkdownSection(content, sectionName);
        var rows = ExtractMarkdownTableRows(section)
            .Take(12)
            .Select(row => string.Join(" | ", row.Where(cell => !string.IsNullOrWhiteSpace(cell)).Take(4)))
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

        if (rows.Count == 0)
        {
            return [emptyMessage];
        }

        return
        [
            $"{chartContext.LocalDisplayName}: {label}",
            string.Empty,
            .. rows.Select(row => $"- {NormalizeMobilePacketText(row)}")
        ];
    }

    private static bool MobileNoteMatchesMode(string path, string normalizedMode)
    {
        if (string.IsNullOrWhiteSpace(normalizedMode) ||
            normalizedMode.Equals("current", StringComparison.OrdinalIgnoreCase) ||
            normalizedMode.Contains("recent", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var fileName = NormalizeConfirmation(Path.GetFileNameWithoutExtension(path));
        var content = NormalizeConfirmation(File.ReadAllText(path));
        var haystack = $"{fileName} {content}";
        return normalizedMode switch
        {
            var value when value.Contains("card", StringComparison.OrdinalIgnoreCase) =>
                Regex.IsMatch(haystack, @"\b(cardio|cardiology|cardiologist|heart|echo|ekg|ecg)\b", RegexOptions.IgnoreCase),
            var value when value.Contains("psych", StringComparison.OrdinalIgnoreCase) =>
                Regex.IsMatch(haystack, @"\b(psych|psychiatry|behavior|depression|anxiety|adhd)\b", RegexOptions.IgnoreCase),
            var value when value.Contains("pcp", StringComparison.OrdinalIgnoreCase) ||
                           value.Contains("primary", StringComparison.OrdinalIgnoreCase) =>
                Regex.IsMatch(haystack, @"\b(primary care|pcp|annual|preventive|well visit|routine)\b", RegexOptions.IgnoreCase),
            _ => true
        };
    }

    private static List<string> ExtractUsefulMarkdownLines(string content, int maxLines)
    {
        return content
            .Split(["\r\n", "\n"], StringSplitOptions.TrimEntries)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Where(line => !line.StartsWith("---", StringComparison.Ordinal) &&
                           !line.Contains("chart_id:", StringComparison.OrdinalIgnoreCase) &&
                           !line.Contains("dolly_directive:", StringComparison.OrdinalIgnoreCase) &&
                           !line.StartsWith("# ", StringComparison.Ordinal))
            .Select(NormalizeMobilePacketText)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Take(maxLines)
            .ToList();
    }

    private static List<List<string>> ExtractMarkdownTableRows(string content)
    {
        return content
            .Split(["\r\n", "\n"], StringSplitOptions.TrimEntries)
            .Where(line => line.StartsWith("|", StringComparison.Ordinal) &&
                           line.EndsWith("|", StringComparison.Ordinal) &&
                           !line.Contains("|---", StringComparison.Ordinal) &&
                           !line.Contains("Date (YYYY", StringComparison.OrdinalIgnoreCase) &&
                           !line.Contains("Medication | Dose", StringComparison.OrdinalIgnoreCase))
            .Select(line => line.Trim('|')
                .Split('|', StringSplitOptions.TrimEntries)
                .Select(NormalizeMobilePacketText)
                .ToList())
            .Where(cells => cells.Count > 0 && cells.Any(cell => !string.IsNullOrWhiteSpace(cell)))
            .ToList();
    }

    private static string CellOrBlank(IReadOnlyList<string> cells, int index)
    {
        return index >= 0 && index < cells.Count ? cells[index] : string.Empty;
    }

    private static string? ExtractEncounterName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var match = Regex.Match(value, @"\[\[(?:encounters/)?(?<name>[^\]#|]+)(?:#[^\]|]+)?(?:\|[^\]]+)?\]\]");
        return match.Success ? match.Groups["name"].Value.Trim() : null;
    }

    private string ReadEncounterContent(ChartContext chartContext, string? encounterName)
    {
        if (string.IsNullOrWhiteSpace(encounterName))
        {
            return string.Empty;
        }

        var encounterPath = Path.Combine(VaultRootPath, chartContext.ChartFolderName, "wiki", "encounters", $"{encounterName}.md");
        return File.Exists(encounterPath) ? File.ReadAllText(encounterPath) : string.Empty;
    }

    private static string ExtractMarkdownSection(string content, string heading)
    {
        var match = Regex.Match(
            content,
            $@"(?ms)^##\s+{Regex.Escape(heading)}\s*(?<body>.*?)(?=^##\s+|\z)");
        return match.Success ? match.Groups["body"].Value : string.Empty;
    }

    private static IReadOnlyList<string> ExtractMobileSectionLines(string content, string heading, int maxItems)
    {
        var body = ExtractMarkdownSection(content, heading);
        if (string.IsNullOrWhiteSpace(body))
        {
            var levelThree = Regex.Match(
                content,
                $@"(?ms)^###\s+{Regex.Escape(heading)}\s*(?<body>.*?)(?=^###\s+|^##\s+|\z)");
            body = levelThree.Success ? levelThree.Groups["body"].Value : string.Empty;
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            return [];
        }

        var tableRows = ExtractMarkdownTableRows(body)
            .Select(row => string.Join(" | ", row.Where(cell => !string.IsNullOrWhiteSpace(cell)).Take(5)))
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Take(maxItems)
            .ToList();
        if (tableRows.Count > 0)
        {
            return tableRows;
        }

        return body
            .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !line.StartsWith("#", StringComparison.Ordinal) &&
                           !line.StartsWith("|---", StringComparison.Ordinal) &&
                           !line.StartsWith("<!--", StringComparison.Ordinal))
            .Select(line => NormalizeMobilePacketText(line.TrimStart('-', '*', ' ')))
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Take(maxItems)
            .ToList();
    }

    private static void AddFileHighlights(List<string> lines, string path, string heading, int maxLines)
    {
        if (!File.Exists(path))
        {
            return;
        }

        var highlights = ExtractUsefulMarkdownLines(File.ReadAllText(path), maxLines);
        if (highlights.Count == 0)
        {
            return;
        }

        lines.Add($"## {heading}");
        lines.AddRange(highlights);
        lines.Add(string.Empty);
    }

    private static bool IsPortableWikiPath(string path)
    {
        return !path.Contains($"{Path.DirectorySeparatorChar}raw{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) &&
               !Path.GetFileName(path).Equals("Vita_Mastery.md", StringComparison.OrdinalIgnoreCase);
    }

    private static bool RequestMatchesContent(string normalizedRequest, string path, string content)
    {
        var tokens = normalizedRequest
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(token => token.Length > 2)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var haystack = NormalizeConfirmation($"{Path.GetFileName(path)} {content}");
        return tokens.Count > 0 && tokens.Any(token => haystack.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private static DateOnly ExtractDateForMobileNote(string path)
    {
        var fileName = Path.GetFileName(path);
        var match = Regex.Match(fileName, @"(?<date>\d{4}-\d{2}-\d{2})");
        return match.Success && DateOnly.TryParse(match.Groups["date"].Value, out var parsed)
            ? parsed
            : DateOnly.MinValue;
    }

    private static string ExtractMarkdownTitle(string content, string fallback)
    {
        var match = Regex.Match(content, @"(?m)^#\s+(?<title>.+)$");
        return match.Success ? match.Groups["title"].Value.Trim() : fallback.Replace('_', ' ');
    }

    private static string ExtractFrontMatterValue(string content, string key)
    {
        var match = Regex.Match(content, $@"(?m)^{Regex.Escape(key)}:\s*""?(?<value>[^""\r\n]+)""?\s*$");
        return match.Success ? match.Groups["value"].Value.Trim() : string.Empty;
    }

    private static string ExtractClinicalGestalt(string content)
    {
        var blockQuote = Regex.Match(content, @">\s*\*\*Clinical Gestalt:\*\*\s*(?<value>.+?)(?:\r?\n\r?\n|###|\z)", RegexOptions.Singleline);
        if (blockQuote.Success)
        {
            return NormalizeMobilePacketText(blockQuote.Groups["value"].Value);
        }

        var admonition = Regex.Match(content, @">\s*\[!abstract\]\s*Clinical Gestalt\s*\r?\n>\s*(?<value>.+?)(?:\r?\n\r?\n|###|\z)", RegexOptions.Singleline);
        return admonition.Success
            ? NormalizeMobilePacketText(admonition.Groups["value"].Value)
            : string.Empty;
    }

    private static IReadOnlyList<string> ExtractMobileNoteSectionBullets(string content, string heading, int maxItems)
    {
        var match = Regex.Match(
            content,
            $@"(?ms)^###\s+{Regex.Escape(heading)}\s*(?<body>.*?)(?=^###\s+|^##\s+|\z)");
        if (!match.Success)
        {
            return [];
        }

        return match.Groups["body"].Value
            .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => line.StartsWith("-", StringComparison.Ordinal))
            .Select(line => NormalizeMobilePacketText(line.TrimStart('-', ' ')))
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Take(maxItems)
            .ToList();
    }

    private static string NormalizeMobilePacketText(string value)
    {
        var cleaned = Regex.Replace(value, @"(?m)^>\s?", string.Empty);
        cleaned = Regex.Replace(cleaned, @"\*\*(?<text>.*?)\*\*", "${text}");
        cleaned = Regex.Replace(cleaned, @"\s+", " ");
        return cleaned.Trim();
    }

    private static string BlankIfMissing(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "not listed" : value.Trim();
    }

    private ChartContext? ResolveMentionedMobileChart(string userText)
    {
        if (string.IsNullOrWhiteSpace(userText))
        {
            return null;
        }

        var normalized = NormalizeConfirmation(userText);
        foreach (var patient in _patientRegistryService.GetAllIdentities()
                     .Where(patient => !string.IsNullOrWhiteSpace(patient.PatientDisplayName))
                     .OrderByDescending(patient => patient.PatientDisplayName.Length))
        {
            var displayName = NormalizeConfirmation(patient.PatientDisplayName);
            if (!string.IsNullOrWhiteSpace(displayName) &&
                normalized.Contains(displayName, StringComparison.OrdinalIgnoreCase))
            {
                return new ChartContext(patient.ChartId, patient.PatientDisplayName);
            }

            var nameParts = displayName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var firstName = nameParts.FirstOrDefault();
            var lastName = nameParts.Length > 1 ? nameParts[^1] : string.Empty;
            if (!string.IsNullOrWhiteSpace(firstName) &&
                Regex.IsMatch(normalized, $@"\b{Regex.Escape(firstName)}\b", RegexOptions.IgnoreCase))
            {
                return new ChartContext(patient.ChartId, patient.PatientDisplayName);
            }

            if (DoesTextContainFuzzyFullName(normalized, nameParts))
            {
                return new ChartContext(patient.ChartId, patient.PatientDisplayName);
            }
        }

        return null;
    }

    private static bool DoesTextContainFuzzyFullName(string normalizedText, IReadOnlyList<string> displayNameParts)
    {
        if (displayNameParts.Count < 2)
        {
            return false;
        }

        var firstName = displayNameParts[0];
        var lastName = displayNameParts[^1];
        if (firstName.Length < 3 || lastName.Length < 3)
        {
            return false;
        }

        var words = normalizedText.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (var index = 0; index < words.Length - 1; index++)
        {
            if (!words[index + 1].Equals(lastName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var candidateFirst = words[index];
            if (candidateFirst.Equals(firstName, StringComparison.OrdinalIgnoreCase) ||
                candidateFirst.StartsWith(firstName, StringComparison.OrdinalIgnoreCase) ||
                firstName.StartsWith(candidateFirst, StringComparison.OrdinalIgnoreCase) ||
                CalculateSmallEditDistance(candidateFirst, firstName) <= 1)
            {
                return true;
            }
        }

        return false;
    }

    private static int CalculateSmallEditDistance(string left, string right)
    {
        if (left.Equals(right, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (Math.Abs(left.Length - right.Length) > 1)
        {
            return 2;
        }

        var leftIndex = 0;
        var rightIndex = 0;
        var edits = 0;
        while (leftIndex < left.Length && rightIndex < right.Length)
        {
            if (char.ToLowerInvariant(left[leftIndex]) == char.ToLowerInvariant(right[rightIndex]))
            {
                leftIndex++;
                rightIndex++;
                continue;
            }

            edits++;
            if (edits > 1)
            {
                return edits;
            }

            if (left.Length > right.Length)
            {
                leftIndex++;
            }
            else if (right.Length > left.Length)
            {
                rightIndex++;
            }
            else
            {
                leftIndex++;
                rightIndex++;
            }
        }

        if (leftIndex < left.Length || rightIndex < right.Length)
        {
            edits++;
        }

        return edits;
    }

    private static bool IsMobileCurrentChartQuestion(string value)
    {
        var normalized = NormalizeConfirmation(value);

        return normalized.Contains("whose chart", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("which chart", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("what chart", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("current chart", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("active chart", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("are we currently in", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("who are we looking at", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("who am i looking at", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMobileChartFocusRequest(string value)
    {
        var normalized = NormalizeConfirmation(value);

        return normalized.Contains("look at", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("open", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("switch", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("focus", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("pull up", StringComparison.OrdinalIgnoreCase);
    }

    private string? DetectUnknownMobileRegistryName(string userText)
    {
        if (string.IsNullOrWhiteSpace(userText))
        {
            return null;
        }

        var knownNames = _patientRegistryService.GetAllIdentities()
            .Where(patient => !string.IsNullOrWhiteSpace(patient.PatientDisplayName))
            .SelectMany(patient =>
            {
                var normalizedName = NormalizeConfirmation(patient.PatientDisplayName);
                var parts = normalizedName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                return parts.Concat([normalizedName]);
            })
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in Regex.Matches(userText, @"\b(?:who is|who's|name of|by the name of)\s+([A-Z][A-Za-z'’-]{2,}(?:\s+[A-Z][A-Za-z'’-]{2,})?)", RegexOptions.IgnoreCase))
        {
            var candidate = match.Groups[1].Value.Trim(' ', '.', ',', '?', '!');
            var normalizedCandidate = NormalizeConfirmation(candidate);
            if (string.IsNullOrWhiteSpace(normalizedCandidate) ||
                knownNames.Contains(normalizedCandidate) ||
                IsIgnoredMobileNameCandidate(normalizedCandidate))
            {
                continue;
            }

            return candidate;
        }

        return null;
    }

    private static bool IsIgnoredMobileNameCandidate(string normalizedCandidate)
    {
        return normalizedCandidate is "dolly" or "vita" or "vitamr" or "patient" or "chart" or "record" or "records";
    }

    private static bool TryResolveMobilePacketRequest(string userText, out string packetType, out string mode)
    {
        var normalized = NormalizeConfirmation(userText);
        mode = normalized.Contains("synth", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("summary", StringComparison.OrdinalIgnoreCase)
            ? "synthesized"
            : "current";

        var wantsPhonePacket =
            normalized.Contains("send", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("load", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("save", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("put", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("packet", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("to my phone", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("to the app", StringComparison.OrdinalIgnoreCase);

        if (wantsPhonePacket &&
            Regex.IsMatch(normalized, @"\b(vaccine|vaccines|immunization|immunizations|shots?)\b", RegexOptions.IgnoreCase))
        {
            packetType = "vaccines";
            return true;
        }

        if (wantsPhonePacket &&
            Regex.IsMatch(normalized, @"\b(lab|labs|imaging|xray|x ray|mri|ct|note|notes|question|questions)\b", RegexOptions.IgnoreCase))
        {
            packetType = NormalizeMobilePacketType(normalized);
            return true;
        }

        packetType = string.Empty;
        return false;
    }

    private static string NormalizeMobilePacketType(string value)
    {
        var normalized = NormalizeConfirmation(value);
        if (Regex.IsMatch(normalized, @"\b(vaccine|vaccines|immunization|immunizations|shots?)\b", RegexOptions.IgnoreCase))
        {
            return "vaccines";
        }

        if (Regex.IsMatch(normalized, @"\b(lab|labs)\b", RegexOptions.IgnoreCase))
        {
            return "labs";
        }

        if (Regex.IsMatch(normalized, @"\b(imaging|xray|x ray|mri|ct)\b", RegexOptions.IgnoreCase))
        {
            return "imaging";
        }

        if (Regex.IsMatch(normalized, @"\b(note|notes)\b", RegexOptions.IgnoreCase))
        {
            return "notes";
        }

        if (Regex.IsMatch(normalized, @"\b(question|questions)\b", RegexOptions.IgnoreCase))
        {
            return "questions";
        }

        return normalized;
    }

    private static string ToMobilePacketTitle(string packetType, string mode = "current")
    {
        var normalizedMode = NormalizeConfirmation(mode);
        return NormalizeMobilePacketType(packetType) switch
        {
            "labs" => normalizedMode.Contains("latest", StringComparison.OrdinalIgnoreCase) ? "Latest Labs" : "Labs",
            "imaging" => normalizedMode.Contains("recent", StringComparison.OrdinalIgnoreCase) ? "Recent Imaging" : "Imaging",
            "notes" when normalizedMode.Contains("card", StringComparison.OrdinalIgnoreCase) => "Last Cardiology Note",
            "notes" when normalizedMode.Contains("psych", StringComparison.OrdinalIgnoreCase) => "Last Psychiatry Note",
            "notes" when normalizedMode.Contains("pcp", StringComparison.OrdinalIgnoreCase) ||
                         normalizedMode.Contains("primary", StringComparison.OrdinalIgnoreCase) => "Last PCP Note",
            "notes" when normalizedMode.Contains("recent", StringComparison.OrdinalIgnoreCase) => "Recent Notes",
            "notes" => "Notes",
            "vaccines" => "Vaccines",
            "questions" => "Questions",
            "other" => string.IsNullOrWhiteSpace(mode) || mode.Equals("current", StringComparison.OrdinalIgnoreCase)
                ? "Other Record"
                : $"Requested Record - {TruncateForTitle(mode, 36)}",
            _ => "Chart packet"
        };
    }

    private static string TruncateForTitle(string value, int maxLength)
    {
        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : $"{trimmed[..maxLength]}...";
    }

    private static bool IsYesConfirmation(string value)
    {
        var normalized = NormalizeConfirmation(value);
        return normalized is "yes" or "y" or "yeah" or "yep" or "confirm" or "switch" or "switch chart" ||
               normalized.StartsWith("yes ", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("switch to", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsNoConfirmation(string value)
    {
        var normalized = NormalizeConfirmation(value);
        return normalized is "no" or "n" or "nope" or "cancel" or "stay" or "stay here" ||
               normalized.StartsWith("no ", StringComparison.OrdinalIgnoreCase);
    }

    private static MobileChatResponse BuildMobileChatResponse(
        string requestId,
        string status,
        string route,
        IEnumerable<string> lines,
        ChartContext? chartContext = null,
        MobileChartPacketResponse? packet = null)
    {
        return new MobileChatResponse
        {
            RequestId = requestId,
            Status = status,
            Route = route,
            Lines = lines.ToList(),
            Reply = string.Join("\n\n", lines.Where(line => !string.IsNullOrWhiteSpace(line))),
            ActiveChartId = chartContext?.ChartId ?? string.Empty,
            ActivePatientDisplayName = chartContext?.LocalDisplayName ?? string.Empty,
            Packet = packet
        };
    }

    public async Task HandleMobileCaptureAsync(
        MobileCaptureRequest request,
        MobileTrustedDevice device,
        string savedPath,
        int byteCount,
        string captureId,
        CancellationToken cancellationToken = default)
    {
        if (!Application.Current.Dispatcher.CheckAccess())
        {
            var operation = Application.Current.Dispatcher.InvokeAsync(
                () => HandleMobileCaptureAsync(request, device, savedPath, byteCount, captureId, cancellationToken),
                DispatcherPriority.Normal,
                cancellationToken);
            await operation.Task.Unwrap();
            return;
        }

        var chartContext = ResolveMobileChartContext(request.ChartId);
        var captureType = string.IsNullOrWhiteSpace(request.CaptureType)
            ? "attachment"
            : request.CaptureType.Trim();
        var note = request.Note?.Trim() ?? string.Empty;

        IngestResult? ingestResult = null;
        if (File.Exists(savedPath))
        {
            _vaultIngestService.VaultRoot = VaultRootPath;
            ingestResult = _vaultIngestService.IngestRawFiles(chartContext, [savedPath]).FirstOrDefault();
        }

        var displayName = ingestResult?.DisplayName ?? Path.GetFileName(savedPath);
        var attachmentPath = ingestResult?.RawVaultPath ?? savedPath;
        var sizeLine = byteCount > 1024 * 1024
            ? $"{byteCount / 1024d / 1024d:0.0} MB"
            : $"{Math.Max(1, byteCount / 1024)} KB";
        var inboxItem = new PhoneInboxItemViewModel(
            captureId,
            DateTimeOffset.Now.ToString("O"),
            chartContext.ChartId,
            chartContext.LocalDisplayName,
            captureType,
            string.IsNullOrWhiteSpace(note) ? $"Attachment saved: {displayName}" : note,
            displayName,
            PhoneInboxWaitingStatus,
            attachmentPath);
        inboxItem.MasterHuntHint = BuildPhoneInboxMasterHuntHint(inboxItem);
        PhoneInboxItems.Insert(0, inboxItem);

        var userLines = new List<string>
        {
            $"Android sent a {captureType} attachment: {displayName}",
            $"Saved to {chartContext.LocalDisplayName}'s chart raw folder. Size: {sizeLine}."
        };
        if (!string.IsNullOrWhiteSpace(note))
        {
            userLines.Add($"Message from phone: {note}");
        }

        AddArchivedMessage(
            MessageAuthor.User,
            $"Android - {DateTime.Now:h:mm tt}",
            userLines,
            [new AttachmentViewModel(attachmentPath)],
            mode: "mobile_capture_received",
            linkedChartId: chartContext.ChartId);

        var dollyLines = string.IsNullOrWhiteSpace(note)
            ? new[]
            {
                "I received the attachment from your phone and saved the original to the chart.",
                "What would you like me to do with it: save only, extract text, summarize, or attach it to a specific note?"
            }
            : new[]
            {
                "I received the attachment and your message from the phone.",
                "I saved the original to the chart. If you want extraction or a chart update from it, I will keep that behind the usual review step."
            };

        AddArchivedMessage(
            MessageAuthor.Dolly,
            AgentLabel,
            dollyLines,
            mode: "mobile_capture_ack",
            linkedChartId: chartContext.ChartId);

        _chronosLedgerService.RecordEvent(
            VaultRootPath,
            chartContext,
            "mobile_capture_received",
            $"{captureType} attachment {captureId} saved as {displayName}.",
            device.DeviceName,
            "mobile");

        RefreshPreview();
    }

    private async void SendMessage()
    {
        if (!CanSendMessage())
        {
            return;
        }

        if (IsRepairSetupRequest(PromptText))
        {
            HandleSetupRepairCommand();
            return;
        }

        if (IsRunSetupRequest(PromptText))
        {
            StartInitialSetupFromCommand();
            return;
        }

        if (IsInitialSetupActive)
        {
            HandleInitialSetupMessage();
            return;
        }

        if (IsThinking)
        {
            HandleConcurrentChatMessage();
            return;
        }

        var userText = string.IsNullOrWhiteSpace(PromptText)
            ? "Uploaded file for Dolly review."
            : PromptText.Trim();

        var sentAttachments = Attachments.ToList();
        IsThinking = true;

        AddArchivedMessage(
            MessageAuthor.User,
            $"User - {DateTime.Now:h:mm tt}",
            [userText],
            sentAttachments);
        PromptText = string.Empty;

        var thinkingMessage = new ChatMessageViewModel(
            MessageAuthor.Dolly,
            AgentLabel,
            ["I am checking this through the local chart intake first. If it needs backend review, I will keep you posted while it runs."]);

        Messages.Add(thinkingMessage);

        try
        {
        if (_pendingSetupRepairConfirmation && sentAttachments.Count == 0)
        {
            HandleSetupRepairConfirmation(userText, thinkingMessage);
            return;
        }

        if (sentAttachments.Count == 0 &&
            TryHandlePendingPatientDeleteResponse(userText.Trim(), thinkingMessage))
        {
            return;
        }

        if (IsCleanupChartsRequest(userText))
        {
            Messages.Remove(thinkingMessage);
            AddArchivedMessage(
                MessageAuthor.Dolly,
                AgentLabel,
                BuildChartCleanupLines(),
                mode: "chart_cleanup_review");
            IsThinking = false;
            RefreshPreview();
            SendMessageCommand.RaiseCanExecuteChanged();
            return;
        }

        if (sentAttachments.Count == 0 &&
            TryHandleHealthspanModeCommand(userText, out var healthspanModeLines))
        {
            Messages.Remove(thinkingMessage);
            AddArchivedMessage(
                MessageAuthor.Dolly,
                AgentLabel,
                healthspanModeLines,
                mode: "healthspan_mode_updated",
                linkedChartId: ActiveChartId);
            IsThinking = false;
            RefreshPreview();
            SendMessageCommand.RaiseCanExecuteChanged();
            return;
        }

        if (sentAttachments.Count == 0 &&
            TryHandleHealthspanMotivationLadder(userText, out var motivationLines))
        {
            Messages.Remove(thinkingMessage);
            AddArchivedMessage(
                MessageAuthor.Dolly,
                AgentLabel,
                motivationLines,
                mode: "healthspan_motivation_ladder",
                linkedChartId: ActiveChartId);
            IsThinking = false;
            RefreshPreview();
            SendMessageCommand.RaiseCanExecuteChanged();
            return;
        }

        if (sentAttachments.Count == 0 && IsDreamRunnerRequest(userText))
        {
            await RunDreamRunnerFromGemmaPacketAsync(thinkingMessage, GetActiveChart());
            return;
        }

        if (TryApplyChartManagerProfileUpdate(userText, sentAttachments, out var managerProfileLines))
        {
            Attachments.Clear();
            Messages.Remove(thinkingMessage);
            AddArchivedMessage(
                MessageAuthor.Dolly,
                AgentLabel,
                managerProfileLines,
                mode: "chart_manager_profile_updated");
            IsThinking = false;
            RefreshPreview();
            SendMessageCommand.RaiseCanExecuteChanged();
            return;
        }

        if (SelectedOmniboxAction == "Delete Patient File")
        {
            HandleDeletePatientWorkflow(userText, thinkingMessage);
            return;
        }

        ChartContext? forcedWorkflowChart = null;

        if (SelectedOmniboxAction == "Add New Patient")
        {
            var intake = await HandleAddNewPatientWorkflowAsync(userText, sentAttachments, thinkingMessage);

            if (!intake.CanProceed)
            {
                return;
            }

            forcedWorkflowChart = intake.ChartContext;
            userText = intake.SourceText;
        }

        var confirmedTypedCapture = false;
        var typedCaptureText = string.Empty;
        ChartContext? confirmedTypedCaptureChart = null;
        PatientNameResolution patientNameResolution;

        if (forcedWorkflowChart is not null)
        {
            patientNameResolution = new PatientNameResolution(
                true,
                forcedWorkflowChart,
                string.Empty,
                new OmniboxPreflightResult
                {
                    IsAvailable = true,
                    ProcessingRequired = true,
                    IntendedAction = "add_patient",
                    RecommendedMode = "thinking",
                    DollyReply = "I have the required new-patient details. I am creating the chart and moving any attached notes through the privacy path now."
                },
                false);
        }
        else if (_pendingTypedCapture is null &&
                 sentAttachments.Count == 0)
        {
            patientNameResolution = BuildAgenticTextResolution(GetActiveChart());
        }
        else
        {
            VaultWriteStatus = "Checking Dolly preflight";
            RefreshPreview();
            SendMessageCommand.RaiseCanExecuteChanged();
            await Task.Yield();

            patientNameResolution = await ResolvePatientForSendAsync(userText, sentAttachments);
        }

        if (!patientNameResolution.CanProceed && sentAttachments.Count > 0)
        {
            Messages.Remove(thinkingMessage);
            AddArchivedMessage(
                MessageAuthor.Dolly,
                AgentLabel,
                [patientNameResolution.Message]);
            IsThinking = false;
            RefreshPreview();
            SendMessageCommand.RaiseCanExecuteChanged();
            return;
        }

        UpdateDollyStatus(thinkingMessage, BuildPreflightStatusMessage(patientNameResolution.Preflight));

        if (_pendingCareGapResolution is not null && sentAttachments.Count == 0)
        {
            if (IsAffirmative(userText))
            {
                var resultLines = ApplyCareGapResolution(_pendingCareGapResolution);
                var resolutionChartContext = _pendingCareGapResolution.ChartContext;
                _pendingCareGapResolution = null;
                Messages.Remove(thinkingMessage);
                AddArchivedMessage(
                    MessageAuthor.Dolly,
                    AgentLabel,
                    resultLines,
                    mode: "care_gap_resolution_applied",
                    linkedChartId: resolutionChartContext.ChartId);
                TrackActionPacketVisibility(
                    resolutionChartContext,
                    "executed",
                    $"care_gap_resolution_applied: {TruncateForChronos(string.Join(" ", resultLines), 140)}");
                QueueBackgroundSummaryRefresh(resolutionChartContext, "care gap resolved");
                IsThinking = false;
                RefreshPreview();
                SendMessageCommand.RaiseCanExecuteChanged();
                return;
            }

            if (IsNegative(userText))
            {
                _pendingCareGapResolution = null;
                Messages.Remove(thinkingMessage);
                AddArchivedMessage(
                    MessageAuthor.Dolly,
                    AgentLabel,
                    ["Okay. I did not change the pending item."],
                    mode: "care_gap_resolution_canceled");
                TrackActionPacketVisibility(
                    GetActiveChart(),
                    "canceled",
                    "care_gap_resolution_canceled: user declined the pending chart edit.");
                IsThinking = false;
                RefreshPreview();
                SendMessageCommand.RaiseCanExecuteChanged();
                return;
            }

            Messages.Remove(thinkingMessage);
            AddArchivedMessage(
                MessageAuthor.Dolly,
                AgentLabel,
                ["I still have that pending-item change paused. Reply yes to apply it, or no to leave the chart unchanged."],
                mode: "care_gap_resolution_waiting");
            TrackActionPacketVisibility(
                _pendingCareGapResolution.ChartContext,
                "awaiting_confirmation",
                "care_gap_resolution_waiting: waiting for yes or no.");
            IsThinking = false;
            RefreshPreview();
            SendMessageCommand.RaiseCanExecuteChanged();
            return;
        }

        if (_pendingVaccineUpdate is not null && sentAttachments.Count == 0)
        {
            if (IsAffirmative(userText))
            {
                var resultLines = ApplyVaccineUpdate(_pendingVaccineUpdate);
                var vaccineChartContext = _pendingVaccineUpdate.ChartContext;
                _pendingVaccineUpdate = null;
                Messages.Remove(thinkingMessage);
                AddArchivedMessage(
                    MessageAuthor.Dolly,
                    AgentLabel,
                    resultLines,
                    mode: "vaccine_record_applied",
                    linkedChartId: vaccineChartContext.ChartId);
                TrackActionPacketVisibility(
                    vaccineChartContext,
                    "executed",
                    $"vaccine_record_applied: {TruncateForChronos(string.Join(" ", resultLines), 140)}");
                QueueBackgroundSummaryRefresh(vaccineChartContext, "vaccine record added");
                IsThinking = false;
                RefreshPreview();
                SendMessageCommand.RaiseCanExecuteChanged();
                return;
            }

            if (IsNegative(userText))
            {
                _pendingVaccineUpdate = null;
                Messages.Remove(thinkingMessage);
                AddArchivedMessage(
                    MessageAuthor.Dolly,
                    AgentLabel,
                    ["Okay. I did not add the vaccine record."],
                    mode: "vaccine_record_canceled");
                TrackActionPacketVisibility(
                    GetActiveChart(),
                    "canceled",
                    "vaccine_record_canceled: user declined the pending vaccine record.");
                IsThinking = false;
                RefreshPreview();
                SendMessageCommand.RaiseCanExecuteChanged();
                return;
            }

            Messages.Remove(thinkingMessage);
            AddArchivedMessage(
                MessageAuthor.Dolly,
                AgentLabel,
                ["I still have that vaccine record paused. Reply yes to add it, or no to leave the chart unchanged."],
                mode: "vaccine_record_waiting");
            TrackActionPacketVisibility(
                _pendingVaccineUpdate.ChartContext,
                "awaiting_confirmation",
                "vaccine_record_waiting: waiting for yes or no.");
            IsThinking = false;
            RefreshPreview();
            SendMessageCommand.RaiseCanExecuteChanged();
            return;
        }

        if (_pendingReminderUpdate is not null && sentAttachments.Count == 0)
        {
            if (IsAffirmative(userText))
            {
                var resultLines = ApplyReminderUpdate(_pendingReminderUpdate);
                _pendingReminderUpdate = null;
                Messages.Remove(thinkingMessage);
                AddArchivedMessage(
                    MessageAuthor.Dolly,
                    AgentLabel,
                    resultLines,
                    mode: "user_reminder_applied",
                    linkedChartId: ActiveChartId);
                TrackActionPacketVisibility(
                    GetActiveChart(),
                    "executed",
                    $"user_reminder_applied: {TruncateForChronos(string.Join(" ", resultLines), 140)}");
                IsThinking = false;
                RefreshPreview();
                SendMessageCommand.RaiseCanExecuteChanged();
                return;
            }

            if (IsNegative(userText))
            {
                _pendingReminderUpdate = null;
                Messages.Remove(thinkingMessage);
                AddArchivedMessage(
                    MessageAuthor.Dolly,
                    AgentLabel,
                    ["Okay. I did not add that reminder."],
                    mode: "user_reminder_canceled");
                TrackActionPacketVisibility(
                    GetActiveChart(),
                    "canceled",
                    "user_reminder_canceled: user declined the pending reminder.");
                IsThinking = false;
                RefreshPreview();
                SendMessageCommand.RaiseCanExecuteChanged();
                return;
            }

            Messages.Remove(thinkingMessage);
            AddArchivedMessage(
                MessageAuthor.Dolly,
                AgentLabel,
                ["I still have that reminder paused. Reply yes to add it, or no to cancel."],
                mode: "user_reminder_waiting");
            TrackActionPacketVisibility(
                GetActiveChart(),
                "awaiting_confirmation",
                "user_reminder_waiting: waiting for yes or no.");
            IsThinking = false;
            RefreshPreview();
            SendMessageCommand.RaiseCanExecuteChanged();
            return;
        }

        if (_pendingTypedCapture is not null && sentAttachments.Count == 0)
        {
            if (IsAffirmative(userText))
            {
                confirmedTypedCapture = true;
                typedCaptureText = _pendingTypedCapture.SourceText;
                confirmedTypedCaptureChart = _pendingTypedCapture.ChartContext;
                TrackActionPacketVisibility(
                    confirmedTypedCaptureChart,
                    "executing",
                    "store_patient_update_confirmed: user approved the typed chart note.");
                _pendingTypedCapture = null;
                UpdateDollyStatus(thinkingMessage, "I have the confirmation. I am storing the paused typed note through the local scrub and privacy gates now.");
            }
            else if (IsNegative(userText))
            {
                _pendingTypedCapture = null;
                PromptText = string.Empty;
                Messages.Remove(thinkingMessage);
                AddArchivedMessage(
                    MessageAuthor.Dolly,
                    AgentLabel,
                    ["Okay. I did not store that typed note."]);
                TrackActionPacketVisibility(
                    GetActiveChart(),
                    "canceled",
                    "store_patient_update_canceled: user declined the paused typed note.");
                IsThinking = false;
                RefreshPreview();
                SendMessageCommand.RaiseCanExecuteChanged();
                return;
            }
            else if (LooksLikeReplacementTypedCapture(userText))
            {
                _pendingTypedCapture = null;
            }
            else
            {
                PromptText = string.Empty;
                Messages.Remove(thinkingMessage);
                AddArchivedMessage(
                    MessageAuthor.Dolly,
                    AgentLabel,
                    ["I still have that typed note paused. Should I store it in the chart? Please reply yes or no."]);
                IsThinking = false;
                RefreshPreview();
                SendMessageCommand.RaiseCanExecuteChanged();
                return;
            }
        }

        if (sentAttachments.Count == 0)
        {
            if (_pendingPhotoUpdate is not null)
            {
                _pendingPhotoUpdate = null;
                Messages.Remove(thinkingMessage);
                AddArchivedMessage(
                    MessageAuthor.Dolly,
                    AgentLabel,
                    ["Okay. I canceled the photo update."],
                    mode: "patient_photo_canceled");
                IsThinking = false;
                RefreshPreview();
                SendMessageCommand.RaiseCanExecuteChanged();
                return;
            }

            _pendingTypedCapture = null;

            var activeChart = patientNameResolution.ChartContext ?? GetActiveChart();
            if (_pendingDataHunterBasicChartId.Equals(activeChart.ChartId, StringComparison.OrdinalIgnoreCase) &&
                TryHandleDataHunterSessionControl(activeChart, userText, out var controlLines))
            {
                Messages.Remove(thinkingMessage);
                AddArchivedMessage(
                    MessageAuthor.Dolly,
                    AgentLabel,
                    controlLines,
                    mode: "data_hunter_session_control",
                    linkedChartId: activeChart.ChartId);
                IsThinking = false;
                RefreshPreview();
                SendMessageCommand.RaiseCanExecuteChanged();
                return;
            }

            if (_pendingDataHunterExpansionChartId.Equals(activeChart.ChartId, StringComparison.OrdinalIgnoreCase) &&
                TryCaptureDataHunterExpansionAnswer(activeChart, userText, "desktop_chat", out var expansionLines))
            {
                Messages.Remove(thinkingMessage);
                AddArchivedMessage(
                    MessageAuthor.Dolly,
                    AgentLabel,
                    expansionLines,
                    mode: "data_hunter_expansion_answer_saved",
                    linkedChartId: activeChart.ChartId);
                IsThinking = false;
                RefreshPreview();
                SendMessageCommand.RaiseCanExecuteChanged();
                return;
            }

            if (_pendingDataHunterBasicChartId.Equals(activeChart.ChartId, StringComparison.OrdinalIgnoreCase) &&
                TryCaptureDataHunterBasicAnswer(activeChart, userText, "desktop_chat", out var dataHunterLines))
            {
                if (GetNextDataHunterBasicQuestionIndex(LoadDataHunterPersonalDataMap(activeChart)) is null)
                {
                    _pendingDataHunterBasicChartId = string.Empty;
                }

                Messages.Remove(thinkingMessage);
                AddArchivedMessage(
                    MessageAuthor.Dolly,
                    AgentLabel,
                    dataHunterLines,
                    mode: "data_hunter_basic_answer_saved",
                    linkedChartId: activeChart.ChartId);
                IsThinking = false;
                RefreshPreview();
                SendMessageCommand.RaiseCanExecuteChanged();
                return;
            }

            if (IsApiDollyAgentFirst(patientNameResolution.Preflight))
            {
                await HandleTypedOmniboxWithLocalGemmaAsync(userText, thinkingMessage, patientNameResolution);
                return;
            }

            if (IsVaultRosterQuestion(patientNameResolution.Preflight) || IsRosterQuestionText(userText))
            {
                AnswerFamilyRosterQuestion(thinkingMessage);
                return;
            }

            if (IsVaultQuestion(patientNameResolution.Preflight) || LooksLikeChartQuestionText(userText))
            {
                var readOnlyResolution = EnsureReadOnlyChartResolution(patientNameResolution);
                await HandleTypedOmniboxWithBackendAsync(userText, thinkingMessage, readOnlyResolution, skipPatchPlanner: true);
                return;
            }

            if (patientNameResolution.ShouldConfirmTypedCapture)
            {
                _pendingTypedCapture = new PendingTypedCapture(userText, patientNameResolution.ChartContext!);
                PromptText = string.Empty;
                Messages.Remove(thinkingMessage);
                AddArchivedMessage(
                    MessageAuthor.Dolly,
                    AgentLabel,
                    [
                        $"This looks like medical chart information for {patientNameResolution.ChartContext!.LocalDisplayName}. Should I store it in that chart?",
                        "I have not written anything yet. Reply yes to store, no to cancel, or paste a corrected replacement note."
                    ],
                    mode: "capture_confirmation",
                    linkedChartId: patientNameResolution.ChartContext!.ChartId);
                IsThinking = false;
                RefreshPreview();
                SendMessageCommand.RaiseCanExecuteChanged();
                return;
            }

            if (patientNameResolution.Preflight?.IsAvailable != true)
            {
                PromptText = string.Empty;
                Messages.Remove(thinkingMessage);
                AddArchivedMessage(
                    MessageAuthor.Dolly,
                    AgentLabel,
                    [BuildConversationalDollyReply(patientNameResolution.Preflight, userText)],
                    mode: "conversation_fallback",
                    linkedChartId: ActiveChartId);
                IsThinking = false;
                RefreshPreview();
                SendMessageCommand.RaiseCanExecuteChanged();
                return;
            }

            await HandleTypedOmniboxWithLocalGemmaAsync(userText, thinkingMessage, patientNameResolution);
            return;
        }

        if (_pendingPhotoUpdate is not null)
        {
            if (TryApplyPatientPhotoUpdate(sentAttachments, out var photoLines))
            {
                _pendingPhotoUpdate = null;
                PromptText = string.Empty;
                Attachments.Clear();
                Messages.Remove(thinkingMessage);
                AddArchivedMessage(
                    MessageAuthor.Dolly,
                    AgentLabel,
                    photoLines,
                    mode: "patient_photo_updated",
                    linkedChartId: ActiveChartId);
                IsThinking = false;
                RefreshPreview();
                SendMessageCommand.RaiseCanExecuteChanged();
                return;
            }

            Messages.Remove(thinkingMessage);
            AddArchivedMessage(
                MessageAuthor.Dolly,
                AgentLabel,
                ["I need an image file for the chart photo. Please attach a JPG, PNG, GIF, BMP, or WEBP image."],
                sentAttachments,
                mode: "patient_photo_waiting");
            IsThinking = false;
            RefreshPreview();
            SendMessageCommand.RaiseCanExecuteChanged();
            return;
        }

        if (!confirmedTypedCapture &&
            sentAttachments.Count == 0 &&
            (IsVaultRosterQuestion(patientNameResolution.Preflight) || IsRosterQuestionText(userText)))
        {
            VaultWriteStatus = "Reading family vault roster";
            UpdateDollyStatus(thinkingMessage, "I am checking the family-vault roster now. I will answer from the sealed registry and allowed sterile context.");
            RefreshPreview();
            var rosterContext = _vaultContextService.BuildFamilyVaultRosterContext(
                VaultRootPath,
                _patientRegistryService.GetAllIdentities());
            VaultWriteStatus = "Asking Dolly from roster";
            RefreshPreview();
            var answer = await _dollyVaultChatService.AnswerFromVaultAsync(
                LocalModelEndpoint,
                LocalModelName,
                userText,
                BuildRecentConversationContext(),
                rosterContext);

            Messages.Remove(thinkingMessage);
            AddArchivedMessage(
                MessageAuthor.Dolly,
                AgentLabel,
                [answer],
                mode: "vault_roster");
            VaultWriteStatus = "Vault roster read only";
            IsThinking = false;
            RefreshPreview();
            SendMessageCommand.RaiseCanExecuteChanged();
            return;
        }

        if (!confirmedTypedCapture &&
            sentAttachments.Count == 0 &&
            IsVaultQuestion(patientNameResolution.Preflight))
        {
            VaultWriteStatus = "Reading sterile context";
            UpdateDollyStatus(thinkingMessage, "I found this as a chart question. I am pulling the allowed sterile context now, not raw files.");
            RefreshPreview();
            var vaultContext = _vaultContextService.BuildSterileContext(
                VaultRootPath,
                patientNameResolution.ChartContext!,
                userText);
            VaultWriteStatus = "Asking Dolly from vault";
            UpdateDollyStatus(thinkingMessage, "I have the sterile context packet. I am using that to answer from the chart.");
            RefreshPreview();
            var answer = await _dollyVaultChatService.AnswerFromVaultAsync(
                LocalModelEndpoint,
                LocalModelName,
                userText,
                BuildRecentConversationContext(),
                vaultContext);

            ActiveChartId = patientNameResolution.ChartContext!.ChartId;
            ActivePatientDisplayName = patientNameResolution.ChartContext.LocalDisplayName;
            PromptText = string.Empty;
            Messages.Remove(thinkingMessage);
            AddArchivedMessage(
                MessageAuthor.Dolly,
                AgentLabel,
                [answer],
                mode: "vault_question",
                linkedChartId: patientNameResolution.ChartContext!.ChartId);
            VaultWriteStatus = "Vault read only";
            IsThinking = false;
            RefreshPreview();
            SendMessageCommand.RaiseCanExecuteChanged();
            return;
        }

        if (!confirmedTypedCapture &&
            sentAttachments.Count == 0 &&
            patientNameResolution.ShouldConfirmTypedCapture)
        {
            _pendingTypedCapture = new PendingTypedCapture(userText, patientNameResolution.ChartContext!);
            PromptText = string.Empty;
            Messages.Remove(thinkingMessage);
            AddArchivedMessage(
                MessageAuthor.Dolly,
                AgentLabel,
                [
                    $"This looks like medical chart information for {patientNameResolution.ChartContext!.LocalDisplayName}. Should I store it in that chart?",
                    "I have not written anything yet. Reply yes to store, no to cancel, or paste a corrected replacement note."
                ],
                mode: "capture_confirmation",
                linkedChartId: patientNameResolution.ChartContext!.ChartId);
            IsThinking = false;
            RefreshPreview();
            SendMessageCommand.RaiseCanExecuteChanged();
            return;
        }

        if (!confirmedTypedCapture &&
            sentAttachments.Count == 0 &&
            !patientNameResolution.ShouldConfirmTypedCapture)
        {
            PromptText = string.Empty;
            Messages.Remove(thinkingMessage);
            AddArchivedMessage(
                MessageAuthor.Dolly,
                AgentLabel,
                [BuildConversationalDollyReply(patientNameResolution.Preflight)]);
            IsThinking = false;
            RefreshPreview();
            SendMessageCommand.RaiseCanExecuteChanged();
            return;
        }

        var chartContext = confirmedTypedCapture
            ? confirmedTypedCaptureChart!
            : patientNameResolution.ChartContext!;
        ActiveChartId = chartContext.ChartId;
        ActivePatientDisplayName = chartContext.LocalDisplayName;
        var ingestTaskId = StartDollyTask(
            chartContext,
            sentAttachments.Count > 0 ? "Attachment ingest" : "Typed source capture",
            sentAttachments.Count > 0 ? "saving raw attachments" : "saving typed source");
        VaultWriteStatus = sentAttachments.Count > 0
            ? "Saving raw attachments"
            : "Saving typed source";
        UpdateDollyStatus(
            thinkingMessage,
            sentAttachments.Count > 0
                ? "I am saving the source into the protected chart intake area before the scrub gates run."
                : "I am capturing this typed chart information locally first, then it will go through the scrub gates.");
        UpdateDollyTask(chartContext, ingestTaskId, "running", "saving protected source", "Raw intake started.");
        RefreshPreview();
        var ingestResults = confirmedTypedCapture
            ? new List<IngestResult>
            {
                _vaultIngestService.IngestRawText(chartContext, typedCaptureText, "Omnibox_Typed_Source.md")
            }
            : sentAttachments.Count > 0
                ? _vaultIngestService.IngestRawFiles(chartContext, sentAttachments.Select(attachment => attachment.Path))
                : [];

        _lastIngestResults.Clear();
        _lastIngestResults.AddRange(ingestResults);
        _chronosLedgerService.RecordEvent(
            VaultRootPath,
            chartContext,
            confirmedTypedCapture ? "typed_source_captured" : "source_ingested",
            ingestResults.Count == 1
                ? $"Captured 1 source: {ingestResults[0].DisplayName}."
                : $"Captured {ingestResults.Count} source(s).",
            "C#",
            confirmedTypedCapture ? "omnibox" : "ingest");

        _lastScrubResults.Clear();
        VaultWriteStatus = "Running local scrubber";
        UpdateDollyStatus(thinkingMessage, "I am making a safe local copy first, then I will read only the cleaned text.");
        UpdateDollyTask(chartContext, ingestTaskId, "running", "local safety pass", "Removing obvious structured identifiers.");
        RefreshPreview();
        _lastScrubResults.AddRange(ingestResults.Select(result => _fastScrubberService.ScrubFile(result.RawVaultPath)));

        _lastImageDocumentReviewResults.Clear();
        VaultWriteStatus = "Checking image gate";
        UpdateDollyStatus(thinkingMessage, "I am checking whether any image or PDF attachments need an official-read safety hold.");
        UpdateDollyTask(chartContext, ingestTaskId, "running", "image/official-read check", "Checking attachment type safety locally.");
        RefreshPreview();
        _lastImageDocumentReviewResults.AddRange(await _imageDocumentReviewService.ReviewImageDocumentsAsync(
            VaultRootPath,
            chartContext,
            LocalModelEndpoint,
            LocalModelName,
            ingestResults));

        _lastEncounterNodeWriteResults.Clear();
        _lastGeminiBackendPipelineResult = null;
        UpdateDollyTask(chartContext, ingestTaskId, "running", "local privacy review", "Local privacy checks are preparing safe text.");
        var apiEligibleScrubbedPayloads = await BuildApiEligibleScrubbedPayloadsWithRetriesAsync(
            thinkingMessage,
            chartContext);
        VaultWriteStatus = "Running agent chart review";
        UpdateDollyStatus(
            thinkingMessage,
            apiEligibleScrubbedPayloads.Count == 0
                ? "The safe text needs more local review, so I am not sending it onward."
                : "I am reviewing the safe chart text now. This can take about a minute.");
        UpdateDollyTask(
            chartContext,
            ingestTaskId,
            "running",
            apiEligibleScrubbedPayloads.Count == 0 ? "agent review skipped" : "agent chart review",
            apiEligibleScrubbedPayloads.Count == 0 ? "No safe text was ready for chart review." : "Safe text sent for chart review.");
        RefreshPreview();
        SetApiLlmState(apiEligibleScrubbedPayloads.Count > 0, apiEligibleScrubbedPayloads.Count > 0 ? "API ingest writer" : "API idle");

        try
        {
            _lastGeminiBackendPipelineResult = await _geminiBackendPipelineService.ProcessScrubbedPayloadsAsync(
                EnableGeminiBackendProcessing,
                GeminiFastModelName,
                GeminiThinkingModelName,
                VaultRootPath,
                chartContext,
                apiEligibleScrubbedPayloads,
                SourceSystem,
                SourceFacility,
                SelectedSourceType,
                async (status, currentStep, resultSummary) =>
                {
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        SetApiLlmState(true, $"API {currentStep}");
                        VaultWriteStatus = currentStep;
                        UpdateDollyTask(chartContext, ingestTaskId, status, currentStep, resultSummary);
                        UpdateDollyStatus(thinkingMessage, $"{currentStep}: {resultSummary}");
                    });
                });
        }
        finally
        {
            SetApiLlmState(false, "API ready");
        }

        _lastEncounterNodeWriteResults.AddRange(_lastGeminiBackendPipelineResult.WriteResults);

        _lastEncounterDraft = ingestResults.Count > 0
            ? _encounterDraftService.CreateMockDraft(chartContext, ingestResults, _lastScrubResults, userText)
            : null;
        _lastDraftValidation = _lastEncounterDraft is null
            ? null
            : _encounterDraftService.Validate(_lastEncounterDraft);

        _lastAuditEntries.Clear();
        VaultWriteStatus = "Writing audit trail";
        UpdateDollyStatus(thinkingMessage, "I have the chart review back. I am writing the audit trail and preparing the summary.");
        UpdateDollyTask(chartContext, ingestTaskId, "running", "writing audit trail", "Backend returned; C# is recording the run.");
        RefreshPreview();
        _lastAuditEntries.AddRange(CreateAuditEntries(
            ingestResults,
            _lastScrubResults,
            _lastSecondPassScrubResults,
            _lastLocalModelReview,
            patientNameResolution.Preflight,
            _lastImageDocumentReviewResults,
            _lastGeminiBackendPipelineResult));
        _ingestAuditService.Append(VaultRootPath, _lastAuditEntries);
        CompleteDollyTask(
            chartContext,
            ingestTaskId,
            "INGEST_PIPELINE_DONE",
            $"Ingested {ingestResults.Count} source(s); wiki writes {_lastGeminiBackendPipelineResult?.WikiWrittenSourcePaths.Count ?? 0}.");
        QueueBackgroundSummaryRefresh(chartContext, "ingest completed");

        VaultWriteStatus = ingestResults.Count > 0
            ? $"Raw saved: {ingestResults.Count}"
            : "Vault write idle";

        PromptText = string.Empty;
        Attachments.Clear();
        IsListening = false;
        OnStatusChanged();
        RefreshPreview();
        SendMessageCommand.RaiseCanExecuteChanged();

        await Task.Delay(150);

        Messages.Remove(thinkingMessage);
        AddArchivedMessage(
            MessageAuthor.Dolly,
            AgentLabel,
            BuildIngestOutcomeMessageLines(
                ingestResults,
                _lastLocalModelReview,
                _lastSecondPassScrubResults,
                _lastGeminiBackendPipelineResult,
                VaultRootPath,
                chartContext),
            mode: confirmedTypedCapture ? "typed_capture_saved" : "ingest_status",
            linkedChartId: ActiveChartId);

        // Keep implementation details in the preview/audit layers; the chat stays Dolly-led.

        IsThinking = false;
        SendMessageCommand.RaiseCanExecuteChanged();
        RefreshPreview();
        }
        catch (Exception exception)
        {
            Messages.Remove(thinkingMessage);
            AddArchivedMessage(
                MessageAuthor.Dolly,
                AgentLabel,
                [
                    "I hit a recoverable problem and stopped this run instead of leaving the chart in limbo.",
                    $"Status: {HumanizeException(exception)}",
                    "The original source, if already saved, was not modified."
                ],
                mode: "run_blocked");

            VaultWriteStatus = "Run blocked safely";
            IsThinking = false;
            RefreshPreview();
            SendMessageCommand.RaiseCanExecuteChanged();
        }
    }

    private void ToggleVoice()
    {
        IsListening = !IsListening;

        if (IsListening && string.IsNullOrWhiteSpace(PromptText))
        {
            PromptText = "Voice input placeholder: ";
        }
    }

    private void TogglePreviewPanel()
    {
        IsPreviewPanelOpen = !IsPreviewPanelOpen;
    }

    private void ClosePreviewPanel()
    {
        IsPreviewPanelOpen = false;
    }

    private void ToggleEvaluationTool()
    {
        IsEvaluationToolOpen = !IsEvaluationToolOpen;
        if (IsEvaluationToolOpen && EvaluationQuestions.Count == 0)
        {
            BuildEvaluationQuestions();
        }
    }

    private void BuildEvaluationQuestions()
    {
        EvaluationQuestions.Clear();
        IReadOnlyList<string> prompts = SelectedEvaluationScenario switch
        {
            "Setup Repair" =>
            [
                "Did Dolly detect the bad setup values without you hunting for them?",
                "Did the repair prompt explain what would change before changing it?",
                "Did setup repair avoid deleting charts automatically?",
                "After repair, did the active chart point to a real chart?",
                "Did the flow feel calm and trustworthy?",
                "Overall setup repair score from 1 to 5"
            ],
            "Admin Dashboard" =>
            [
                "Could you quickly tell who the Chart Manager is?",
                "Could you quickly see the active chart and active user?",
                "Were protected actions understandable?",
                "Did chart cleanup candidates stand out without feeling scary?",
                "Did the dashboard help you understand the system state?",
                "Overall Admin dashboard score from 1 to 5"
            ],
            "Chart Switching" =>
            [
                "Did natural phrases like open/switch/go back route correctly?",
                "Did ambiguous patient names ask for clarification?",
                "Did the visible active chart update everywhere you expected?",
                "Did switching avoid modifying chart files?",
                "Did Dolly's reply stay brief and plain?",
                "Overall chart switching score from 1 to 5"
            ],
            "Document Ingest" =>
            [
                "Did raw capture happen before any chart summary or wiki write?",
                "Did the privacy gate status feel understandable?",
                "Did Dolly provide useful progress without backend clutter?",
                "Did the completion summary tell you what was added?",
                "Did the original source remain untouched?",
                "Overall document ingest score from 1 to 5"
            ],
            "Vaccine Records" =>
            [
                "Did Dolly recognize vaccine wording naturally?",
                "Did Dolly ask before saving vaccine records?",
                "Were approximate years or year ranges preserved?",
                "Did later vaccine questions list the table accurately?",
                "Were duplicates avoided?",
                "Overall vaccine record score from 1 to 5"
            ],
            "Reminders" =>
            [
                "Did Dolly recognize the reminder request naturally?",
                "Did Dolly ask before saving the reminder?",
                "Did the due date land correctly?",
                "Did duplicate reminders get skipped?",
                "Did due reminders surface at session open?",
                "Overall reminders score from 1 to 5"
            ],
            "Privacy Gate" =>
            [
                "Was the local privacy status visible enough?",
                "Did blocked/unavailable states stop safely?",
                "Did Dolly avoid exposing raw source text in chat?",
                "Did audit/status wording feel understandable?",
                "Did the app preserve the raw-first boundary?",
                "Overall privacy gate score from 1 to 5"
            ],
            _ =>
            [
                "Did the feature do what you expected while you were using it?",
                "Did Dolly's wording feel natural and not too technical?",
                "Did you always know which chart or person was active?",
                "Did any action happen without a confirmation you expected?",
                "Did the UI make the next step obvious?",
                "Did anything feel unsafe, confusing, or too hidden?",
                "Did the app preserve privacy and raw-source boundaries?",
                "Would you trust this flow enough to use it again?",
                "What is the biggest weakness you found?",
                "Overall live-use score from 1 to 5"
            ]
        };

        foreach (var prompt in prompts.Take(10))
        {
            EvaluationQuestions.Add(new EvaluationQuestionViewModel(prompt));
        }
    }

    private void SaveEvaluationResult()
    {
        Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "Evaluation_Results"));
        var repoEvaluationRoot = Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "docs",
            "Evaluation",
            "Runs");
        var outputRoot = Directory.Exists(Path.GetFullPath(repoEvaluationRoot))
            ? Path.GetFullPath(repoEvaluationRoot)
            : Path.Combine(AppContext.BaseDirectory, "Evaluation_Results");

        Directory.CreateDirectory(outputRoot);
        var scenarioSlug = BuildSafeFolderName(SelectedEvaluationScenario).Replace('_', '-');
        var path = GetNonConflictingPath(
            outputRoot,
            $"{DateTime.Now:yyyy-MM-dd_HHmmss}_{scenarioSlug}_Human_Evaluation.md");

        var builder = new StringBuilder();
        builder.AppendLine($"# Builder Evaluation: {SelectedEvaluationScenario}");
        builder.AppendLine();
        builder.AppendLine("> Full human evaluation is reserved for a completed VitaMR version after at least one week of real chart use.");
        builder.AppendLine();
        builder.AppendLine($"Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        builder.AppendLine($"Active chart: {ActivePatientDisplayName} ({ActiveChartId})");
        builder.AppendLine($"Chart Manager: {AdminManagerDisplayName}");
        builder.AppendLine($"Agent: {AgentDisplayName}");
        builder.AppendLine();
        builder.AppendLine("## Questions");
        builder.AppendLine();
        builder.AppendLine("| # | Question | Answer | Score / Note |");
        builder.AppendLine("|---:|---|---|---|");

        var index = 1;
        foreach (var question in EvaluationQuestions)
        {
            builder.AppendLine(
                $"| {index} | {EscapeMarkdownTableCell(question.Prompt)} | {EscapeMarkdownTableCell(question.Answer)} | {EscapeMarkdownTableCell(question.ScoreText)} |");
            index++;
        }

        builder.AppendLine();
        builder.AppendLine("## Notes");
        builder.AppendLine();
        builder.AppendLine(string.IsNullOrWhiteSpace(EvaluationNotes) ? "No notes." : EvaluationNotes.Trim());
        File.WriteAllText(path, builder.ToString());

        EvaluationSavedStatus = $"Saved {Path.GetFileName(path)}";
        VaultWriteStatus = "Evaluation saved";
        RefreshPreview();
    }

    private void ShowConversation()
    {
        ClosePreviewPanel();
        if (!_isAdminDashboardOpen && !_isVitaMasteryOpen)
        {
            return;
        }

        _isAdminDashboardOpen = false;
        _isVitaMasteryOpen = false;
        OnPropertyChanged(nameof(ConversationVisibility));
        OnPropertyChanged(nameof(AdminDashboardVisibility));
        OnPropertyChanged(nameof(VitaMasteryVisibility));
    }

    private void ShowAdminDashboard()
    {
        ClosePreviewPanel();
        RefreshAdminDashboard();
        if (_isAdminDashboardOpen && !_isVitaMasteryOpen)
        {
            return;
        }

        _isAdminDashboardOpen = true;
        _isVitaMasteryOpen = false;
        OnPropertyChanged(nameof(ConversationVisibility));
        OnPropertyChanged(nameof(AdminDashboardVisibility));
        OnPropertyChanged(nameof(VitaMasteryVisibility));
    }

    private void ShowVitaMastery()
    {
        RefreshVitaMastery();
        if (_isVitaMasteryOpen && !_isAdminDashboardOpen)
        {
            return;
        }

        _isAdminDashboardOpen = false;
        _isVitaMasteryOpen = true;
        OnPropertyChanged(nameof(ConversationVisibility));
        OnPropertyChanged(nameof(AdminDashboardVisibility));
        OnPropertyChanged(nameof(VitaMasteryVisibility));
    }

    private void RunSetupFromAdmin()
    {
        ShowConversation();
        PromptText = "Run setup";
        SendMessage();
    }

    private void RepairSetupFromAdmin()
    {
        ShowConversation();
        PromptText = "repair setup";
        SendMessage();
    }

    private void ReviewCleanupFromAdmin()
    {
        ShowConversation();
        PromptText = "chart cleanup";
        SendMessage();
    }

    private void FocusChartFromAdmin(object? parameter)
    {
        if (parameter is not AdminChartRowViewModel row)
        {
            return;
        }

        ActiveChartId = row.ChartId;
        ActivePatientDisplayName = row.PatientDisplayName;
        VaultWriteStatus = $"Admin focus set to {ExtractFirstNameForDisplay(row.PatientDisplayName)}";
        RefreshAdminDashboard();
        RefreshPreview();
    }

    private void RefreshAdminDashboard()
    {
        AdminChartRows.Clear();
        foreach (var patient in _patientRegistryService.GetAllIdentities()
                     .Where(patient => !string.IsNullOrWhiteSpace(patient.PatientDisplayName))
                     .OrderBy(patient => patient.PatientDisplayName, StringComparer.OrdinalIgnoreCase))
        {
            var isActive = patient.ChartId.Equals(ActiveChartId, StringComparison.OrdinalIgnoreCase);
            var looksAccidental = LooksLikeAccidentalChartName(patient.PatientDisplayName);
            AdminChartRows.Add(new AdminChartRowViewModel(
                patient.PatientDisplayName,
                patient.ChartId,
                FormatKnownOrMissing(patient.DateOfBirth),
                FirstNonEmpty(patient.RelationshipNotes, "No access note"),
                isActive ? "Active" : "Ready",
                looksAccidental ? $"Cleanup candidate: {BuildChartManagerPendingDeletePhrase()}" : "Protected chart",
                isActive ? "Active" : "Focus"));
        }

        AdminProtectedActions.Clear();
        AdminProtectedActions.Add($"Chart creation requires: {BuildChartManagerCreatePhrase()}");
        AdminProtectedActions.Add("Chart deletion always moves to the 7-day wastebasket first.");

        var setupIssues = DetectSetupRepairIssues();
        AdminProtectedActions.Add(setupIssues.Count == 0
            ? "Setup repair: no obvious accidental values detected."
            : $"Setup repair: {setupIssues.Count} issue(s) need review.");

        var cleanupCandidates = GetAccidentalChartCandidates();
        AdminProtectedActions.Add(cleanupCandidates.Count == 0
            ? "Chart cleanup: no obvious accidental chart names detected."
            : $"Chart cleanup: {cleanupCandidates.Count} possible accidental chart(s).");

        if (_pendingPatientDelete is not null)
        {
            AdminProtectedActions.Add($"Pending deletion confirmation: {BuildChartManagerPendingDeletePhrase()}");
        }

        if (_pendingVaccineUpdate is not null)
        {
            AdminProtectedActions.Add("Pending vaccine update: waiting for user confirmation.");
        }

        if (_pendingReminderUpdate is not null)
        {
            AdminProtectedActions.Add("Pending reminder update: waiting for user confirmation.");
        }

        if (_pendingCareGapResolution is not null)
        {
            AdminProtectedActions.Add("Pending care-gap edit: waiting for user confirmation.");
        }

        OnPropertyChanged(nameof(AdminManagerDisplayName));
        OnPropertyChanged(nameof(AdminManagerNickname));
        OnPropertyChanged(nameof(AdminPersonaPath));
        OnPropertyChanged(nameof(AdminActiveUserDisplay));
        OnPropertyChanged(nameof(AdminAgentDisplay));
        OnPropertyChanged(nameof(AdminSetupStatus));
        OnPropertyChanged(nameof(AdminActiveChartDisplay));
        OnPropertyChanged(nameof(AdminAccessNotes));
        OnPropertyChanged(nameof(ProviderSetupStatus));
        OnPropertyChanged(nameof(ProviderSetupDetail));
    }

    private void SaveSettings()
    {
        _settings.ActiveChartId = NormalizeChartId(_settings.ActiveChartId);
        _settings.ActivePatientDisplayName = string.IsNullOrWhiteSpace(_settings.ActivePatientDisplayName)
            ? _settings.ActiveChartId
            : _settings.ActivePatientDisplayName.Trim();
        _patientRegistryService.SaveIdentityRecord(new PatientIdentityRecord
        {
            ChartId = _settings.ActiveChartId,
            PatientDisplayName = _settings.ActivePatientDisplayName
        });
        _settings.DefaultSourceSystem = NormalizeProvenanceField(_settings.DefaultSourceSystem);
        _settings.DefaultSourceFacility = NormalizeProvenanceField(_settings.DefaultSourceFacility);
        _settings.DefaultSourceType = NormalizeProvenanceField(_settings.DefaultSourceType);
        _settings.HealthspanCoachingMode = string.IsNullOrWhiteSpace(_settings.HealthspanCoachingMode)
            ? "Record Assistant Mode"
            : _settings.HealthspanCoachingMode.Trim();
        if (!_settings.EnableHealthspanMode)
        {
            _settings.HealthspanCoachingMode = "Record Assistant Mode";
        }

        _settings.LocalModelEndpoint = string.IsNullOrWhiteSpace(_settings.LocalModelEndpoint)
            ? "http://localhost:11434"
            : _settings.LocalModelEndpoint.Trim();
        _settings.LocalModelName = string.IsNullOrWhiteSpace(_settings.LocalModelName)
            ? "gemma4:latest"
            : _settings.LocalModelName.Trim();
        _settings.GeminiFastModelName = string.IsNullOrWhiteSpace(_settings.GeminiFastModelName)
            ? "gemini-3.1-flash-lite-preview"
            : _settings.GeminiFastModelName.Trim();
        _settings.GeminiThinkingModelName = string.IsNullOrWhiteSpace(_settings.GeminiThinkingModelName)
            ? "gemini-3-flash-preview"
            : _settings.GeminiThinkingModelName.Trim();
        _settings.SelectedAiProvider = NormalizeProviderDisplayName(_settings.SelectedAiProvider);
        _settings.ChartManagerName = string.IsNullOrWhiteSpace(_settings.ChartManagerName)
            ? (string.IsNullOrWhiteSpace(_settings.VaultOwnerName) ? "Vault Manager" : _settings.VaultOwnerName.Trim())
            : _settings.ChartManagerName.Trim();
        _settings.ChartManagerPassword = string.IsNullOrWhiteSpace(_settings.ChartManagerPassword)
            ? (string.IsNullOrWhiteSpace(_settings.VaultOwnerDeletePassword) ? "Vault Manager" : _settings.VaultOwnerDeletePassword.Trim())
            : _settings.ChartManagerPassword.Trim();
        _settingsService.Save(_settings);
        _vaultIngestService.VaultRoot = _settings.VaultRootPath;
        VaultWriteStatus = "Settings saved";
        RefreshAdminDashboard();
        RefreshPreview();
    }

    private bool CanSaveProviderApiKey()
    {
        return !string.IsNullOrWhiteSpace(ProviderApiKeyInput);
    }

    private void SaveProviderApiKey()
    {
        var provider = NormalizeProviderDisplayName(SelectedAiProvider);
        _providerApiKeyStore.SaveProviderKey(new ProviderApiKeyBundle
        {
            ProviderId = provider,
            ApiKey = ProviderApiKeyInput
        });

        if (provider.Equals("Gemini", StringComparison.OrdinalIgnoreCase))
        {
            _geminiApiKeyStore.Save(new GeminiApiKeyBundle
            {
                FastApiKey = ProviderApiKeyInput,
                ThinkingApiKey = ProviderApiKeyInput
            });
        }

        ProviderApiKeyInput = string.Empty;
        VaultWriteStatus = $"{provider} provider key saved locally";
        OnPropertyChanged(nameof(ProviderSetupStatus));
        OnPropertyChanged(nameof(GeminiKeyStatus));
        RefreshPreview();
    }

    private void ClearProviderApiKey()
    {
        var provider = NormalizeProviderDisplayName(SelectedAiProvider);
        _providerApiKeyStore.ClearProviderKey(provider);
        ProviderApiKeyInput = string.Empty;
        VaultWriteStatus = $"{provider} provider key cleared locally";
        OnPropertyChanged(nameof(ProviderSetupStatus));
        OnPropertyChanged(nameof(GeminiKeyStatus));
        RefreshPreview();
    }

    private void ChooseVaultFolder()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Choose VitaMR vault folder",
            InitialDirectory = Directory.Exists(VaultRootPath)
                ? VaultRootPath
                : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };

        if (dialog.ShowDialog() == true)
        {
            VaultRootPath = dialog.FolderName;
            SaveSettings();
        }
    }

    private void RefreshPreview()
    {
        if (_isAdminDashboardOpen)
        {
            RefreshAdminDashboard();
        }

        PreviewLines.Clear();
        PreviewLines.Add($"Vault root: {VaultRootPath}");
        PreviewLines.Add($"Active chart: {ActiveChartId}");
        PreviewLines.Add($"Local display: {ActivePatientDisplayName}");
        PreviewLines.Add($"Chart file: {ActiveChartPath}");
        PreviewLines.Add("Vault mode: sterile chart folders");
        PreviewLines.Add("Identity registry: local sealed JSON");
        PreviewLines.Add($"Source system: {SourceSystem}");
        PreviewLines.Add($"Source facility: {SourceFacility}");
        PreviewLines.Add($"Source type: {SelectedSourceType}");
        PreviewLines.Add($"Synthetic mode: {IsSyntheticTestMode}");
        PreviewLines.Add($"Model route: {SelectedModel}");
        PreviewLines.Add(HealthspanModeSummary);
        PreviewLines.Add(HealthspanModeDetail);
        PreviewLines.Add($"Life mode: {ActiveLifeMode}");
        PreviewLines.Add(ActiveLifeModeDetail);
        PreviewLines.Add(VitaMasterySummary);
        PreviewLines.Add($"Vita Mastery quests: {VitaMasteryMicroQuestSummary}");
        PreviewLines.Add("Local privacy check: required");
        PreviewLines.Add($"Local helper: {LocalModelReadinessStatus}");
        PreviewLines.Add($"Agent chart review: {(EnableGeminiBackendProcessing ? "enabled" : "disabled")}");
        PreviewLines.Add(ProviderSetupStatus);
        PreviewLines.Add(GeminiKeyStatus);
        PreviewLines.Add($"Typed characters: {PromptText.Length}");
        PreviewLines.Add($"Attachments queued: {Attachments.Count}");
        PreviewLines.Add($"Phone inbox: {PhoneInboxItems.Count} item(s)");
        PreviewLines.Add($"Personal Vault: {PersonalVaultItems.Count} visible item(s)");
        PreviewLines.Add(LocalScrubberStatus);
        PreviewLines.Add(PayloadStatus);
        PreviewLines.Add(VaultWriteStatus);
        PreviewLines.Add(ConversationArchiveStatus);

        if (IsInitialSetupActive)
        {
            PreviewLines.Add($"Initial setup: {_settings.InitialSetupStep}");
            PreviewLines.Add($"Chart manager: {FirstNonEmpty(_settings.ChartManagerName, "not set")}");
            PreviewLines.Add($"Agent name: {AgentDisplayName}");
            PreviewLines.Add($"Agent tone: {FirstNonEmpty(_settings.AgentTone, "Friendly")}");
        }

        if (_recentActionPacketAuditLines.Count > 0)
        {
            PreviewLines.Add("Action packet audit");
            foreach (var line in _recentActionPacketAuditLines)
            {
                PreviewLines.Add(line);
            }
        }

        if (!string.IsNullOrWhiteSpace(_conversationArchiveService.RawLogPath))
        {
            PreviewLines.Add($"Raw conversation log: {_conversationArchiveService.RawLogPath}");
            PreviewLines.Add($"Scrubbed conversation log: {_conversationArchiveService.ScrubbedLogPath}");
        }

        if (Attachments.Count > 0)
        {
            foreach (var attachment in Attachments.Take(4))
            {
                PreviewLines.Add($"File: {attachment.DisplayName} ({attachment.Kind})");
            }
        }

        if (_lastIngestResults.Count > 0)
        {
            PreviewLines.Add($"Last raw ingest: {_lastIngestResults.Count} file(s)");
            var scaffoldCreatedCount = _lastIngestResults
                .SelectMany(result => result.ScaffoldCreatedPaths)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            PreviewLines.Add($"Chart scaffold created: {scaffoldCreatedCount} item(s)");

            foreach (var result in _lastIngestResults.Take(3))
            {
                PreviewLines.Add($"Saved: {result.DisplayName}");
            }
        }

        if (_lastScrubResults.Count > 0)
        {
            PreviewLines.Add($"Fast scrubber: {_lastScrubResults.Count} file(s) reviewed");

            foreach (var result in _lastScrubResults.Take(3))
            {
                PreviewLines.Add($"{result.DisplayName}: {result.Status}");

                foreach (var finding in result.Findings.Take(4))
                {
                    PreviewLines.Add($"Finding: {finding}");
                }

                if (!string.IsNullOrWhiteSpace(result.Preview))
                {
                    PreviewLines.Add($"Preview: {result.Preview}");
                }
            }
        }

        if (_lastAuditEntries.Count > 0)
        {
            PreviewLines.Add($"Audit log updated: {_lastAuditEntries.Count} entr{(_lastAuditEntries.Count == 1 ? "y" : "ies")}");
            PreviewLines.Add($"Audit path: {_ingestAuditService.AuditLogPath}");
            PreviewLines.Add("API sent: false");
            PreviewLines.Add("Wiki written: false");
        }

        if (_lastSecondPassScrubResults.Count > 0)
        {
            PreviewLines.Add($"Safe chart text files: {_lastSecondPassScrubResults.Count}");

            foreach (var result in _lastSecondPassScrubResults.Take(3))
            {
                PreviewLines.Add($"Safe text: {result.DisplayName}");
                PreviewLines.Add($"Second-pass replacements: {result.ReplacementCount}");
                PreviewLines.Add($"Final path: {result.FinalScrubbedPath}");
            }
        }

        if (_lastImageDocumentReviewResults.Count > 0)
        {
            PreviewLines.Add($"Image review: {_lastImageDocumentReviewResults.Count} file(s)");

            foreach (var result in _lastImageDocumentReviewResults.Take(3))
            {
                PreviewLines.Add($"{result.DisplayName}: {result.ImageDocumentType}");
                PreviewLines.Add($"Official read required: {result.OfficialReadRequired}");
                PreviewLines.Add($"Official read present: {result.OfficialReadPresent}");
                PreviewLines.Add($"Image metadata: {result.SavedMetadataPath}");
            }
        }

        if (_lastEncounterDraft is not null)
        {
            PreviewLines.Add($"Draft id: {_lastEncounterDraft.DraftId}");
            PreviewLines.Add($"Draft document type: {_lastEncounterDraft.DocumentType}");
            PreviewLines.Add($"Draft risk tier: {_lastEncounterDraft.RiskTier}");
            PreviewLines.Add($"Draft citations: {_lastEncounterDraft.Citations.Count}");
            PreviewLines.Add($"Draft validation: {(_lastDraftValidation?.IsValid == true ? "valid" : "needs review")}");
        }

        if (_lastEncounterNodeWriteResults.Count > 0)
        {
            PreviewLines.Add($"Encounter nodes written: {_lastEncounterNodeWriteResults.Count}");
            PreviewLines.Add("Dream Runner: report-only audit available. Try: run Dream Runner");

            foreach (var result in _lastEncounterNodeWriteResults.Take(3))
            {
                PreviewLines.Add($"Encounter node: {result.EncounterNodePath}");
            }
        }

        if (_lastDreamRunnerAuditResult is not null)
        {
            PreviewLines.Add($"Dream Runner status: {_lastDreamRunnerAuditResult.Status}");
            PreviewLines.Add($"Dream Runner open gaps: {_lastDreamRunnerAuditResult.OpenCareGaps}");
            PreviewLines.Add($"Dream Runner active conflicts: {_lastDreamRunnerAuditResult.ActiveConflicts}");
            PreviewLines.Add($"Dream Runner broken links: {_lastDreamRunnerAuditResult.BrokenLinks}");
        }

        if (_lastSpecialtyWeaverResult is not null)
        {
            PreviewLines.Add($"Specialty Weaver status: {_lastSpecialtyWeaverResult.Status}");
            PreviewLines.Add($"Specialty rows written: {_lastSpecialtyWeaverResult.RowsWritten}");
            PreviewLines.Add($"Specialty files ensured: {_lastSpecialtyWeaverResult.SpecialtyFilesEnsured}");
            PreviewLines.Add($"Specialties updated: {string.Join(", ", _lastSpecialtyWeaverResult.UpdatedSpecialties.Take(5))}");
        }

        if (_lastSymptomWatcherResult?.WasCaptured == true)
        {
            PreviewLines.Add($"Symptom Watcher status: {_lastSymptomWatcherResult.Status}");
            PreviewLines.Add($"Symptom mentions captured: {_lastSymptomWatcherResult.Mentions.Count}");
            PreviewLines.Add($"Symptom journal: {Path.GetFileName(_lastSymptomWatcherResult.JournalPath)}");
        }

        if (_lastPatternLinkerResult is not null)
        {
            PreviewLines.Add($"Pattern Linker status: {_lastPatternLinkerResult.Status}");
            PreviewLines.Add($"Pattern charts scanned: {_lastPatternLinkerResult.ChartsScanned}");
            PreviewLines.Add($"Patterns written: {_lastPatternLinkerResult.PatternsWritten}");
            PreviewLines.Add($"Care gaps from patterns: {_lastPatternLinkerResult.CareGapsWritten}");
        }

        if (_lastHealthGoalResult?.WasCaptured == true)
        {
            PreviewLines.Add($"Health Goal status: {_lastHealthGoalResult.Status}");
            PreviewLines.Add($"Health Goal captured: {_lastHealthGoalResult.GoalTitle}");
        }

        if (_lastHealthPreferenceResult?.WasCaptured == true)
        {
            PreviewLines.Add($"Health Preference status: {_lastHealthPreferenceResult.Status}");
            PreviewLines.Add($"Health Preference captured: {_lastHealthPreferenceResult.Summary}");
        }

        if (_lastDrugInteractionScanResult is not null)
        {
            PreviewLines.Add($"Drug scan status: {_lastDrugInteractionScanResult.Status}");
            PreviewLines.Add($"Medications reviewed: {_lastDrugInteractionScanResult.ActiveMedicationsReviewed}");
            PreviewLines.Add($"Drug scan findings: {_lastDrugInteractionScanResult.FindingsWritten}");
        }

        if (_lastPreVisitBriefResult is not null)
        {
            PreviewLines.Add($"Pre-Visit Brief status: {_lastPreVisitBriefResult.Status}");
            PreviewLines.Add($"Pre-Visit sections: {_lastPreVisitBriefResult.SectionsIncluded}");
        }

        if (_lastSessionOpenAwarenessResult is not null)
        {
            PreviewLines.Add($"Session Open status: {_lastSessionOpenAwarenessResult.Status}");
            PreviewLines.Add($"Session Open chart: {_lastSessionOpenAwarenessResult.ActiveChartId}");
        }

        if (_lastDollyWorkingSummaryResult is not null)
        {
            PreviewLines.Add($"Dolly summary status: {_lastDollyWorkingSummaryResult.Status}");
            PreviewLines.Add($"Dolly summary sections: {_lastDollyWorkingSummaryResult.SectionsIncluded}");
            PreviewLines.Add($"Dolly summary file: {Path.GetFileName(_lastDollyWorkingSummaryResult.SummaryPath)}");
            PreviewLines.Add(string.IsNullOrWhiteSpace(_lastGemmaWorkingSummaryContext)
                ? $"{AgentDisplayName} context: not loaded yet"
                : $"{AgentDisplayName} context: loaded");
        }

        if (_lastDollyTaskBoardResult is not null)
        {
            PreviewLines.Add($"Dolly task board: {_lastDollyTaskBoardResult.Status}");
            PreviewLines.Add($"Dolly active tasks: {_lastDollyTaskBoardResult.ActiveTaskCount}");
            PreviewLines.Add($"Dolly completed tasks: {_lastDollyTaskBoardResult.CompletedTaskCount}");
        }

        if (_lastGeminiBackendPipelineResult is not null)
        {
            PreviewLines.Add($"Agent review steps: {_lastGeminiBackendPipelineResult.Steps.Count}");
            PreviewLines.Add($"Safe text submissions: {_lastGeminiBackendPipelineResult.ApiSubmittedSourcePaths.Count}");
            PreviewLines.Add($"Validated wiki writes: {_lastGeminiBackendPipelineResult.WikiWrittenSourcePaths.Count}");

            foreach (var step in _lastGeminiBackendPipelineResult.Steps.Take(5))
            {
                PreviewLines.Add($"{step.Name}: {step.Status} - {step.Detail}");
            }
        }

        if (_lastLocalModelReview is not null)
        {
            PreviewLines.Add($"Local review status: {_lastLocalModelReview.Status}");
            PreviewLines.Add($"Local PHI risk: {_lastLocalModelReview.RemainingPhiRisk}");
            PreviewLines.Add($"Local recommendation: {_lastLocalModelReview.Recommendation}");

            foreach (var finding in _lastLocalModelReview.Findings.Take(3))
            {
                PreviewLines.Add($"Local finding: {finding}");
            }
        }
    }

    private void RefreshVitaMastery()
    {
        try
        {
            var chartContext = GetActiveChart();
            var identity = _patientRegistryService.GetAllIdentities()
                .FirstOrDefault(patient => patient.ChartId.Equals(chartContext.ChartId, StringComparison.OrdinalIgnoreCase));
            _lastVitaMasteryResult = _vitaMasteryService.Evaluate(VaultRootPath, chartContext, identity);
        }
        catch
        {
            _lastVitaMasteryResult = null;
        }

        RefreshVitaMasteryRows();
        OnPropertyChanged(nameof(VitaMasterySummary));
        OnPropertyChanged(nameof(VitaMasteryPercent));
        OnPropertyChanged(nameof(VitaMasteryMicroQuestSummary));
        OnPropertyChanged(nameof(VitaMasteryPointsSummary));
        OnPropertyChanged(nameof(VitaMasteryStageDisplay));
        OnPropertyChanged(nameof(VitaMasteryCompletionSummary));
        OnPropertyChanged(nameof(VitaMasteryFoundationStepValue));
        OnPropertyChanged(nameof(VitaMasteryPreventiveStepValue));
        OnPropertyChanged(nameof(VitaMasteryDeepSignalStepValue));
        OnPropertyChanged(nameof(DataHunterBasicPercent));
        OnPropertyChanged(nameof(DataHunterProgressSummary));
        OnPropertyChanged(nameof(DataHunterStageSummary));
        OnPropertyChanged(nameof(DataHunterPointsSummary));
        OnPropertyChanged(nameof(DataHunterNextQuestion));
        OnPropertyChanged(nameof(DataHunterQuestCardMeta));
        OnPropertyChanged(nameof(DataHunterFirstMasterTargetSummary));
        OnPropertyChanged(nameof(DataHunterFirstMasterTargetDetail));
        OnPropertyChanged(nameof(DataHunterMasterEvidenceSummary));
        OnPropertyChanged(nameof(DataHunterDebugSummary));
        RefreshDataHunterDebugRows();
    }

    private void StartDataHunterBasicInterview()
    {
        ShowConversation();
        var chartContext = GetActiveChart();
        _pendingDataHunterBasicChartId = chartContext.ChartId;
        _pendingDataHunterExpansionChartId = string.Empty;
        _pendingDataHunterExpansionKey = string.Empty;
        _dataHunterQuestionsAnsweredThisSession = 0;
        _dataHunterContinuousInterviewEnabled = false;
        _dataHunterPausedForSession = false;
        AddArchivedMessage(
            MessageAuthor.Dolly,
            AgentLabel,
            BuildDataHunterQuestStartLines(chartContext),
            mode: "data_hunter_basic_question",
            linkedChartId: chartContext.ChartId);
        VaultWriteStatus = "Data Hunter Basic started";
        RefreshPreview();
    }

    private int CalculateDataHunterAnsweredCount()
    {
        return CalculateDataHunterAnsweredCount(GetActiveChart());
    }

    private int CalculateDataHunterAnsweredCount(ChartContext chartContext)
    {
        var map = LoadDataHunterPersonalDataMap(chartContext);
        var applicableKeys = GetApplicableDataHunterBasicQuestionIndexes(map)
            .Select(index => GetDataHunterBasicQuestionKey(index))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return Math.Clamp(
            map.Answers.Count(answer => applicableKeys.Contains(answer.Key) && IsDataHunterAnswerComplete(answer)),
            0,
            applicableKeys.Count);
    }

    private int CalculateDataHunterBasicPercent()
    {
        var chartContext = GetActiveChart();
        var map = LoadDataHunterPersonalDataMap(chartContext);
        var applicableQuestionCount = GetApplicableDataHunterBasicQuestionIndexes(map).Count;
        return applicableQuestionCount == 0
            ? 0
            : CalculateDataHunterAnsweredCount(chartContext) * 100 / applicableQuestionCount;
    }

    private int CalculateDataHunterBasicPercent(ChartContext chartContext)
    {
        var map = LoadDataHunterPersonalDataMap(chartContext);
        var applicableQuestionCount = GetApplicableDataHunterBasicQuestionIndexes(map).Count;
        return applicableQuestionCount == 0
            ? 0
            : CalculateDataHunterAnsweredCount(chartContext) * 100 / applicableQuestionCount;
    }

    private static int CalculateDataHunterBasicXp(DataHunterPersonalDataMap map)
    {
        return map.Answers.Sum(answer => answer.XpAwarded) +
            map.CategoryRewards.Sum(reward => reward.XpAwarded);
    }

    private static int CalculateDataHunterTotalAnsweredCount(DataHunterPersonalDataMap map)
    {
        return Math.Clamp(
            map.Answers.Count(IsDataHunterAnswerComplete),
            0,
            DataHunterBasicQuestions.Length);
    }

    private static string BuildDataHunterCategoryProgressSummary(DataHunterPersonalDataMap map)
    {
        var nextIndex = GetNextDataHunterBasicQuestionIndex(map);
        if (!nextIndex.HasValue)
        {
            return $"{CalculateDataHunterTotalAnsweredCount(map)} / {DataHunterBasicQuestions.Length} | {CalculateDataHunterBasicXp(map)} XP";
        }

        var category = GetDataHunterQuestionCategory(GetDataHunterBasicQuestionKey(nextIndex.Value));
        var categoryKeys = DataHunterBasicQuestionKeys
            .Where(key => GetDataHunterQuestionCategory(key).Equals(category, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var answered = map.Answers.Count(answer =>
            categoryKeys.Contains(answer.Key, StringComparer.OrdinalIgnoreCase) &&
            IsDataHunterAnswerComplete(answer));
        return $"{answered} / {categoryKeys.Count} {ShortenDataHunterCategory(category)}";
    }

    private static string ShortenDataHunterCategory(string category)
    {
        return category switch
        {
            "Current health status" => "status",
            "Medications & supplements" => "meds",
            "Prevention & habits" => "prevention",
            "Vitals & measurements" => "vitals",
            "Care team & support" => "care team",
            "Medical records" => "records",
            _ => category.ToLowerInvariant()
        };
    }

    private string GetDataHunterNextQuestion(ChartContext chartContext)
    {
        var map = LoadDataHunterPersonalDataMap(chartContext);
        var nextIndex = GetNextDataHunterBasicQuestionIndex(map);
        return nextIndex.HasValue
            ? DataHunterBasicQuestions[nextIndex.Value]
            : "Basic interview complete. Next: gather the records you mapped.";
    }

    private IReadOnlyList<string> BuildDataHunterQuestStartLines(ChartContext chartContext)
    {
        var map = LoadDataHunterPersonalDataMap(chartContext);
        var nextIndex = GetNextDataHunterBasicQuestionIndex(map);
        var firstName = ExtractFirstNameForDisplay(chartContext.LocalDisplayName);
        if (!nextIndex.HasValue)
        {
            return
            [
                $"{firstName}, welcome back to Data Hunting.",
                "Basic Hunter is complete for now. Your next quest is to gather accepted records from the sources we mapped.",
                "Upload a record when you are ready, and I will keep the evidence step behind desktop review."
            ];
        }

        var key = GetDataHunterBasicQuestionKey(nextIndex.Value);
        return
        [
            $"{firstName}, welcome back to Data Hunting.",
            $"Your next quest is in {GetDataHunterQuestionCategory(key)}.",
            DataHunterBasicQuestions[nextIndex.Value],
            "Answer when you are ready. You can also say later, not sure, or does not apply."
        ];
    }

    private string GetDataHunterNextQuestCategory(ChartContext chartContext)
    {
        var map = LoadDataHunterPersonalDataMap(chartContext);
        var nextIndex = GetNextDataHunterBasicQuestionIndex(map);
        return nextIndex.HasValue
            ? GetDataHunterQuestionCategory(GetDataHunterBasicQuestionKey(nextIndex.Value))
            : "Data Master";
    }

    private string GetDataHunterNextQuestStatus(ChartContext chartContext)
    {
        var map = LoadDataHunterPersonalDataMap(chartContext);
        var nextIndex = GetNextDataHunterBasicQuestionIndex(map);
        if (!nextIndex.HasValue)
        {
            return "Pending accepted evidence";
        }

        var key = GetDataHunterBasicQuestionKey(nextIndex.Value);
        var answer = map.Answers.FirstOrDefault(answer => answer.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
        return answer?.Status ?? "ready";
    }

    private int GetDataHunterNextQuestXp(ChartContext chartContext)
    {
        var map = LoadDataHunterPersonalDataMap(chartContext);
        var nextIndex = GetNextDataHunterBasicQuestionIndex(map);
        return nextIndex.HasValue
            ? GetDataHunterQuestionXp(GetDataHunterBasicQuestionKey(nextIndex.Value))
            : 0;
    }

    private bool TryCaptureDataHunterBasicAnswer(
        ChartContext chartContext,
        string userText,
        string sourceRoute,
        out IReadOnlyList<string> responseLines)
    {
        responseLines = [];
        if (string.IsNullOrWhiteSpace(userText) ||
            DataHunterBasicQuestions.Length == 0)
        {
            return false;
        }

        var map = LoadDataHunterPersonalDataMap(chartContext);
        var questionIndex = GetNextDataHunterBasicQuestionIndex(map);
        if (!questionIndex.HasValue)
        {
            return false;
        }

        var question = DataHunterBasicQuestions[questionIndex.Value];
        var key = GetDataHunterBasicQuestionKey(questionIndex.Value);
        var status = ClassifyDataHunterAnswerStatus(userText);
        var answerText = NormalizeDataHunterStoredAnswer(userText, status);
        var previousMasterQuestIds = map.MasterQuests
            .Select(quest => quest.QuestId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (status.Equals(DataHunterAnswerStatusAnswered, StringComparison.OrdinalIgnoreCase) &&
            TryBuildInlineDetailAnswers(key, question, userText, sourceRoute, out var parentAnswer, out var detailAnswer, out var expansionQuestion))
        {
            map.Answers.RemoveAll(item =>
                item.Key.Equals(parentAnswer.Key, StringComparison.OrdinalIgnoreCase) ||
                item.Key.Equals(detailAnswer.Key, StringComparison.OrdinalIgnoreCase));
            map.Answers.Add(parentAnswer);
            map.Answers.Add(detailAnswer);
            map.UpdatedAt = DateTimeOffset.Now;
            map.MasterQuests = GenerateDataHunterMasterQuests(map);
            var inlineNewMasterQuest = map.MasterQuests.FirstOrDefault(quest => !previousMasterQuestIds.Contains(quest.QuestId));
            var inlineCategoryReward = TryAwardDataHunterCategoryReward(map, parentAnswer.Category);
            SaveDataHunterPersonalDataMap(chartContext, map);
            RefreshDataHunterProperties();

            _pendingDataHunterExpansionChartId = chartContext.ChartId;
            _pendingDataHunterExpansionKey = detailAnswer.Key;
            _dataHunterQuestionsAnsweredThisSession++;

            var inlineAnsweredCount = CalculateDataHunterAnsweredCount(chartContext);
            var inlineApplicableQuestionCount = GetApplicableDataHunterBasicQuestionIndexes(map).Count;
            responseLines =
            [
                $"Got it. I added {detailAnswer.Answer}. +{parentAnswer.XpAwarded + detailAnswer.XpAwarded} answer XP to Data Hunter.",
                BuildDataHunterRewardLine(inlineCategoryReward, inlineNewMasterQuest),
                expansionQuestion
            ];
            responseLines = responseLines
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToList();

            _chronosLedgerService.RecordEvent(
                VaultRootPath,
                chartContext,
                "data_hunter_basic_answer_saved",
                $"Saved Data Hunter Basic inline answer: {parentAnswer.Key} + {detailAnswer.Key}.",
                sourceRoute,
                "data_hunter");
            VaultWriteStatus = $"Data Hunter Basic saved {inlineAnsweredCount}/{inlineApplicableQuestionCount}";
            return true;
        }

        var answer = new DataHunterPersonalDataAnswer
        {
            Key = key,
            Question = question,
            Answer = answerText,
            Status = status,
            Category = GetDataHunterQuestionCategory(key),
            XpAwarded = GetDataHunterAnswerXp(status, key),
            AnsweredAt = DateTimeOffset.Now,
            SourceRoute = sourceRoute
        };

        map.Answers.RemoveAll(item => item.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
        map.Answers.Add(answer);
        map.UpdatedAt = DateTimeOffset.Now;
        map.MasterQuests = GenerateDataHunterMasterQuests(map);
        var newMasterQuest = map.MasterQuests.FirstOrDefault(quest => !previousMasterQuestIds.Contains(quest.QuestId));
        var categoryReward = TryAwardDataHunterCategoryReward(map, answer.Category);
        SaveDataHunterPersonalDataMap(chartContext, map);
        RefreshDataHunterProperties();

        var nextQuestion = GetDataHunterNextQuestion(chartContext);
        var answeredCount = CalculateDataHunterAnsweredCount(chartContext);
        var applicableQuestionCount = GetApplicableDataHunterBasicQuestionIndexes(map).Count;
        if (status.Equals(DataHunterAnswerStatusDeferred, StringComparison.OrdinalIgnoreCase))
        {
            responseLines =
            [
                "No problem. I skipped that one for now. No XP was added. Here is your next question.",
                nextQuestion
            ];
        }
        else if (status.Equals(DataHunterAnswerStatusNotSure, StringComparison.OrdinalIgnoreCase) ||
                 status.Equals(DataHunterAnswerStatusNotApplicable, StringComparison.OrdinalIgnoreCase))
        {
            _dataHunterQuestionsAnsweredThisSession++;
            responseLines =
            [
                status.Equals(DataHunterAnswerStatusNotSure, StringComparison.OrdinalIgnoreCase)
                    ? "Thanks. I marked that as not sure and moved on. No XP was added. Here is your next question."
                    : "Got it. I marked that as not applicable and moved on. No XP was added. Here is your next question.",
                nextQuestion
            ];
        }
        else
        {
            _dataHunterQuestionsAnsweredThisSession++;
            if (!_dataHunterContinuousInterviewEnabled &&
                _dataHunterQuestionsAnsweredThisSession >= DataHunterQuestionsPerSession &&
                answeredCount < applicableQuestionCount)
            {
                _dataHunterPausedForSession = true;
                responseLines =
                [
                    $"Thanks, that helps. I added +{answer.XpAwarded} answer XP to Data Hunter.",
                    BuildDataHunterRewardLine(categoryReward, newMasterQuest),
                    "That is a good chunk for now. I can pause here, or keep going if you have a minute."
                ];
            }
            else
            {
                responseLines = answeredCount >= applicableQuestionCount
            ? [
                $"Thanks, that helps. I added +{answer.XpAwarded} answer XP to Data Hunter.",
                BuildDataHunterRewardLine(categoryReward, newMasterQuest),
                "Basic interview complete for now. I marked these answers as user-provided context, not verified medical evidence.",
                "Next up: I can turn the record locations you named into Data Master gathering quests."
            ]
            : [
                BuildDataHunterNextQuestionLead(
                    $"Thanks, that helps. I added +{answer.XpAwarded} answer XP to Data Hunter.",
                    BuildDataHunterRewardLine(categoryReward, newMasterQuest)),
                nextQuestion
            ];
            }
        }

        responseLines = responseLines
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

        _chronosLedgerService.RecordEvent(
            VaultRootPath,
            chartContext,
            "data_hunter_basic_answer_saved",
            $"Saved Data Hunter Basic answer: {key}.",
            sourceRoute,
            "data_hunter");
        VaultWriteStatus = $"Data Hunter Basic saved {answeredCount}/{applicableQuestionCount}";
        return true;
    }

    private static DataHunterCategoryReward TryAwardDataHunterCategoryReward(
        DataHunterPersonalDataMap map,
        string category)
    {
        if (string.IsNullOrWhiteSpace(category) ||
            map.CategoryRewards.Any(reward => reward.Category.Equals(category, StringComparison.OrdinalIgnoreCase)) ||
            !IsDataHunterCategoryComplete(map, category) ||
            !HasDataHunterCategoryAnswerXp(map, category))
        {
            return new DataHunterCategoryReward();
        }

        var reward = new DataHunterCategoryReward
        {
            Category = category,
            XpAwarded = DataHunterCategoryCompleteXp,
            AwardedAt = DateTimeOffset.Now
        };
        map.CategoryRewards.Add(reward);
        return reward;
    }

    private static bool IsDataHunterCategoryComplete(DataHunterPersonalDataMap map, string category)
    {
        var categoryKeys = GetApplicableDataHunterBasicQuestionIndexes(map)
            .Select(GetDataHunterBasicQuestionKey)
            .Where(key => GetDataHunterQuestionCategory(key).Equals(category, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return categoryKeys.Count > 0 &&
            categoryKeys.All(key => map.Answers.Any(answer =>
                answer.Key.Equals(key, StringComparison.OrdinalIgnoreCase) &&
                IsDataHunterAnswerComplete(answer)));
    }

    private static bool HasDataHunterCategoryAnswerXp(DataHunterPersonalDataMap map, string category)
    {
        return map.Answers.Any(answer =>
            answer.XpAwarded > 0 &&
            GetDataHunterQuestionCategory(answer.Key).Equals(category, StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildDataHunterRewardLine(
        DataHunterCategoryReward categoryReward,
        DataHunterMasterQuest? newMasterQuest)
    {
        var parts = new List<string>();
        if (categoryReward.XpAwarded > 0)
        {
            parts.Add($"Category bonus: {categoryReward.Category} mapped +{categoryReward.XpAwarded} XP.");
        }

        if (newMasterQuest is not null)
        {
            parts.Add($"Master quest queued: {newMasterQuest.Title}.");
        }

        return parts.Count == 0
            ? string.Empty
            : string.Join(" ", parts);
    }

    private static string BuildDataHunterNextQuestionLead(string acknowledgement, string rewardLine = "")
    {
        return string.Join(
            " ",
            new[] { acknowledgement, rewardLine, "Here is your next question." }
                .Where(line => !string.IsNullOrWhiteSpace(line)));
    }

    private static bool TryBuildInlineDetailAnswers(
        string parentKey,
        string parentQuestion,
        string userText,
        string sourceRoute,
        out DataHunterPersonalDataAnswer parentAnswer,
        out DataHunterPersonalDataAnswer detailAnswer,
        out string expansionQuestion)
    {
        parentAnswer = new DataHunterPersonalDataAnswer();
        detailAnswer = new DataHunterPersonalDataAnswer();
        expansionQuestion = string.Empty;

        if (!TryGetDataHunterInlineDetailChild(parentKey, out var detailKey, out expansionQuestion) ||
            !LooksLikeInlineDataHunterDetail(userText))
        {
            return false;
        }

        var detailIndex = Array.IndexOf(DataHunterBasicQuestionKeys, detailKey);
        if (detailIndex < 0 || detailIndex >= DataHunterBasicQuestions.Length)
        {
            return false;
        }

        var category = GetDataHunterQuestionCategory(parentKey);
        var now = DateTimeOffset.Now;
        parentAnswer = new DataHunterPersonalDataAnswer
        {
            Key = parentKey,
            Question = parentQuestion,
            Answer = "Yes",
            Status = DataHunterAnswerStatusAnswered,
            Category = category,
            XpAwarded = GetDataHunterQuestionXp(parentKey),
            AnsweredAt = now,
            SourceRoute = sourceRoute
        };
        detailAnswer = new DataHunterPersonalDataAnswer
        {
            Key = detailKey,
            Question = DataHunterBasicQuestions[detailIndex],
            Answer = userText.Trim(),
            Status = DataHunterAnswerStatusAnswered,
            Category = GetDataHunterQuestionCategory(detailKey),
            XpAwarded = GetDataHunterQuestionXp(detailKey),
            AnsweredAt = now,
            SourceRoute = sourceRoute
        };
        return true;
    }

    private static bool TryGetDataHunterInlineDetailChild(
        string parentKey,
        out string detailKey,
        out string expansionQuestion)
    {
        (detailKey, expansionQuestion) = parentKey switch
        {
            "has_known_health_problems" => ("known_health_problems", "Any other health problems or diagnoses you want me to remember?"),
            "has_prescription_medications" => ("prescription_medications", "Any other prescription medications you want to add?"),
            "has_surgery_history" => ("surgery_history", "Any other surgeries you want me to remember?"),
            "has_procedure_history" => ("procedure_history", "Any other procedures you want me to remember?"),
            "has_hospital_history" => ("hospital_history", "Any other hospital stays you want me to remember?"),
            "has_er_or_urgent_care_history" => ("er_or_urgent_care_history", "Any other ER or urgent care visits you want me to remember?"),
            "has_family_history" => ("family_history_conditions", "Any other family history you want me to remember?"),
            "nicotine_status" => ("nicotine_amount_duration", "Anything else about nicotine use you want me to remember?"),
            "alcohol_status" => ("alcohol_amount_frequency", "Anything else about alcohol use you want me to remember?"),
            "exercise_frequency" => ("exercise_type", "Any other exercise or movement details you want me to remember?"),
            "has_primary_care_doctor" => ("primary_care_doctor_name", "Anything else about your primary care doctor you want me to remember?"),
            "has_patient_portals" => ("patient_portals", "Any other portals or health systems you want me to remember?"),
            "has_past_hospital_care" => ("past_hospitals", "Any other hospitals you want me to remember?"),
            "has_specialists" => ("specialists", "Any other specialists you want me to remember?"),
            "has_regular_pharmacy" => ("regular_pharmacy", "Any other pharmacy details you want me to remember?"),
            "has_lab_records" => ("lab_record_locations", "Any other lab record locations you want me to remember?"),
            "has_imaging_records" => ("imaging_record_locations", "Any other imaging locations or CDs you want me to remember?"),
            _ => (string.Empty, string.Empty)
        };
        return !string.IsNullOrWhiteSpace(detailKey);
    }

    private static bool LooksLikeInlineDataHunterDetail(string userText)
    {
        var normalized = NormalizeConfirmation(userText);
        if (normalized.Length < 8 ||
            normalized is "yes" or "y" or "yeah" or "yep" or "sure" or "ok" or "okay" ||
            IsDataHunterNoAdditionalDetailAnswer(userText) ||
            !IsPositiveDataHunterAnswer(userText))
        {
            return false;
        }

        return normalized.Contains(' ') ||
            normalized.Contains(',') ||
            normalized.Contains(';') ||
            normalized.Contains(':');
    }

    private static bool IsDataHunterNoAdditionalDetailAnswer(string value)
    {
        var normalized = NormalizeConfirmation(value);
        return normalized is "same as above" or "already said" or "i already said" or "i already told you" or "no other" or "no others" or "none besides that" or "nothing else" or "no more" ||
               normalized.Contains("same as above", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("i already said", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("i already told", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("none besides", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("nothing else", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("no other", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsDataHunterDuplicateDetail(string existingAnswer, string newAnswer)
    {
        var normalizedExisting = NormalizeDataHunterComparableText(existingAnswer);
        var normalizedNew = NormalizeDataHunterComparableText(newAnswer);
        return !string.IsNullOrWhiteSpace(normalizedNew) &&
               (normalizedExisting.Equals(normalizedNew, StringComparison.OrdinalIgnoreCase) ||
                normalizedExisting.Contains(normalizedNew, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeDataHunterComparableText(string value)
    {
        var normalized = NormalizeConfirmation(value);
        normalized = Regex.Replace(normalized, @"\b(same as above|i already said|i already told you|already said)\b", string.Empty, RegexOptions.IgnoreCase);
        normalized = Regex.Replace(normalized, @"[^a-z0-9]+", " ", RegexOptions.IgnoreCase);
        return Regex.Replace(normalized, @"\s+", " ").Trim();
    }

    private bool TryCaptureDataHunterExpansionAnswer(
        ChartContext chartContext,
        string userText,
        string sourceRoute,
        out IReadOnlyList<string> responseLines)
    {
        responseLines = [];
        if (string.IsNullOrWhiteSpace(_pendingDataHunterExpansionKey) ||
            string.IsNullOrWhiteSpace(userText))
        {
            return false;
        }

        var status = ClassifyDataHunterAnswerStatus(userText);
        var map = LoadDataHunterPersonalDataMap(chartContext);
        var existing = map.Answers.FirstOrDefault(answer =>
            answer.Key.Equals(_pendingDataHunterExpansionKey, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            _pendingDataHunterExpansionChartId = string.Empty;
            _pendingDataHunterExpansionKey = string.Empty;
            return false;
        }

        _pendingDataHunterExpansionChartId = string.Empty;
        _pendingDataHunterExpansionKey = string.Empty;

        if (status.Equals(DataHunterAnswerStatusAnswered, StringComparison.OrdinalIgnoreCase) &&
            IsPositiveDataHunterAnswer(userText))
        {
            if (IsDataHunterNoAdditionalDetailAnswer(userText))
            {
                responseLines =
                [
                    "Got it. I kept the answer as it is. Here is your next question.",
                    GetDataHunterNextQuestion(chartContext)
                ];
                _chronosLedgerService.RecordEvent(
                    VaultRootPath,
                    chartContext,
                    "data_hunter_expansion_answer_saved",
                    $"Processed Data Hunter expansion answer without new detail: {existing.Key}.",
                    sourceRoute,
                    "data_hunter");
                return true;
            }

            if (ContainsDataHunterDuplicateDetail(existing.Answer, userText))
            {
                responseLines =
                [
                    "I already had that detail, so I did not duplicate it. Here is your next question.",
                    GetDataHunterNextQuestion(chartContext)
                ];
                _chronosLedgerService.RecordEvent(
                    VaultRootPath,
                    chartContext,
                    "data_hunter_expansion_answer_saved",
                    $"Skipped duplicate Data Hunter expansion answer: {existing.Key}.",
                    sourceRoute,
                    "data_hunter");
                return true;
            }

            existing.Answer = $"{existing.Answer}; {userText.Trim()}";
            existing.XpAwarded += 1;
            existing.AnsweredAt = DateTimeOffset.Now;
            existing.SourceRoute = sourceRoute;
            map.UpdatedAt = DateTimeOffset.Now;
            SaveDataHunterPersonalDataMap(chartContext, map);
            RefreshDataHunterProperties();
            responseLines =
            [
                "Thanks, I added that detail. +1 XP to Data Hunter. Here is your next question.",
                GetDataHunterNextQuestion(chartContext)
            ];
        }
        else
        {
            responseLines =
            [
                "Got it. I kept the answer as it is. Here is your next question.",
                GetDataHunterNextQuestion(chartContext)
            ];
        }

        _chronosLedgerService.RecordEvent(
            VaultRootPath,
            chartContext,
            "data_hunter_expansion_answer_saved",
            $"Processed Data Hunter expansion answer: {existing.Key}.",
            sourceRoute,
            "data_hunter");
        return true;
    }

    private bool TryHandleDataHunterSessionControl(
        ChartContext chartContext,
        string userText,
        out IReadOnlyList<string> responseLines)
    {
        responseLines = [];
        var normalized = NormalizeConfirmation(userText);
        if (normalized.Contains("stop", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("pause", StringComparison.OrdinalIgnoreCase))
        {
            _pendingDataHunterBasicChartId = string.Empty;
            _pendingDataHunterExpansionChartId = string.Empty;
            _pendingDataHunterExpansionKey = string.Empty;
            _dataHunterPausedForSession = false;
            responseLines =
            [
                "Sounds good. I paused Data Hunter for now.",
                "The next question stays on the Data Hunter quest card, so you can answer it anytime."
            ];
            return true;
        }

        if (!_dataHunterPausedForSession)
        {
            return false;
        }

        if (IsAffirmative(userText) ||
            normalized.Contains("keep going", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("more", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("continue", StringComparison.OrdinalIgnoreCase))
        {
            _dataHunterPausedForSession = false;
            _dataHunterContinuousInterviewEnabled = true;
            responseLines =
            [
                "I will keep asking more. Let me know when to stop, pause, or save the next one for later.",
                GetDataHunterNextQuestion(chartContext)
            ];
            return true;
        }

        if (IsNegative(userText) ||
            normalized.Contains("later", StringComparison.OrdinalIgnoreCase))
        {
            _pendingDataHunterBasicChartId = string.Empty;
            _pendingDataHunterExpansionChartId = string.Empty;
            _pendingDataHunterExpansionKey = string.Empty;
            responseLines =
            [
                "Sounds good. I paused Data Hunter for now.",
                "The next question stays on the Data Hunter quest card, so you can answer it anytime."
            ];
            return true;
        }

        _dataHunterPausedForSession = false;
        return false;
    }

    private static int? GetNextDataHunterBasicQuestionIndex(DataHunterPersonalDataMap map)
    {
        foreach (var index in GetApplicableDataHunterBasicQuestionIndexes(map))
        {
            var key = GetDataHunterBasicQuestionKey(index);
            var answer = map.Answers.FirstOrDefault(answer => answer.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (answer is null || !IsDataHunterAnswerComplete(answer))
            {
                return index;
            }
        }

        return null;
    }

    private static List<int> GetApplicableDataHunterBasicQuestionIndexes(DataHunterPersonalDataMap map)
    {
        var indexes = Enumerable.Range(0, DataHunterBasicQuestions.Length).ToList();
        foreach (var index in Enumerable.Range(0, DataHunterBasicQuestions.Length).ToList())
        {
            var key = GetDataHunterBasicQuestionKey(index);
            if (!ShouldAskDataHunterBasicQuestion(map, key))
            {
                indexes.Remove(index);
            }
        }

        return indexes;
    }

    private static bool ShouldAskDataHunterBasicQuestion(DataHunterPersonalDataMap map, string key)
    {
        if (!TryGetDataHunterConditionalParentKey(key, out var parentKey))
        {
            return true;
        }

        var parentAnswer = GetDataHunterAnswer(map, parentKey);
        return IsPositiveDataHunterAnswer(parentAnswer);
    }

    private static bool TryGetDataHunterConditionalParentKey(string key, out string parentKey)
    {
        parentKey = key switch
        {
            "known_health_problems" => "has_known_health_problems",
            "top_known_problem" => "has_known_health_problems",
            "prescription_medications" => "has_prescription_medications",
            "medication_adherence_or_side_effects" => "has_prescription_medications",
            "surgery_history" => "has_surgery_history",
            "procedure_history" => "has_procedure_history",
            "hospital_history" => "has_hospital_history",
            "er_or_urgent_care_history" => "has_er_or_urgent_care_history",
            "family_history_relatives" => "has_family_history",
            "family_history_conditions" => "has_family_history",
            "family_history_onset_age" => "has_family_history",
            "high_signal_family_history" => "has_family_history",
            "family_history_unsure" => "has_family_history",
            "nicotine_amount_duration" => "nicotine_status",
            "alcohol_amount_frequency" => "alcohol_status",
            "exercise_type" => "exercise_frequency",
            "primary_care_doctor_name" => "has_primary_care_doctor",
            "pcp_has_most_records" => "has_primary_care_doctor",
            "patient_portals" => "has_patient_portals",
            "past_hospitals" => "has_past_hospital_care",
            "specialists" => "has_specialists",
            "regular_pharmacy" => "has_regular_pharmacy",
            "lab_record_locations" => "has_lab_records",
            "imaging_record_locations" => "has_imaging_records",
            _ => string.Empty
        };
        return !string.IsNullOrWhiteSpace(parentKey);
    }

    private static bool IsPositiveDataHunterAnswer(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = NormalizeConfirmation(value);
        if (normalized is "no" or "n" or "none" or "nope" or "not sure" or "unknown" or "do not know" or "don't know")
        {
            return false;
        }

        if (normalized.Contains("no ", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("none", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("does not apply", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("doesn't apply", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("not right now", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("not currently", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("never", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private static string ClassifyDataHunterAnswerStatus(string value)
    {
        var normalized = NormalizeConfirmation(value);
        if (IsDataHunterDeferredAnswer(normalized))
        {
            return DataHunterAnswerStatusDeferred;
        }

        if (IsDataHunterNotApplicableAnswer(normalized))
        {
            return DataHunterAnswerStatusNotApplicable;
        }

        if (IsDataHunterNotSureAnswer(normalized))
        {
            return DataHunterAnswerStatusNotSure;
        }

        return DataHunterAnswerStatusAnswered;
    }

    private static string NormalizeDataHunterStoredAnswer(string value, string status)
    {
        return status switch
        {
            DataHunterAnswerStatusDeferred => "Later",
            DataHunterAnswerStatusNotApplicable => "Does not apply",
            DataHunterAnswerStatusNotSure => string.IsNullOrWhiteSpace(value) ? "Not sure" : value.Trim(),
            _ => value.Trim()
        };
    }

    private static bool IsDataHunterAnswerComplete(DataHunterPersonalDataAnswer answer)
    {
        var status = string.IsNullOrWhiteSpace(answer.Status)
            ? DataHunterAnswerStatusAnswered
            : answer.Status;
        return status is DataHunterAnswerStatusAnswered
            or DataHunterAnswerStatusNotSure
            or DataHunterAnswerStatusDeferred
            or DataHunterAnswerStatusNotApplicable
            or DataHunterAnswerStatusRecordConfirmed;
    }

    private static int GetDataHunterAnswerXp(string status, string questionKey)
    {
        return status is DataHunterAnswerStatusAnswered
            or DataHunterAnswerStatusRecordConfirmed
            ? GetDataHunterQuestionXp(questionKey)
            : 0;
    }

    private static int GetDataHunterQuestionXp(string questionKey)
    {
        return questionKey is "priority_records" or "known_missing_records" ? 2 : 1;
    }

    private static string GetDataHunterQuestionCategory(string questionKey)
    {
        return questionKey switch
        {
            "long_term_health_goals" => "Future health goals",
            "has_known_health_problems" or "known_health_problems" or "top_known_problem" or "new_or_frequent_symptoms" or "recent_health_changes" => "Current health status",
            "has_prescription_medications" or "prescription_medications" or "supplements_and_nonprescription_treatments" or "medication_adherence_or_side_effects" or "allergies_and_reactions" => "Medications & supplements",
            "has_surgery_history" or "surgery_history" or "has_procedure_history" or "procedure_history" or "has_hospital_history" or "hospital_history" or "has_er_or_urgent_care_history" or "er_or_urgent_care_history" => "Medical history",
            "has_family_history" or "family_history_relatives" or "family_history_conditions" or "family_history_onset_age" or "high_signal_family_history" or "family_history_unsure" => "Family history",
            "nicotine_status" or "nicotine_amount_duration" or "alcohol_status" or "alcohol_amount_frequency" or "substance_context" or "exercise_frequency" or "exercise_type" or "sleep_hours" or "sleep_enough" => "Prevention & habits",
            "usual_blood_pressure" or "current_height_weight" or "unintentional_weight_change" => "Vitals & measurements",
            "has_primary_care_doctor" or "primary_care_doctor_name" or "medical_decision_support" or "emergency_contact_or_proxy" => "Care team & support",
            _ => "Medical records"
        };
    }

    private static bool IsDataHunterDeferredAnswer(string normalized)
    {
        return normalized is "later" or "not now" or "skip" or "defer" or "ask later" or "pause" ||
               normalized.Contains("later", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDataHunterNotSureAnswer(string normalized)
    {
        return normalized is "not sure" or "unsure" or "unknown" or "i do not know" or "i don't know" or "dont know" ||
               normalized.Contains("not sure", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDataHunterNotApplicableAnswer(string normalized)
    {
        return normalized is "not applicable" or "n/a" or "na" or "does not apply" or "doesn't apply" ||
               normalized.Contains("does not apply", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetDataHunterBasicQuestionKey(int index)
    {
        return DataHunterBasicQuestionKeys.Length > index
            ? DataHunterBasicQuestionKeys[index]
            : $"basic_{index + 1}";
    }

    private void RefreshDataHunterProperties()
    {
        OnPropertyChanged(nameof(DataHunterBasicPercent));
        OnPropertyChanged(nameof(DataHunterProgressSummary));
        OnPropertyChanged(nameof(DataHunterStageSummary));
        OnPropertyChanged(nameof(DataHunterPointsSummary));
        OnPropertyChanged(nameof(DataHunterNextQuestion));
        OnPropertyChanged(nameof(DataHunterQuestCardMeta));
        OnPropertyChanged(nameof(DataHunterFirstMasterTargetSummary));
        OnPropertyChanged(nameof(DataHunterFirstMasterTargetDetail));
        OnPropertyChanged(nameof(DataHunterDebugSummary));
        RefreshDataHunterDebugRows();
    }

    private void RefreshDataHunterDebugRows()
    {
        DataHunterDebugRows.Clear();
        var map = LoadDataHunterPersonalDataMap(GetActiveChart());
        var nextIndex = GetNextDataHunterBasicQuestionIndex(map);
        var nextKey = nextIndex.HasValue ? GetDataHunterBasicQuestionKey(nextIndex.Value) : "Basic complete";
        var nextQuestion = nextIndex.HasValue ? DataHunterBasicQuestions[nextIndex.Value] : "No Basic question pending.";
        var savedKeys = map.Answers
            .OrderBy(answer => Array.IndexOf(DataHunterBasicQuestionKeys, answer.Key))
            .Select(answer => $"{answer.Key} ({answer.Status}, +{answer.XpAwarded})")
            .ToList();
        var skippedKeys = DataHunterBasicQuestionKeys
            .Where(key => TryGetDataHunterConditionalParentKey(key, out _) && !ShouldAskDataHunterBasicQuestion(map, key))
            .ToList();
        var categoryReward = map.CategoryRewards
            .OrderByDescending(reward => reward.AwardedAt)
            .FirstOrDefault();
        var firstMasterQuest = GetFirstDataHunterMasterQuest(map);

        DataHunterDebugRows.Add(new DataHunterDebugRowViewModel("Current question", nextKey, nextQuestion));
        DataHunterDebugRows.Add(new DataHunterDebugRowViewModel(
            "Saved answer keys",
            savedKeys.Count == 0 ? "None yet" : string.Join(", ", savedKeys.Take(8)),
            savedKeys.Count > 8 ? $"+{savedKeys.Count - 8} more saved answer keys" : string.Empty));
        DataHunterDebugRows.Add(new DataHunterDebugRowViewModel(
            "Skipped follow-ups",
            skippedKeys.Count == 0 ? "None" : string.Join(", ", skippedKeys.Take(8)),
            skippedKeys.Count > 8 ? $"+{skippedKeys.Count - 8} more skipped conditional questions" : string.Empty));
        DataHunterDebugRows.Add(new DataHunterDebugRowViewModel(
            "XP gained",
            $"{CalculateDataHunterBasicXp(map)} total Basic XP",
            categoryReward is null || categoryReward.XpAwarded <= 0
                ? "No category reward yet"
                : $"Latest category reward: {categoryReward.Category} +{categoryReward.XpAwarded}"));
        DataHunterDebugRows.Add(new DataHunterDebugRowViewModel(
            "Generated Master target",
            firstMasterQuest?.Title ?? "None queued yet",
            firstMasterQuest is null
                ? "Answer record-location questions to generate the first target."
                : $"{BuildDataHunterQuestClueLine(firstMasterQuest)} | {firstMasterQuest.Status}: {FirstNonEmpty(firstMasterQuest.AcceptedEvidenceSummary, firstMasterQuest.EvidenceHint)}"));
    }

    private DataHunterPersonalDataMap LoadDataHunterPersonalDataMap(ChartContext chartContext)
    {
        var mapPath = GetDataHunterPersonalDataMapJsonPath(chartContext);
        try
        {
            if (File.Exists(mapPath))
            {
                var map = JsonSerializer.Deserialize<DataHunterPersonalDataMap>(File.ReadAllText(mapPath));
                if (map is not null)
                {
                    map.ChartId = chartContext.ChartId;
                    map.PatientDisplayName = chartContext.LocalDisplayName;
                    map.Answers ??= [];
                    map.CategoryRewards ??= [];
                    map.MasterQuests ??= [];
                    return map;
                }
            }
        }
        catch
        {
            // Data Hunter progress should never block core chart use.
        }

        return new DataHunterPersonalDataMap
        {
            ChartId = chartContext.ChartId,
            PatientDisplayName = chartContext.LocalDisplayName
        };
    }

    private void SaveDataHunterPersonalDataMap(ChartContext chartContext, DataHunterPersonalDataMap map)
    {
        var systemFolder = Path.Combine(VaultRootPath, chartContext.ChartFolderName, "wiki", "system");
        Directory.CreateDirectory(systemFolder);
        map.ChartId = chartContext.ChartId;
        map.PatientDisplayName = chartContext.LocalDisplayName;
        var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        map.MasterQuests = GenerateDataHunterMasterQuests(map);
        File.WriteAllText(
            Path.Combine(systemFolder, DataHunterPersonalDataMapJsonFileName),
            JsonSerializer.Serialize(map, jsonOptions));
        File.WriteAllText(
            Path.Combine(systemFolder, DataHunterPersonalDataMapMarkdownFileName),
            BuildDataHunterPersonalDataMapMarkdown(map));
    }

    private string GetDataHunterPersonalDataMapJsonPath(ChartContext chartContext)
    {
        return Path.Combine(
            VaultRootPath,
            chartContext.ChartFolderName,
            "wiki",
            "system",
            DataHunterPersonalDataMapJsonFileName);
    }

    private static string BuildDataHunterPersonalDataMapMarkdown(DataHunterPersonalDataMap map)
    {
        var builder = new StringBuilder();
        builder.AppendLine("---");
        builder.AppendLine($"chart_id: \"{EscapeYaml(map.ChartId)}\"");
        builder.AppendLine("document_type: \"Data_Hunter_Personal_Data_Map\"");
        builder.AppendLine("context_status: \"user_provided_context_not_verified_medical_evidence\"");
        builder.AppendLine($"updated_at: \"{map.UpdatedAt:O}\"");
        builder.AppendLine("---");
        builder.AppendLine();
        builder.AppendLine("# Data Hunter Personal Data Map");
        builder.AppendLine();
        builder.AppendLine("> These Basic interview answers are user-provided context for organizing record gathering. They are not accepted vault evidence and do not award Data Master evidence points.");
        builder.AppendLine();
        builder.AppendLine("| Category | Field | Answer | Status | XP | Source | Evidence status |");
        builder.AppendLine("|---|---|---|---|---:|---|---|");
        foreach (var answer in map.Answers.OrderBy(answer => Array.IndexOf(DataHunterBasicQuestionKeys, answer.Key)))
        {
            builder.AppendLine($"| {EscapeMarkdownTable(FirstNonEmpty(answer.Category, GetDataHunterQuestionCategory(answer.Key)))} | {EscapeMarkdownTable(answer.Question)} | {EscapeMarkdownTable(answer.Answer)} | {EscapeMarkdownTable(FirstNonEmpty(answer.Status, DataHunterAnswerStatusAnswered))} | {answer.XpAwarded} | user-provided context | not verified medical evidence |");
        }

        if (map.CategoryRewards.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("## Basic Category Rewards");
            builder.AppendLine();
            builder.AppendLine("| Category | XP | Awarded at |");
            builder.AppendLine("|---|---:|---|");
            foreach (var reward in map.CategoryRewards.OrderBy(reward => reward.AwardedAt))
            {
                builder.AppendLine($"| {EscapeMarkdownTable(reward.Category)} | {reward.XpAwarded} | {reward.AwardedAt:O} |");
            }
        }

        var masterQuests = GenerateDataHunterMasterQuests(map);
        builder.AppendLine();
        builder.AppendLine("## Data Master Gathering Quests");
        builder.AppendLine();
        builder.AppendLine("> These quests are generated from the Personal Data Map. They remain non-awarding until a matching record is accepted into the vault.");
        builder.AppendLine();
        builder.AppendLine("| Quest | Basic clue | Target | Status | Evidence rule |");
        builder.AppendLine("|---|---|---|---|---|");
        foreach (var quest in masterQuests)
        {
            var evidenceRule = string.IsNullOrWhiteSpace(quest.AcceptedEvidenceSummary)
                ? quest.EvidenceHint
                : $"{quest.EvidenceHint}; accepted: {quest.AcceptedEvidenceSummary}";
            builder.AppendLine($"| {EscapeMarkdownTable(quest.Title)} | {EscapeMarkdownTable(quest.SourceClueSummary)} | {EscapeMarkdownTable(quest.Target)} | {EscapeMarkdownTable(quest.Status)} | {EscapeMarkdownTable(evidenceRule)} |");
        }

        return builder.ToString();
    }

    private static List<DataHunterMasterQuest> GenerateDataHunterMasterQuests(DataHunterPersonalDataMap map)
    {
        var priorityRecordText = GetDataHunterAnswer(map, "priority_records");
        var combined = string.Join(
            " ",
            [
                GetDataHunterAnswer(map, "has_primary_care_doctor"),
                GetDataHunterAnswer(map, "primary_care_doctor_name"),
                GetDataHunterAnswer(map, "pcp_has_most_records"),
                GetDataHunterAnswer(map, "home_records"),
                GetDataHunterAnswer(map, "has_patient_portals"),
                GetDataHunterAnswer(map, "patient_portals"),
                GetDataHunterAnswer(map, "has_past_hospital_care"),
                GetDataHunterAnswer(map, "past_hospitals"),
                GetDataHunterAnswer(map, "has_specialists"),
                GetDataHunterAnswer(map, "specialists"),
                GetDataHunterAnswer(map, "has_regular_pharmacy"),
                GetDataHunterAnswer(map, "regular_pharmacy"),
                GetDataHunterAnswer(map, "has_lab_records"),
                GetDataHunterAnswer(map, "lab_record_locations"),
                GetDataHunterAnswer(map, "has_imaging_records"),
                GetDataHunterAnswer(map, "imaging_record_locations"),
                GetDataHunterAnswer(map, "vaccine_record_location"),
                priorityRecordText
            ]);
        var quests = new List<DataHunterMasterQuest>();

        AddDataHunterMasterQuestIfMentioned(quests, map, combined, "portal", "Collect patient portal export", "Portal", "portal export, visit notes, labs, medications, or immunizations accepted into the vault", "portal", "mychart", "epic", "cerner", "athena");
        AddDataHunterMasterQuestIfMentioned(quests, map, combined, "doctor", "Collect doctor or clinic records", "Doctor/clinic", "clinic note, after-visit summary, referral, medication list, or visit record accepted into the vault", "doctor", "pcp", "primary care", "clinic", "specialist", "pediatrician");
        AddDataHunterMasterQuestIfMentioned(quests, map, combined, "hospital", "Collect hospital records", "Hospital", "discharge summary, operative note, ED note, admission note, or hospital packet accepted into the vault", "hospital", "er", "ed", "emergency", "inpatient", "admission", "discharge");
        AddDataHunterMasterQuestIfMentioned(quests, map, combined, "paper", "Scan paper health records", "Paper records", "scanned paper record accepted into the vault", "paper", "folder", "binder", "file cabinet", "printed");
        AddDataHunterMasterQuestIfMentioned(quests, map, combined, "imaging", "Capture imaging reports or CDs", "Imaging", "radiology report, imaging CD index, or official read accepted into the vault", "imaging", "radiology", "xray", "x-ray", "mri", "ct", "ultrasound", "cd");
        AddDataHunterMasterQuestIfMentioned(quests, map, combined, "pharmacy", "Collect pharmacy medication history", "Pharmacy", "pharmacy fill history, medication list, or vaccine record accepted into the vault", "pharmacy", "walgreens", "cvs", "rite aid", "medication", "prescription");
        AddDataHunterMasterQuestIfMentioned(quests, map, combined, "lab", "Collect lab result history", "Labs", "lab report, portal lab export, or result table accepted into the vault", "lab", "labs", "quest", "labcorp", "bloodwork", "blood work");
        AddDataHunterMasterQuestIfMentioned(quests, map, combined, "vaccine", "Upload vaccine card or immunization record", "Vaccines", "vaccine card, immunization registry export, or portal vaccine table accepted into the vault", "vaccine", "vaccines", "immunization", "shot record");

        AddDataHunterMasterQuestIfPositive(quests, map, "has_primary_care_doctor", "doctor", "Collect primary care records", "Primary care doctor", "primary care problem list, visit notes, medication list, vaccine record, or summary accepted into the vault");
        AddDataHunterMasterQuestIfPositive(quests, map, "home_records", "paper", "Scan home medical records", "Home records", "home paper record, folder, PDF, disc, or photo accepted into the vault");
        AddDataHunterMasterQuestIfPositive(quests, map, "has_patient_portals", "portal", "Collect patient portal export", "Patient portal", "portal export, visit note, lab result, medication list, or vaccine table accepted into the vault");
        AddDataHunterMasterQuestIfPositive(quests, map, "has_past_hospital_care", "hospital", "Collect hospital records", "Hospital", "discharge summary, ED note, admission note, operative note, or hospital packet accepted into the vault");
        AddDataHunterMasterQuestIfPositive(quests, map, "has_specialists", "specialist", "Collect specialist records", "Specialist", "specialist note, test result, referral, or treatment summary accepted into the vault");
        AddDataHunterMasterQuestIfPositive(quests, map, "has_regular_pharmacy", "pharmacy", "Collect pharmacy medication history", "Pharmacy", "pharmacy fill history, medication list, or vaccine record accepted into the vault");
        AddDataHunterMasterQuestIfPositive(quests, map, "has_lab_records", "lab", "Collect lab result history", "Labs", "lab report, portal lab export, or result table accepted into the vault");
        AddDataHunterMasterQuestIfPositive(quests, map, "has_imaging_records", "imaging", "Capture imaging reports or CDs", "Imaging", "radiology report, imaging CD index, or official read accepted into the vault");

        foreach (var target in ExtractPriorityRecordTargets(priorityRecordText).Take(5))
        {
            var questId = $"data_hunter.master.priority.{ToQuestSlug(target)}";
            if (quests.Any(quest => quest.QuestId.Equals(questId, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            quests.Add(new DataHunterMasterQuest
            {
                QuestId = questId,
                Title = $"Gather priority record: {target}",
                Target = target,
                Status = DataHunterMasterQuestStatusPendingEvidence,
                EvidenceHint = "matching priority record accepted into the vault",
                SourceClueSummary = BuildDataHunterAnswerClue(map, "priority_records")
            });
        }

        if (quests.Count == 0 && map.Answers.Any(answer => !string.IsNullOrWhiteSpace(answer.Answer)))
        {
            quests.Add(new DataHunterMasterQuest
            {
                QuestId = "data_hunter.master.record_location",
                Title = "Name the first record source to gather",
                Target = "Record source",
                Status = "Needs Basic map detail",
                EvidenceHint = "add portal, doctor, hospital, paper, imaging, pharmacy, lab, vaccine card, or priority record location",
                SourceClueSummary = "Basic has context, but no record source clue yet"
            });
        }

        var generatedQuests = quests
            .GroupBy(quest => quest.QuestId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(quest => quest.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
        PreserveDataHunterMasterQuestEvidenceState(generatedQuests, map.MasterQuests);
        return generatedQuests;
    }

    private static void PreserveDataHunterMasterQuestEvidenceState(
        IReadOnlyList<DataHunterMasterQuest> generatedQuests,
        IReadOnlyList<DataHunterMasterQuest> existingQuests)
    {
        foreach (var generated in generatedQuests)
        {
            var existing = existingQuests.FirstOrDefault(quest =>
                quest.QuestId.Equals(generated.QuestId, StringComparison.OrdinalIgnoreCase));
            if (existing is null)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(existing.Status) &&
                !existing.Status.Equals(DataHunterMasterQuestStatusPendingEvidence, StringComparison.OrdinalIgnoreCase) &&
                !existing.Status.Equals("Needs Basic map detail", StringComparison.OrdinalIgnoreCase))
            {
                generated.Status = existing.Status;
            }

            generated.AcceptedEvidenceSummary = existing.AcceptedEvidenceSummary;
            generated.AcceptedEvidenceSourceId = existing.AcceptedEvidenceSourceId;
            generated.AcceptedAt = existing.AcceptedAt;
            if (!string.IsNullOrWhiteSpace(existing.SourceClueSummary))
            {
                generated.SourceClueSummary = existing.SourceClueSummary;
            }
        }
    }

    private static DataHunterMasterQuest? GetFirstDataHunterMasterQuest(DataHunterPersonalDataMap map)
    {
        return GenerateDataHunterMasterQuests(map)
            .OrderBy(quest => quest.Status.Equals(DataHunterMasterQuestStatusPendingEvidence, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(quest => quest.Title, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    private static void AddDataHunterMasterQuestIfPositive(
        List<DataHunterMasterQuest> quests,
        DataHunterPersonalDataMap map,
        string answerKey,
        string questSlug,
        string title,
        string target,
        string evidenceHint)
    {
        if (!IsPositiveDataHunterAnswer(GetDataHunterAnswer(map, answerKey)) ||
            quests.Any(quest => quest.QuestId.Equals($"data_hunter.master.{questSlug}", StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        quests.Add(new DataHunterMasterQuest
        {
            QuestId = $"data_hunter.master.{questSlug}",
            Title = title,
            Target = target,
            Status = DataHunterMasterQuestStatusPendingEvidence,
            EvidenceHint = evidenceHint,
            SourceClueSummary = BuildDataHunterAnswerClue(map, answerKey)
        });
    }

    private static void AddDataHunterMasterQuestIfMentioned(
        List<DataHunterMasterQuest> quests,
        DataHunterPersonalDataMap map,
        string sourceText,
        string questSlug,
        string title,
        string target,
        string evidenceHint,
        params string[] needles)
    {
        if (!ContainsAny(sourceText, needles))
        {
            return;
        }

        quests.Add(new DataHunterMasterQuest
        {
            QuestId = $"data_hunter.master.{questSlug}",
            Title = title,
            Target = target,
            Status = DataHunterMasterQuestStatusPendingEvidence,
            EvidenceHint = evidenceHint,
            SourceClueSummary = BuildDataHunterMentionClue(map, needles)
        });
    }

    private static string BuildDataHunterAnswerClue(DataHunterPersonalDataMap map, string answerKey)
    {
        var answer = map.Answers.FirstOrDefault(answer =>
            answer.Key.Equals(answerKey, StringComparison.OrdinalIgnoreCase));
        if (answer is null)
        {
            return $"Basic answer: {answerKey}";
        }

        return $"{FirstNonEmpty(answer.Category, GetDataHunterQuestionCategory(answer.Key))} - {FirstNonEmpty(answer.Answer, answer.Question)}";
    }

    private static string BuildDataHunterMentionClue(DataHunterPersonalDataMap map, IReadOnlyList<string> needles)
    {
        var answer = map.Answers.FirstOrDefault(answer =>
            ContainsAny($"{answer.Question} {answer.Answer}", needles.ToArray()));
        if (answer is null)
        {
            return "Basic record-location answer mentioned this source";
        }

        return $"{FirstNonEmpty(answer.Category, GetDataHunterQuestionCategory(answer.Key))} - {FirstNonEmpty(answer.Answer, answer.Question)}";
    }

    private static string GetDataHunterAnswer(DataHunterPersonalDataMap map, string key)
    {
        return map.Answers
            .FirstOrDefault(answer => answer.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
            ?.Answer ?? string.Empty;
    }

    private static IEnumerable<string> ExtractPriorityRecordTargets(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            yield break;
        }

        foreach (var part in Regex.Split(value, @"[,;]|\band\b|\bthen\b", RegexOptions.IgnoreCase))
        {
            var cleaned = Regex.Replace(part.Trim(), @"\b(first|priority|prioritize|gather|collect|get|records?|medical|useful|would|feel|most)\b", string.Empty, RegexOptions.IgnoreCase).Trim();
            cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim(' ', '.', ':', '-');
            if (cleaned.Length >= 4 && cleaned.Length <= 80)
            {
                yield return cleaned;
            }
        }
    }

    private static string ToQuestSlug(string value)
    {
        var normalized = Regex.Replace(value.ToLowerInvariant(), @"[^a-z0-9]+", "_").Trim('_');
        return string.IsNullOrWhiteSpace(normalized) ? "record" : normalized;
    }

    private static bool ContainsAny(string value, params string[] needles)
    {
        return needles.Any(needle => value.Contains(needle, StringComparison.OrdinalIgnoreCase));
    }

    private static string EscapeYaml(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    private static string EscapeMarkdownTable(string value)
    {
        return value.Replace("|", "\\|", StringComparison.Ordinal).Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
    }

    private void RefreshVitaMasteryRows()
    {
        VitaMasteryActiveQuestRows.Clear();
        VitaMasteryFoundationQuestRows.Clear();
        VitaMasteryPreventiveQuestRows.Clear();
        VitaMasteryDeepSignalQuestRows.Clear();

        if (_lastVitaMasteryResult is null)
        {
            return;
        }

        foreach (var quest in _lastVitaMasteryResult.ActiveMicroQuests)
        {
            VitaMasteryActiveQuestRows.Add(new VitaMasteryQuestRowViewModel(quest));
        }

        foreach (var quest in _lastVitaMasteryResult.QuestResults.OrderBy(result => result.Quest.Title, StringComparer.OrdinalIgnoreCase))
        {
            var row = new VitaMasteryQuestRowViewModel(quest);
            switch (quest.Quest.Stage)
            {
                case VitaMasteryStage.Foundation:
                    VitaMasteryFoundationQuestRows.Add(row);
                    break;
                case VitaMasteryStage.PreventiveMap:
                    VitaMasteryPreventiveQuestRows.Add(row);
                    break;
                case VitaMasteryStage.DeepSignal:
                    VitaMasteryDeepSignalQuestRows.Add(row);
                    break;
            }
        }
    }

    private void OnStatusChanged()
    {
        OnPropertyChanged(nameof(LocalScrubberStatus));
        OnPropertyChanged(nameof(PayloadStatus));
        OnPropertyChanged(nameof(LlmActivityStatus));
        OnPropertyChanged(nameof(VaultWriteStatus));
    }

    private void SetApiLlmState(bool isWorking, string status)
    {
        IsApiLlmWorking = isWorking;
        ApiLlmStatus = string.IsNullOrWhiteSpace(status)
            ? (isWorking ? "API working" : "API idle")
            : status;
    }

    private void SetLocalLlmState(string endpoint, bool isWorking, string status)
    {
        IsHostLlmWorking = isWorking;
        HostLlmStatus = string.IsNullOrWhiteSpace(status)
            ? (isWorking ? "Local helper working" : "Local helper idle")
            : status;
    }

    private void CaptureSymptomMentionIfPresent(ChartContext chartContext, string userText)
    {
        var symptomResult = _symptomWatcherService.CaptureIfSymptomMention(
            VaultRootPath,
            chartContext,
            userText,
            _conversationArchiveService.SessionId);

        _lastSymptomWatcherResult = symptomResult;

        if (symptomResult.WasCaptured)
        {
            VaultWriteStatus = "Symptom journal updated";
        }
    }

    private void CaptureHealthGoalIfPresent(ChartContext chartContext, string userText)
    {
        var goalResult = _healthGoalEngineService.CaptureIfGoalMention(
            VaultRootPath,
            chartContext,
            userText,
            _conversationArchiveService.SessionId);

        _lastHealthGoalResult = goalResult;

        if (goalResult.WasCaptured)
        {
            VaultWriteStatus = "Health goal captured";
        }
    }

    private void CaptureHealthPreferenceIfPresent(ChartContext chartContext, string userText)
    {
        var preferenceUserName = ResolvePreferenceUserName(chartContext);
        var result = _healthPreferenceService.CaptureIfPreferenceMention(
            VaultRootPath,
            preferenceUserName,
            userText,
            _conversationArchiveService.SessionId);

        _lastHealthPreferenceResult = result;

        if (result.WasCaptured)
        {
            VaultWriteStatus = "Health preference captured";
        }
    }

    private void MaybeOfferWeeklyHealthPreferenceCheckIn(ChartContext chartContext)
    {
        if (IsThinking)
        {
            return;
        }

        var preferenceUserName = ResolvePreferenceUserName(chartContext);
        var prompt = _healthPreferenceService.BuildWeeklyCheckInPromptIfDue(
            VaultRootPath,
            preferenceUserName,
            DateTime.Now);

        if (string.IsNullOrWhiteSpace(prompt))
        {
            return;
        }

        Messages.Add(new ChatMessageViewModel(
            MessageAuthor.Dolly,
            AgentLabel,
            [prompt]));
    }

    private async Task HandleTypedOmniboxWithLocalGemmaAsync(
        string userText,
        ChatMessageViewModel thinkingMessage,
        PatientNameResolution patientNameResolution)
    {
        var chartContext = patientNameResolution.ChartContext ?? GetActiveChart();

        ActiveChartId = chartContext.ChartId;
        ActivePatientDisplayName = chartContext.LocalDisplayName;

        VaultWriteStatus = $"{AgentDisplayName} is reading";
        UpdateDollyStatus(
            thinkingMessage,
            $"I am reading that now.");
        RefreshPreview();

        var localSessionContext = BuildLocalSessionContextForGemma(chartContext);
        var packetResult = await AskGemmaForActionPacketAsync(
            userText,
            chartContext,
            localSessionContext,
            thinkingMessage);

        if (!packetResult.WasValid || packetResult.Packet is null)
        {
            VaultWriteStatus = packetResult.Status;
            Messages.Remove(thinkingMessage);
            AddArchivedMessage(
                MessageAuthor.Dolly,
                AgentLabel,
                [
                    BuildGemmaActionPacketFallbackMessage(packetResult),
                    "I did not write anything. Try naming the patient and task directly, or ask a read-only chart question and I will use the safe chart context."
                ],
                mode: "dolly_api_action_packet_invalid",
                linkedChartId: chartContext.ChartId);

            PromptText = string.Empty;
            IsThinking = false;
            RefreshPreview();
            SendMessageCommand.RaiseCanExecuteChanged();
            return;
        }

        await DispatchGemmaActionPacketAsync(
            packetResult.Packet,
            userText,
            thinkingMessage,
            chartContext,
            localSessionContext,
            packetResult.Attempts);
    }

    private async Task HandleTypedOmniboxWithBackendAsync(
        string userText,
        ChatMessageViewModel thinkingMessage,
        PatientNameResolution patientNameResolution,
        bool skipPatchPlanner = false)
    {
        VaultWriteStatus = "Preparing safe chart request";
        UpdateDollyStatus(thinkingMessage, "I am preparing a safe chart request now.");
        RefreshPreview();

        var knownPatients = _patientRegistryService.GetAllIdentities();
        var scrubbedUserText = DeidentifyForApi(userText, knownPatients);
        var chartContext = patientNameResolution.ChartContext ?? ResolveExistingChartForBackend(patientNameResolution.Preflight);
        var contextPacket = chartContext is null
            ? _vaultContextService.BuildFamilyVaultRosterContext(VaultRootPath, knownPatients)
            : _vaultContextService.BuildSterileContext(VaultRootPath, chartContext, scrubbedUserText);

        if (chartContext is not null)
        {
            ActiveChartId = chartContext.ChartId;
            ActivePatientDisplayName = chartContext.LocalDisplayName;
            CaptureSymptomMentionIfPresent(chartContext, userText);
            CaptureHealthGoalIfPresent(chartContext, userText);
        }

        if (chartContext is not null && !skipPatchPlanner)
        {
            VaultWriteStatus = "Checking whether the chart should update";
            UpdateDollyStatus(thinkingMessage, "I am checking whether this should update the living wiki. I will only make a change after it passes validation.");
            RefreshPreview();

            SetApiLlmState(true, "API patch planner");
            GeminiWikiPatchResult patchPlan;

            try
            {
                patchPlan = await _geminiWikiPatchService.PlanPatchAsync(
                    EnableGeminiBackendProcessing,
                    GeminiThinkingModelName,
                    scrubbedUserText,
                    BuildRecentConversationContext(),
                    contextPacket);
            }
            finally
            {
                SetApiLlmState(false, "API ready");
            }

            if (!patchPlan.WasAvailable && ShouldUseLocalGemmaFallback(patchPlan.Status))
            {
                VaultWriteStatus = "Agent connection unavailable; trying local fallback";
                UpdateDollyStatus(thinkingMessage, "The agent connection is temporarily unavailable or rate-limited. I am trying the local fallback from the safe context.");
                RefreshPreview();
                SetLocalLlmState(LocalModelEndpoint, true, "Local fallback");

                try
                {
                    patchPlan = await _localWikiPatchService.PlanPatchAsync(
                        LocalModelEndpoint,
                        LocalModelName,
                        scrubbedUserText,
                        BuildRecentConversationContext(),
                        contextPacket);
                }
                finally
                {
                    SetLocalLlmState(LocalModelEndpoint, false, "Local helper ready");
                }
            }

            if (patchPlan.WasAvailable && patchPlan.ShouldApply)
            {
                VaultWriteStatus = "Writing manual-entry raw source";
                UpdateDollyStatus(thinkingMessage, "The backend returned a patch plan. C# is saving your original update as a manual-entry raw source first.");
                RefreshPreview();
                var rawManualSource = _vaultIngestService.IngestRawText(
                    chartContext,
                    userText,
                    "Omnibox_Manual_Update.md");

                VaultWriteStatus = "Asking API to rewrite living wiki files";
                UpdateDollyStatus(thinkingMessage, "The backend returned a patch plan. I am asking it to rewrite the relevant wiki files, then a second check will verify the rewrite before C# saves it.");
                RefreshPreview();
                SetApiLlmState(true, "API wiki rewrite");
                WikiRewriteApplyResult rewriteResult;

                try
                {
                    rewriteResult = await _geminiWikiRewriteService.RewriteAndApplyAsync(
                        EnableGeminiBackendProcessing,
                        GeminiThinkingModelName,
                        VaultRootPath,
                        chartContext,
                        patchPlan,
                        rawManualSource,
                        scrubbedUserText,
                        BuildRecentConversationContext());
                }
                finally
                {
                    SetApiLlmState(false, "API ready");
                }

                WikiPatchApplyResult applyResult;

                if (rewriteResult.WasApplied)
                {
                    applyResult = new WikiPatchApplyResult
                    {
                        WasApplied = true,
                        UpdatedFiles = rewriteResult.UpdatedFiles,
                        Messages = rewriteResult.Messages
                    };
                }
                else
                {
                    VaultWriteStatus = "LLM rewrite not applied; using C# fallback writer";
                    UpdateDollyStatus(thinkingMessage, "The LLM rewrite did not pass or was unavailable, so I am using the older C# wiki writer as a fallback for this update.");
                    RefreshPreview();
                    applyResult = _wikiPatchApplyService.Apply(
                        VaultRootPath,
                        chartContext,
                        patchPlan,
                        rawManualSource);
                    applyResult.Messages.Insert(0, $"LLM rewrite path skipped: {rewriteResult.Status}");
                    applyResult.Messages.AddRange(rewriteResult.Messages);
                }

                VaultWriteStatus = applyResult.WasApplied
                    ? "Living wiki patch applied"
                    : "Living wiki patch skipped";
                QueueBackgroundSummaryRefresh(chartContext, "wiki patch completed");
                _chronosLedgerService.RecordEvent(
                    VaultRootPath,
                    chartContext,
                    applyResult.WasApplied ? "wiki_patch_applied" : "wiki_patch_skipped",
                    applyResult.WasApplied
                        ? "Living wiki patch was validated and applied."
                        : "Living wiki patch was skipped after validation.",
                    "C#",
                    "wiki_patch");
                RefreshPreview();

                Messages.Remove(thinkingMessage);
                AddArchivedMessage(
                    MessageAuthor.Dolly,
                    AgentLabel,
                    BuildWikiPatchMessageLines(patchPlan, applyResult),
                    mode: "wiki_patch_applied",
                    linkedChartId: chartContext.ChartId);

                PromptText = string.Empty;
                IsThinking = false;
                RefreshPreview();
                SendMessageCommand.RaiseCanExecuteChanged();
                return;
            }

            if (patchPlan.WasAvailable && !string.IsNullOrWhiteSpace(patchPlan.AnswerIfNoPatch))
            {
                VaultWriteStatus = "Backend answer ready";
                UpdateDollyStatus(thinkingMessage, "The backend answered from the patch-planner pass. I am presenting it now.");
                RefreshPreview();

                Messages.Remove(thinkingMessage);
                AddArchivedMessage(
                    MessageAuthor.Dolly,
                    AgentLabel,
                    [_responsePersonalizationService.Personalize(patchPlan.AnswerIfNoPatch, chartContext, knownPatients)],
                    mode: "backend_omnibox_answer",
                    linkedChartId: chartContext.ChartId);

                PromptText = string.Empty;
                IsThinking = false;
                RefreshPreview();
                SendMessageCommand.RaiseCanExecuteChanged();
                return;
            }
        }

        VaultWriteStatus = chartContext is null
            ? "Sending safe request plus roster context"
            : "Sending safe request plus chart outline";
        UpdateDollyStatus(
            thinkingMessage,
            chartContext is null
                ? "I am checking the safe request against the family-vault roster now."
                : "I am checking the safe request against the chart outline now.");
        RefreshPreview();

        SetApiLlmState(true, $"{AgentDisplayName} chart answer");
        GeminiOmniboxAnswerResult backendAnswer;
        var streamedAnswer = new StringBuilder();

        try
        {
            backendAnswer = await _geminiOmniboxAnswerService.AnswerStreamingAsync(
                EnableGeminiBackendProcessing,
                GeminiThinkingModelName,
                scrubbedUserText,
                BuildRecentConversationContext(),
                contextPacket,
                delta => AppendStreamingDraft(thinkingMessage, streamedAnswer, delta));
        }
        finally
        {
            SetApiLlmState(false, "API ready");
        }

        VaultWriteStatus = backendAnswer.WasAvailable
            ? "Backend answer ready"
            : backendAnswer.Status;
        UpdateDollyStatus(
            thinkingMessage,
            backendAnswer.WasAvailable
                ? "The backend returned an answer from the sterile chart outline. I am presenting it now."
                : "The chart answer did not come back. I am not going to guess from incomplete context.");
        RefreshPreview();

        Messages.Remove(thinkingMessage);
        AddArchivedMessage(
            MessageAuthor.Dolly,
            AgentLabel,
            chartContext is null
                ? SplitChartAnswerIntoFriendlyParagraphs(_responsePersonalizationService.Personalize(backendAnswer.Answer, chartContext, knownPatients))
                : PolishChartAnswerForChat(
                    _responsePersonalizationService.Personalize(backendAnswer.Answer, chartContext, knownPatients),
                    chartContext,
                    knownPatients),
            mode: "backend_omnibox_answer",
            linkedChartId: chartContext?.ChartId ?? ActiveChartId);

        PromptText = string.Empty;
        IsThinking = false;
        RefreshPreview();
        SendMessageCommand.RaiseCanExecuteChanged();
    }

    private async Task<PatientNameResolution> ResolvePatientForSendAsync(
        string userText,
        IReadOnlyList<AttachmentViewModel> sentAttachments)
    {
        var hasImageAttachment = sentAttachments.Any(IsImageAttachment);
        var hasAttachments = sentAttachments.Count > 0;
        var knownPatients = _patientRegistryService.GetAllIdentities();

        var preflight = await ReviewOmniboxPreflightWithFailoverAsync(
            userText,
            sentAttachments,
            knownPatients);

        if (!preflight.IsAvailable)
        {
            return new PatientNameResolution(
                false,
                null,
                "I could not get the local fallback to read this instruction, so I did not write anything. Please retry once the helper is available.",
                preflight,
                false);
        }

        if (preflight.NeedsClarification)
        {
            return new PatientNameResolution(
                false,
                null,
                string.IsNullOrWhiteSpace(preflight.ClarifyingQuestion)
                    ? "Please clarify which patient this should be stored under before I write anything to the vault."
                    : preflight.ClarifyingQuestion,
                preflight,
                false);
        }

        var patientName = ChooseGemmaPatientName(preflight);

        if (!IsVaultQuestion(preflight) &&
            !hasAttachments &&
            string.IsNullOrWhiteSpace(patientName) &&
            !preflight.ProcessingRequired &&
            IsMeaningfulLocalDisplayName(ActivePatientDisplayName))
        {
            patientName = ActivePatientDisplayName;
        }

        var shouldConfirmTypedCapture = !hasAttachments &&
                                        preflight.ProcessingRequired &&
                                        IsProcessingIntent(preflight.IntendedAction);

        if (IsVaultQuestion(preflight) && string.IsNullOrWhiteSpace(patientName))
        {
            return new PatientNameResolution(
                false,
                null,
                "Which patient should I check in the vault?",
                preflight,
                false);
        }

        if (shouldConfirmTypedCapture && string.IsNullOrWhiteSpace(patientName))
        {
            return new PatientNameResolution(
                false,
                null,
                "This looks like medical information. Which patient should I store it under?",
                preflight,
                false);
        }

        if (hasAttachments &&
            IsNewPatientIntent(preflight.IntendedAction) &&
            string.IsNullOrWhiteSpace(patientName))
        {
            return new PatientNameResolution(
                false,
                null,
                "I see this is a new-patient note, but I could not confidently read the name. Please provide the patient name before I store the attachment.",
                preflight,
                false);
        }

        if (!hasImageAttachment && string.IsNullOrWhiteSpace(patientName))
        {
            return new PatientNameResolution(true, GetActiveChart(), string.Empty, preflight, false);
        }

        if (string.IsNullOrWhiteSpace(patientName))
        {
            return new PatientNameResolution(
                false,
                null,
                "Please provide the patient name for this image before I store it. The image is still queued and has not been written to the vault.",
                preflight,
                false);
        }

        var identity = ResolvePreflightIdentity(preflight);

        if (identity is null)
        {
            return new PatientNameResolution(
                false,
                null,
                string.IsNullOrWhiteSpace(preflight.ChartId)
                    ? $"I found the name {patientName}, but I do not see that patient in the sealed registry. To create a new chart, use the Add New Patient dropdown path."
                    : "The agent returned a patient/chart match that did not validate against the sealed registry, so I did not write anything.",
                preflight,
                false);
        }

        return new PatientNameResolution(
            true,
            new ChartContext(identity.ChartId, identity.PatientDisplayName),
            string.Empty,
               preflight,
            shouldConfirmTypedCapture);
    }

    private static bool IsNewPatientIntent(string intendedAction)
    {
        return intendedAction.Equals("add_patient", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsProcessingIntent(string intendedAction)
    {
        return intendedAction.Equals("ingest_document", StringComparison.OrdinalIgnoreCase) ||
               intendedAction.Equals("update_record", StringComparison.OrdinalIgnoreCase) ||
               intendedAction.Equals("process_medical_data", StringComparison.OrdinalIgnoreCase) ||
               intendedAction.Equals("attachment_intake", StringComparison.OrdinalIgnoreCase) ||
               intendedAction.Equals("add_patient", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsVaultQuestion(OmniboxPreflightResult? preflight)
    {
        return preflight?.IntendedAction.Equals("vault_question", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static bool IsVaultRosterQuestion(OmniboxPreflightResult? preflight)
    {
        return preflight?.IntendedAction.Equals("vault_roster", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static bool IsApiDollyAgentFirst(OmniboxPreflightResult? preflight)
    {
        return preflight?.Status.Equals("API_DOLLY_AGENT_FIRST", StringComparison.OrdinalIgnoreCase) == true ||
               preflight?.IntendedAction.Equals("api_dolly_agent", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static bool IsRosterQuestionText(string value)
    {
        var normalized = NormalizeConfirmation(value);

        return normalized.Contains("what other patient", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("other patients", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("other pts", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("what other pts", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("pts in my chart", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("patients in my chart", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("patient files", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("who is in the vault", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("family vault", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("who do you have", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("what patients are we dealing with", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("patients are we dealing with", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("who are we dealing with", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("roster", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeChartQuestionText(string value)
    {
        var normalized = NormalizeConfirmation(value);

        if (!normalized.Contains("?", StringComparison.OrdinalIgnoreCase) &&
            !normalized.Contains("tell me", StringComparison.OrdinalIgnoreCase) &&
            !normalized.Contains("what", StringComparison.OrdinalIgnoreCase) &&
            !normalized.Contains("last", StringComparison.OrdinalIgnoreCase) &&
            !normalized.Contains("about", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return normalized.Contains("chart", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("patient", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("pt", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("bruce", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("cardio", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("visit", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("note", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("echo", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("lab", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("med", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeSmallTalkText(string value)
    {
        var normalized = NormalizeConfirmation(value);

        return normalized.Equals("hi", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("hello", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("hello dolly", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("hey", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("hey dolly", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("good am", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("good day", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("good morning", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("good afternoon", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("good evening", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("thank", StringComparison.OrdinalIgnoreCase);
    }

    private void AnswerFamilyRosterQuestion(ChatMessageViewModel thinkingMessage)
    {
        var patients = _patientRegistryService.GetAllIdentities()
            .Where(patient => !string.IsNullOrWhiteSpace(patient.PatientDisplayName))
            .OrderBy(patient => patient.PatientDisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(patient => patient.PatientDisplayName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var lines = patients.Count == 0
            ? new[]
            {
                "I do not see any patient charts in the sealed registry yet.",
                "Once a synthetic chart is created or selected, I can answer from that chart's sterile context."
            }
            : new[]
            {
                $"I found {patients.Count} patient chart{(patients.Count == 1 ? string.Empty : "s")} in the sealed registry: {string.Join(", ", patients)}.",
                "Tell me which one you want, and I can check the sterile chart context without touching raw source files."
            };

        PromptText = string.Empty;
        Messages.Remove(thinkingMessage);
        AddArchivedMessage(
            MessageAuthor.Dolly,
            AgentLabel,
            lines,
            mode: "vault_roster_deterministic",
            linkedChartId: ActiveChartId);
        VaultWriteStatus = "Vault roster read only";
        IsThinking = false;
        RefreshPreview();
        SendMessageCommand.RaiseCanExecuteChanged();
    }

    private PatientNameResolution EnsureReadOnlyChartResolution(PatientNameResolution resolution)
    {
        if (resolution.ChartContext is not null)
        {
            return resolution;
        }

        var activeChart = GetActiveChart();
        return new PatientNameResolution(
            true,
            activeChart,
            string.Empty,
            resolution.Preflight,
            false);
    }

    private PatientNameResolution BuildReadOnlyResolution(ChartContext chartContext)
    {
        return new PatientNameResolution(
            true,
            chartContext,
            string.Empty,
            new OmniboxPreflightResult
            {
                WasAttempted = false,
                IsAvailable = true,
                Status = "RULE_BASED_READ_ONLY",
                IntendedAction = "vault_question",
                RecommendedMode = "thinking",
                ProcessingRequired = false,
                PatientReferencePresent = true,
                PatientReference = chartContext.LocalDisplayName,
                PatientDisplayName = chartContext.LocalDisplayName,
                ChartId = chartContext.ChartId,
                Confidence = "1.0"
            },
            false);
    }

    private PatientNameResolution BuildAgenticTextResolution(ChartContext chartContext)
    {
        return new PatientNameResolution(
            true,
            chartContext,
            string.Empty,
            new OmniboxPreflightResult
            {
                WasAttempted = false,
                IsAvailable = true,
                Status = "API_DOLLY_AGENT_FIRST",
                IntendedAction = "api_dolly_agent",
                RecommendedMode = "thinking",
                ProcessingRequired = false,
                PatientReferencePresent = true,
                PatientReference = chartContext.LocalDisplayName,
                PatientDisplayName = chartContext.LocalDisplayName,
                ChartId = chartContext.ChartId,
                Confidence = "1.0",
                DollyReply = "I am asking Dolly's API agent to read this first."
            },
            false);
    }

    private ChartContext? ResolveReadOnlyChartFromText(string userText)
    {
        var normalized = NormalizeConfirmation(userText);
        var knownPatients = _patientRegistryService.GetAllIdentities()
            .Where(patient => !string.IsNullOrWhiteSpace(patient.PatientDisplayName))
            .OrderByDescending(patient => patient.PatientDisplayName.Length)
            .ToList();

        foreach (var patient in knownPatients)
        {
            var displayName = NormalizeConfirmation(patient.PatientDisplayName);
            if (!string.IsNullOrWhiteSpace(displayName) &&
                normalized.Contains(displayName, StringComparison.OrdinalIgnoreCase))
            {
                return new ChartContext(patient.ChartId, patient.PatientDisplayName);
            }

            var firstName = displayName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(firstName) &&
                Regex.IsMatch(normalized, $@"\b{Regex.Escape(firstName)}\b", RegexOptions.IgnoreCase))
            {
                return new ChartContext(patient.ChartId, patient.PatientDisplayName);
            }
        }

        return IsMeaningfulLocalDisplayName(ActivePatientDisplayName)
            ? GetActiveChart()
            : null;
    }

    private static bool ShouldUseLocalGemmaFallback(string status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return false;
        }

        return status.Contains("HTTP_429", StringComparison.OrdinalIgnoreCase) ||
               status.Contains("TIMEOUT", StringComparison.OrdinalIgnoreCase) ||
               status.Contains("UNAVAILABLE", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildGemmaActionPacketFallbackMessage(GemmaActionPacketResult result)
    {
        if (result.Attempts <= 0 ||
            result.Status.Contains("UNAVAILABLE", StringComparison.OrdinalIgnoreCase) ||
            result.Status.Contains("TIMEOUT", StringComparison.OrdinalIgnoreCase))
        {
            return "I could not reach the agent for that task, so I stopped before taking action.";
        }

        return "I could not map that request cleanly enough to run a tool or write to the chart.";
    }

    private ChartContext? ResolveExistingChartForBackend(OmniboxPreflightResult? preflight)
    {
        if (preflight is null || !preflight.PatientReferencePresent)
        {
            return IsMeaningfulLocalDisplayName(ActivePatientDisplayName)
                ? GetActiveChart()
                : null;
        }

        var patientName = ChooseGemmaPatientName(preflight);

        if (string.IsNullOrWhiteSpace(patientName))
        {
            return IsMeaningfulLocalDisplayName(ActivePatientDisplayName)
                ? GetActiveChart()
                : null;
        }

        var identity = ResolvePreflightIdentity(preflight);

        return identity is null
            ? null
            : new ChartContext(identity.ChartId, identity.PatientDisplayName);
    }

    private PatientIdentityRecord? ResolvePreflightIdentity(OmniboxPreflightResult preflight)
    {
        var knownPatients = _patientRegistryService.GetAllIdentities();
        var patientName = ChooseGemmaPatientName(preflight);

        if (!string.IsNullOrWhiteSpace(preflight.ChartId))
        {
            var chartMatch = knownPatients.FirstOrDefault(patient =>
                patient.ChartId.Equals(preflight.ChartId.Trim(), StringComparison.OrdinalIgnoreCase));

            if (chartMatch is null)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(patientName) &&
                !chartMatch.PatientDisplayName.Equals(patientName, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return chartMatch;
        }

        return string.IsNullOrWhiteSpace(patientName)
            ? null
            : _patientRegistryService.TryResolveIdentityByName(patientName);
    }

    private static string DeidentifyForApi(string value, IReadOnlyList<PatientIdentityRecord> knownPatients)
    {
        var scrubbed = string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim();

        foreach (var patient in knownPatients)
        {
            if (string.IsNullOrWhiteSpace(patient.PatientDisplayName))
            {
                continue;
            }

            scrubbed = Regex.Replace(
                scrubbed,
                Regex.Escape(patient.PatientDisplayName),
                "[PATIENT]",
                RegexOptions.IgnoreCase);
        }

        scrubbed = Regex.Replace(scrubbed, @"\bVITA-\d{4,6}\b", "[CHART_ID]", RegexOptions.IgnoreCase);
        scrubbed = Regex.Replace(scrubbed, @"\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b", "[EMAIL]", RegexOptions.IgnoreCase);
        scrubbed = Regex.Replace(scrubbed, @"\b(?:\+?1[-.\s]?)?(?:\(?\d{3}\)?[-.\s]?)\d{3}[-.\s]?\d{4}\b", "[PHONE]");
        scrubbed = Regex.Replace(scrubbed, @"\b\d{3}-\d{2}-\d{4}\b", "[SSN]");
        scrubbed = Regex.Replace(scrubbed, @"\b(?:\d{1,2}[/-]\d{1,2}[/-]\d{2,4}|\d{4}-\d{2}-\d{2})\b", "[DATE]");
        scrubbed = Regex.Replace(scrubbed, @"\b(?:MRN|Medical Record|Account|Acct|Patient ID|Member ID|ID)\s*[:#-]?\s*[A-Z0-9-]{4,}\b", "[MEDICAL_ID]", RegexOptions.IgnoreCase);

        return scrubbed;
    }

    private static async Task<string> ScrubOmniboxTextWithPresidioForApiAsync(string userText)
    {
        if (string.IsNullOrWhiteSpace(userText))
        {
            return string.Empty;
        }

        try
        {
            using var httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(10)
            };
            using var response = await httpClient.PostAsJsonAsync(
                "http://localhost:8001/scrub_text",
                new PresidioTextRequest(userText));

            if (!response.IsSuccessStatusCode)
            {
                return userText;
            }

            var result = await response.Content.ReadFromJsonAsync<PresidioTextResponse>();
            return string.IsNullOrWhiteSpace(result?.ScrubbedText)
                ? userText
                : result.ScrubbedText;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            return userText;
        }
    }

    private sealed record PresidioTextRequest(
        [property: System.Text.Json.Serialization.JsonPropertyName("text")] string Text,
        [property: System.Text.Json.Serialization.JsonPropertyName("language")] string Language = "en");

    private sealed class PresidioTextResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("scrubbed_text")]
        public string ScrubbedText { get; set; } = string.Empty;
    }

    private static void UpdateDollyStatus(ChatMessageViewModel message, string status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return;
        }

        message.Paragraphs.Clear();
        message.Paragraphs.Add(status.Trim());
    }

    private static void AppendStreamingDraft(
        ChatMessageViewModel message,
        StringBuilder buffer,
        string delta)
    {
        if (string.IsNullOrWhiteSpace(delta))
        {
            return;
        }

        void Apply()
        {
            buffer.Append(delta);
            var preview = buffer.ToString().TrimStart();

            if (string.IsNullOrWhiteSpace(preview))
            {
                return;
            }

            message.Paragraphs.Clear();
            foreach (var paragraph in SplitStreamingPreview(preview))
            {
                message.Paragraphs.Add(paragraph);
            }
        }

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(Apply);
            return;
        }

        Apply();
    }

    private static IEnumerable<string> SplitStreamingPreview(string value)
    {
        var cleaned = value
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Trim();

        var paragraphs = Regex.Split(cleaned, @"\n\s*\n")
            .Select(block => Regex.Replace(block.Trim(), @"\s*\n\s*", " "))
            .Where(block => !string.IsNullOrWhiteSpace(block))
            .Take(8)
            .ToList();

        return paragraphs.Count == 0 ? [cleaned] : paragraphs;
    }

    private static string BuildPreflightStatusMessage(OmniboxPreflightResult? preflight)
    {
        if (!string.IsNullOrWhiteSpace(preflight?.DollyReply))
        {
            return preflight.DollyReply;
        }

        if (preflight?.IsAvailable == true)
        {
            return preflight.RecommendedMode.Equals("thinking", StringComparison.OrdinalIgnoreCase)
                ? "I have the local intake result. I am moving this through the chart workflow now."
                : "I checked this locally and it does not need a chart write.";
        }

        return "Local intake was unavailable, so I am keeping this inside the local safety path.";
    }

    private string BuildRecentConversationContext()
    {
        var modeLines = BuildHealthspanModeContextLines()
            .Select(line => $"System: {line}");
        var lines = Messages
            .Where(message => !message.Paragraphs.Any(paragraph => paragraph.Equals("Processing...", StringComparison.OrdinalIgnoreCase)))
            .TakeLast(12)
            .Select(message =>
            {
                var speaker = message.Author == MessageAuthor.User ? "User" : "Dolly";
                var text = string.Join(" ", message.Paragraphs).Trim();
                return $"{speaker}: {StripInternalChartIds(text)}";
            })
            .Where(line => !string.IsNullOrWhiteSpace(line));

        return string.Join(Environment.NewLine, modeLines.Concat(lines));
    }

    private static string StripInternalChartIds(string value)
    {
        return Regex.Replace(value, @"\bVITA-\d{4,6}\b", "[chart id]");
    }

    private static string BuildConversationalDollyReply(OmniboxPreflightResult? preflight, string userText)
    {
        if (!string.IsNullOrWhiteSpace(preflight?.DollyReply))
        {
            return preflight.DollyReply;
        }

        if (LooksLikeSmallTalkText(userText))
        {
            return "Good day. I am here, the vault is idle, and I am ready when you are.";
        }

        return preflight?.IsAvailable == true
            ? "I am here. That looks conversational, so I did not write anything to the vault."
            : "I am here. The local helper is unavailable, so I cannot ingest or privacy-review new source files right now. Existing chart questions can still use the agent connection.";
    }

    private static string BuildConversationalDollyReply(OmniboxPreflightResult? preflight)
    {
        if (!string.IsNullOrWhiteSpace(preflight?.DollyReply))
        {
            return preflight.DollyReply;
        }

        return preflight?.IsAvailable == true
            ? "I’m here. That looks conversational, so I did not write anything to the vault."
            : "I’m here. Local Dolly preflight was unavailable, so I kept this as conversation and did not write anything to the vault.";
    }

    private static string ChooseGemmaPatientName(OmniboxPreflightResult preflight)
    {
        if (preflight.PatientReferencePresent &&
            !string.IsNullOrWhiteSpace(preflight.PatientDisplayName) &&
            IsUsableConfidence(preflight.Confidence))
        {
            return preflight.PatientDisplayName.Trim();
        }

        if (preflight.PatientReferencePresent &&
            !string.IsNullOrWhiteSpace(preflight.PatientReference) &&
            IsUsableConfidence(preflight.Confidence))
        {
            return preflight.PatientReference.Trim();
        }

        return string.Empty;
    }

    private static bool IsUsableConfidence(string confidence)
    {
        return confidence.Equals("high", StringComparison.OrdinalIgnoreCase) ||
               confidence.Equals("medium", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> BuildPatientIntakeQuestionLines(
        PendingPatientIntake intake,
        IReadOnlyList<string> missing)
    {
        yield return "I can add this as a new patient file.";
        yield return missing.Count == 1
            ? $"I still need the {missing[0]}."
            : $"I still need: {string.Join(", ", missing)}.";

        if (intake.HasAnyValue)
        {
            yield return $"Captured so far: {intake.FormatCapturedFields()}.";
        }
    }

    private static IEnumerable<string> BuildPatientIdentityQuestionLines(PendingPatientIntake intake)
    {
        var candidates = intake.CandidateNames.Take(5).ToList();
        var joined = candidates.Count switch
        {
            0 => "the patient",
            1 => candidates[0],
            2 => $"{candidates[0]} or {candidates[1]}",
            _ => $"{string.Join(", ", candidates.Take(candidates.Count - 1))}, or {candidates[^1]}"
        };

        yield return $"I found more than one person in that note. Is this chart for {joined}?";
        yield return "Reply with the patient name, or say cancel.";
    }

    private static IEnumerable<string> BuildPatientIntakeReviewLines(
        PendingPatientIntake intake,
        IReadOnlyList<AttachmentViewModel> sentAttachments)
    {
        yield return "Here's what I found.";
        yield return $"Patient name: {FormatKnownOrMissing(intake.FullName)}";
        yield return $"DOB: {FormatKnownOrMissing(intake.DateOfBirth)}";
        yield return $"Address: {FormatKnownOrMissing(intake.Address)}";
        yield return $"Phone: {FormatKnownOrMissing(intake.PhoneNumber)}";

        if (!string.IsNullOrWhiteSpace(intake.RelationshipNotes))
        {
            yield return $"Relationship: {intake.RelationshipNotes}";
        }

        if (sentAttachments.Count > 0)
        {
            yield return $"Attached source: {string.Join(", ", sentAttachments.Select(attachment => attachment.DisplayName))}";
        }

        yield return "Only the chart manager can create a new patient file.";
        yield return "Reply Create chart to review the manager confirmation phrase, add corrections, or say cancel.";
    }

    private bool IsChartManagerCreateConfirmation(string value)
    {
        return value.Trim().Equals(BuildChartManagerCreatePhrase(), StringComparison.OrdinalIgnoreCase);
    }

    private string BuildChartManagerCreatePhrase()
    {
        var password = string.IsNullOrWhiteSpace(_settings.ChartManagerPassword)
            ? "Vault Manager"
            : _settings.ChartManagerPassword.Trim();

        return $"{password} CREATE CHART";
    }

    private string BuildChartManagerDeletePhrase(string patientDisplayName)
    {
        var password = string.IsNullOrWhiteSpace(_settings.ChartManagerPassword)
            ? "Vault Manager"
            : _settings.ChartManagerPassword.Trim();

        return $"{password} DELETE {patientDisplayName}";
    }

    private string BuildChartManagerPendingDeletePhrase()
    {
        var password = string.IsNullOrWhiteSpace(_settings.ChartManagerPassword)
            ? "Vault Manager"
            : _settings.ChartManagerPassword.Trim();

        return $"{password} DELETE";
    }

    private IEnumerable<string> BuildChartManagerCreateConfirmationLines(PendingPatientIntake intake)
    {
        var manager = string.IsNullOrWhiteSpace(_settings.ChartManagerName)
            ? "the chart manager"
            : _settings.ChartManagerName.Trim();

        yield return $"Only {manager} can create new patient files.";
        yield return $"Patient file ready: {intake.FullName}.";
        yield return $"To create this chart, type exactly: {BuildChartManagerCreatePhrase()}";
    }

    private string MovePatientToWastebasket(PatientIdentityRecord patient)
    {
        var chartRoot = Path.Combine(VaultRootPath, patient.ChartId);
        var wastebasketRoot = Path.Combine(VaultRootPath, "_System", "Wastebasket");
        var wastebasketPatientRoot = Path.Combine(
            wastebasketRoot,
            $"{DateTime.Now:yyyyMMdd-HHmmss}_{patient.ChartId}_{BuildSafeFolderName(patient.PatientDisplayName)}");

        Directory.CreateDirectory(wastebasketRoot);

        if (Directory.Exists(chartRoot))
        {
            Directory.Move(chartRoot, wastebasketPatientRoot);
        }
        else
        {
            Directory.CreateDirectory(wastebasketPatientRoot);
        }

        File.WriteAllText(
            Path.Combine(wastebasketPatientRoot, "_Wastebasket_Metadata.txt"),
            $"Patient: {patient.PatientDisplayName}{Environment.NewLine}" +
            $"DOB: {FormatKnownOrMissing(patient.DateOfBirth)}{Environment.NewLine}" +
            $"Chart ID: {patient.ChartId}{Environment.NewLine}" +
            $"Moved by vault owner: Vault Manager{Environment.NewLine}" +
            $"Moved at: {DateTime.Now:O}{Environment.NewLine}" +
            $"Purge eligible after: {DateTime.Now.AddDays(7):yyyy-MM-dd}{Environment.NewLine}");

        _patientRegistryService.RemoveIdentity(patient.ChartId, out _);

        if (ActiveChartId.Equals(patient.ChartId, StringComparison.OrdinalIgnoreCase))
        {
            ActiveChartId = "VITA-0001";
            ActivePatientDisplayName = _patientRegistryService.ResolveDisplayName(ActiveChartId);
        }

        return $"{patient.PatientDisplayName} was moved to the 7-day wastebasket. The registry entry was removed from the active vault roster.";
    }

    private static string FormatKnownOrMissing(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "missing" : value.Trim();
    }

    private static string BuildSafeFolderName(string value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? "Patient" : value.Trim();
        normalized = Regex.Replace(normalized, @"[^A-Za-z0-9_-]+", "_").Trim('_');
        return string.IsNullOrWhiteSpace(normalized) ? "Patient" : normalized;
    }

    private static string GetNonConflictingPath(string folder, string fileName)
    {
        var path = Path.Combine(folder, fileName);
        if (!File.Exists(path))
        {
            return path;
        }

        var stem = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        for (var index = 2; index < 1000; index++)
        {
            var candidate = Path.Combine(folder, $"{stem}_{index}{extension}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }

        return Path.Combine(folder, $"{stem}_{Guid.NewGuid():N}{extension}");
    }

    private static string EscapeMarkdownTableCell(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Replace("|", "\\|", StringComparison.Ordinal).ReplaceLineEndings(" ").Trim();
    }

    private ChartContext GetActiveChart()
    {
        var chartId = NormalizeChartId(ActiveChartId);
        var localDisplayName = string.IsNullOrWhiteSpace(ActivePatientDisplayName)
            ? chartId
            : ActivePatientDisplayName;

        _patientRegistryService.GetOrCreateIdentity(chartId, localDisplayName);
        return new ChartContext(chartId, localDisplayName);
    }

    private static bool IsImageAttachment(AttachmentViewModel attachment)
    {
        var extension = Path.GetExtension(attachment.Path);
        return extension.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".tif", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".tiff", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".bmp", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".gif", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".webp", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMeaningfulLocalDisplayName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim();
        return !normalized.Equals("PatientZero", StringComparison.OrdinalIgnoreCase) &&
               !normalized.Equals("Synthetic Test Patient", StringComparison.OrdinalIgnoreCase) &&
               !normalized.StartsWith("VITA-", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAffirmative(string value)
    {
        var normalized = NormalizeConfirmation(value);
        return normalized is "yes" or "y" or "yeah" or "yep" or "ok" or "okay" or "confirm" or "store it" or "save it" or "create chart" or "create the chart";
    }

    private static bool IsNegative(string value)
    {
        var normalized = NormalizeConfirmation(value);
        return normalized is "no" or "n" or "nope" or "cancel" or "do not store" or "dont store" or "don't store";
    }

    private static string NormalizeConfirmation(string value)
    {
        var normalized = Regex.Replace(value.Trim().ToLowerInvariant(), @"[^\w\s']", " ");
        return Regex.Replace(normalized, @"\s+", " ").Trim();
    }

    private static bool LooksLikeReplacementTypedCapture(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = NormalizeConfirmation(value);
        var hasReplacementIntent = normalized.Contains("forget that one", StringComparison.OrdinalIgnoreCase) ||
                                   normalized.Contains("cancel that", StringComparison.OrdinalIgnoreCase) ||
                                   normalized.Contains("replace", StringComparison.OrdinalIgnoreCase) ||
                                   normalized.Contains("instead", StringComparison.OrdinalIgnoreCase) ||
                                   normalized.Contains("add this", StringComparison.OrdinalIgnoreCase);
        var hasPatientLine = Regex.IsMatch(value, @"\bPatient\s*:", RegexOptions.IgnoreCase);
        var hasMedicalSignal = Regex.IsMatch(
            value,
            @"\b(A1c|blood pressure|medication|metformin|losartan|diagnos|visit|follow-up|follow up|plan)\b",
            RegexOptions.IgnoreCase);

        return hasReplacementIntent && hasPatientLine && hasMedicalSignal;
    }

    private static string ExtractPatientName(string userText)
    {
        if (string.IsNullOrWhiteSpace(userText))
        {
            return string.Empty;
        }

        var explicitPatientLine = Regex.Match(
            userText,
            @"(?im)^\s*Patient(?:\s+Name)?\s*:\s*(?<name>[^\r\n]+)");

        if (explicitPatientLine.Success)
        {
            return CleanPatientName(explicitPatientLine.Groups["name"].Value);
        }

        var patterns = new[]
        {
            @"\badd\s+(?<name>[A-Z][A-Za-z.'-]+(?:\s+[A-Z][A-Za-z.'-]+){1,4})\s+(?:to\s+(?:the\s+)?(?:chart|charts|database|file)|as\s+(?:a\s+)?new\s+p(?:atien)?t)\b",
            @"\b(?:new\s+p(?:atien)?t|new\s+patient\s+name|patient\s+name)\s*(?:is|:|,)?\s*(?<name>[A-Z][A-Za-z.'-]+(?:\s+[A-Z][A-Za-z.'-]+){1,4})\b",
            @"\b(?:for|on)\s+(?<name>[A-Z][A-Za-z.'-]+(?:\s+[A-Z][A-Za-z.'-]+){1,4})\s+(?:as\s+(?:a\s+)?new\s+p(?:atien)?t|new\s+p(?:atien)?t)\b",
            @"\b(?:patient|pt)\s*(?:name\s*)?(?:is|:)\s*(?<name>[A-Z][A-Za-z.'-]+(?:\s+[A-Z][A-Za-z.'-]+){0,4})",
            @"\bname\s*:\s*(?<name>[A-Z][A-Za-z.'-]+(?:\s+[A-Z][A-Za-z.'-]+){0,4})",
            @"\bfor\s+(?<name>[A-Z][A-Za-z.'-]+(?:\s+[A-Z][A-Za-z.'-]+){1,4})\b"
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(userText, pattern, RegexOptions.IgnoreCase);

            if (match.Success)
            {
                return CleanPatientName(match.Groups["name"].Value);
            }
        }

        var fallback = Regex.Match(
            userText,
            @"\b(?<name>[A-Z][A-Za-z.'-]+\s+[A-Z][A-Za-z.'-]+(?:\s+[A-Z][A-Za-z.'-]+){0,2})\b");

        return fallback.Success
            ? CleanPatientName(fallback.Groups["name"].Value)
            : string.Empty;
    }

    private static IReadOnlyList<string> ExtractPatientIdentityCandidates(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var candidates = new List<string>();
        var patterns = new[]
        {
            @"\b(?:Patient|Patient Name|Name|Guardian|Parent|Mother|Father|Sibling|Sister|Brother|Spouse|Wife|Husband|Relative|Emergency Contact)\s*:\s*(?<name>[A-Z][A-Za-z.'-]+(?:\s+[A-Z][A-Za-z.'-]+){0,3})",
            @"\b(?:for|regarding|about)\s+(?<name>[A-Z][A-Za-z.'-]+(?:\s+[A-Z][A-Za-z.'-]+){1,3})\b",
            @"\b(?<name>[A-Z][A-Za-z.'-]+\s+[A-Z][A-Za-z.'-]+(?:\s+[A-Z][A-Za-z.'-]+){0,2})\b"
        };

        foreach (var pattern in patterns)
        {
            foreach (Match match in Regex.Matches(text, pattern))
            {
                var name = CleanPatientName(match.Groups["name"].Value);
                if (string.IsNullOrWhiteSpace(name) ||
                    LooksLikeNonPersonName(name) ||
                    candidates.Any(existing => existing.Equals(name, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                candidates.Add(name);
            }
        }

        return candidates.Take(8).ToList();
    }

    private static bool LooksLikeNonPersonName(string value)
    {
        var normalized = Regex.Replace(value.Trim().ToLowerInvariant(), @"[^a-z0-9\s]+", " ");
        normalized = Regex.Replace(normalized, @"\s+", " ").Trim();

        if (LooksLikePlaceholderPatientName(value))
        {
            return true;
        }

        var blocked = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "date of birth",
            "chief complaint",
            "history of present",
            "review of systems",
            "physical exam",
            "assessment plan",
            "ct abdomen",
            "ct pelvis",
            "ca 125",
            "genetic counseling",
            "lynch syndrome",
            "emergency contact",
            "uploaded file",
            "dolly review",
            "attachment"
        };

        return blocked.Contains(normalized) ||
               Regex.IsMatch(normalized, @"\b(?:dob|phone|address|clinic|hospital|doctor|dr|md|rn|rd|ct|mri|xray|sp02|spo2|api|dolly|vita)\b", RegexOptions.IgnoreCase);
    }

    private static string CleanPatientName(string value)
    {
        var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ct",
            "mri",
            "xray",
            "x-ray",
            "brain",
            "head",
            "image",
            "scan",
            "document",
            "official",
            "report",
            "source",
            "type",
            "source type",
            "dob",
            "date",
            "address",
            "phone",
            "email"
        };

        var words = value
            .Trim()
            .Trim('.', ',', ';', ':')
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .TakeWhile(word => !stopWords.Contains(word.Trim('.', ',', ';', ':')))
            .ToArray();

        var cleaned = string.Join(' ', words).Trim();
        return LooksLikePlaceholderPatientName(cleaned) ? string.Empty : cleaned;
    }

    private static bool LooksLikePlaceholderPatientName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var normalized = Regex.Replace(value.Trim().ToLowerInvariant(), @"[^a-z0-9\s]+", " ");
        normalized = Regex.Replace(normalized, @"\s+", " ").Trim();

        return normalized is "patient" or "pt" or "this patient" or "this pt" or "the patient" or "the pt" or
            "new patient" or "new pt" or "her" or "him" or "them" or "this person" or "that patient" or "that pt" or
            "this chart" or "the chart";
    }

    private IReadOnlyList<IngestAuditEntry> CreateAuditEntries(
        IReadOnlyList<IngestResult> ingestResults,
        IReadOnlyList<ScrubResult> scrubResults,
        IReadOnlyList<SecondPassScrubResult> secondPassScrubResults,
        LocalModelReviewResult? localModelReview,
        OmniboxPreflightResult? preflight,
        IReadOnlyList<ImageDocumentReviewResult> imageDocumentReviewResults,
        GeminiBackendPipelineResult? backendPipelineResult)
    {
        var scrubByPath = scrubResults.ToDictionary(
            result => result.SourcePath,
            StringComparer.OrdinalIgnoreCase);
        var finalByPath = secondPassScrubResults.ToDictionary(
            result => result.SourcePath,
            StringComparer.OrdinalIgnoreCase);
        var imageReviewByPath = imageDocumentReviewResults.ToDictionary(
            result => result.SourcePath,
            StringComparer.OrdinalIgnoreCase);
        var apiSubmittedSourcePaths = backendPipelineResult?.ApiSubmittedSourcePaths
            .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];
        var wikiWrittenSourcePaths = backendPipelineResult?.WikiWrittenSourcePaths
            .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];

        return ingestResults.Select(result =>
        {
            scrubByPath.TryGetValue(result.RawVaultPath, out var scrubResult);
            finalByPath.TryGetValue(result.RawVaultPath, out var finalScrubResult);
            imageReviewByPath.TryGetValue(result.RawVaultPath, out var imageReviewResult);

            return new IngestAuditEntry
            {
                Timestamp = DateTime.Now,
                ChartId = ActiveChartId,
                SourcePath = result.SourcePath,
                RawVaultPath = result.RawVaultPath,
                RawDisplayName = result.DisplayName,
                SourceSystem = SourceSystem,
                SourceFacility = SourceFacility,
                SourceType = SelectedSourceType,
                ScrubStatus = scrubResult?.Status ?? "NOT_SCRUBBED",
                ScrubFindingCount = scrubResult?.Findings.Count ?? 0,
                LocalModelReviewStatus = localModelReview?.Status ?? "LOCAL_MODEL_NOT_RUN",
                LocalModelRoute = localModelReview?.Route ?? string.Empty,
                LocalModelEndpointUsed = localModelReview?.EndpointUsed ?? string.Empty,
                LocalModelNameUsed = localModelReview?.ModelNameUsed ?? string.Empty,
                LocalModelFailoverUsed = localModelReview?.FailoverUsed ?? false,
                LocalModelRouteStatus = localModelReview?.RouteStatus ?? string.Empty,
                OmniboxPreflightRoute = preflight?.Route ?? string.Empty,
                OmniboxPreflightEndpointUsed = preflight?.EndpointUsed ?? string.Empty,
                OmniboxPreflightModelNameUsed = preflight?.ModelNameUsed ?? string.Empty,
                OmniboxPreflightFailoverUsed = preflight?.FailoverUsed ?? false,
                OmniboxPreflightRouteStatus = preflight?.RouteStatus ?? string.Empty,
                PrivacyGateStatus = finalScrubResult is not null
                    ? "FINAL_SCRUBBED_PAYLOAD_SAVED"
                    : localModelReview?.Status ?? "LOCAL_MODEL_NOT_RUN",
                FinalScrubbedPath = finalScrubResult?.FinalScrubbedPath ?? string.Empty,
                SecondPassReplacementCount = finalScrubResult?.ReplacementCount ?? 0,
                ImageDocumentType = imageReviewResult?.ImageDocumentType ?? string.Empty,
                ImageReviewStatus = imageReviewResult?.Status ?? string.Empty,
                OfficialReadRequired = imageReviewResult?.OfficialReadRequired ?? false,
                OfficialReadPresent = imageReviewResult?.OfficialReadPresent ?? false,
                ImageReviewMetadataPath = imageReviewResult?.SavedMetadataPath ?? string.Empty,
                ApiSent = finalScrubResult is not null &&
                    apiSubmittedSourcePaths.Contains(finalScrubResult.SourcePath),
                WikiWritten = finalScrubResult is not null &&
                    wikiWrittenSourcePaths.Contains(finalScrubResult.SourcePath)
            };
        }).ToList();
    }

    private static IEnumerable<string> BuildVaultMessageLines(IReadOnlyList<IngestResult> ingestResults)
    {
        var createdCount = ingestResults
            .SelectMany(result => result.ScaffoldCreatedPaths)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        if (createdCount > 0)
        {
            yield return "I made sure the chart folders are ready.";
        }

        foreach (var result in ingestResults)
        {
            yield return $"I saved the original source unchanged for {result.DisplayName}.";
        }
    }

    private static IEnumerable<string> BuildIngestOutcomeMessageLines(
        IReadOnlyList<IngestResult> ingestResults,
        LocalModelReviewResult? localModelReview,
        IReadOnlyList<SecondPassScrubResult> secondPassScrubResults,
        GeminiBackendPipelineResult? backendPipelineResult,
        string vaultRoot,
        ChartContext chartContext)
    {
        if (ingestResults.Count == 0)
        {
            yield return "No file was attached, so this is being treated as a direct conversational task.";
            yield break;
        }

        yield return ingestResults.Count == 1
            ? $"I received one source file for {ingestResults[0].DisplayName}."
            : $"I received {ingestResults.Count} source files for {ingestResults[0].DisplayName}.";

        if (localModelReview is not { IsAvailable: true })
        {
            yield return localModelReview is null
                ? "Processing stopped before the local privacy gate could run."
                : $"Processing stopped at the local privacy gate: {localModelReview.Status}.";
            yield return localModelReview?.Recommendation ?? "No safe chart text was created, so chart writing did not run.";
            yield break;
        }

        yield return "The local privacy gate finished.";
        yield return localModelReview.FailoverUsed
            ? "I had to switch local workers once, but the safe review continued."
            : "The local Presidio service handled the review.";
        yield return secondPassScrubResults.Count > 0
            ? $"I saved a scrubbed version for {secondPassScrubResults.Count} file(s)."
            : "No scrubbed version was created, so I stopped before writing the wiki.";

        if (backendPipelineResult is null)
        {
            yield return "Backend writer did not run.";
            yield break;
        }

        yield return backendPipelineResult.ApiSubmittedSourcePaths.Count > 0
            ? "The scrubbed content reached the chart writer."
            : "The chart writer did not receive any scrubbed content.";
        yield return backendPipelineResult.WikiWrittenSourcePaths.Count > 0
            ? $"I accepted {backendPipelineResult.WikiWrittenSourcePaths.Count} validated wiki update(s)."
            : "No wiki update passed validation.";

        if (backendPipelineResult.WikiWrittenSourcePaths.Count == 0)
        {
            var lastStep = backendPipelineResult.Steps.LastOrDefault();
            yield return lastStep is null
                ? "No wiki update was written."
                : $"No wiki update was written. Last backend step: {lastStep.Name} {lastStep.Status}.";
            yield break;
        }

        if (backendPipelineResult.SummaryItems.Count == 0)
        {
            yield return "The uploaded document was processed into the sterile wiki.";
            yield break;
        }

        yield return backendPipelineResult.SummaryItems.Count == 1
            ? "I added one visit note."
            : $"I added {backendPipelineResult.SummaryItems.Count} visit notes.";

        foreach (var summary in backendPipelineResult.SummaryItems.Take(2))
        {
            foreach (var line in BuildIngestSummaryCardLines(summary))
            {
                yield return line;
            }
        }

        var possibleResolvedGaps = FindPossibleResolvedCareGaps(
            backendPipelineResult,
            vaultRoot,
            chartContext);

        if (possibleResolvedGaps.Count > 0)
        {
            yield return "This note may help resolve an open item:";
            foreach (var gap in possibleResolvedGaps.Take(3))
            {
                yield return $"- {gap.GapId}: {gap.Title}";
            }

            yield return "I did not change those. Ask me to review one if you want to close it.";
        }
        else if (backendPipelineResult.SummaryItems.Any(item => item.PendingItems.Count > 0))
        {
            yield return "I noticed pending items in the note. I did not change existing care gaps unless you ask.";
        }
    }

    private static IEnumerable<string> BuildIngestSummaryCardLines(IngestSummaryItem summary)
    {
        var visitParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(summary.DocumentType))
        {
            visitParts.Add(summary.DocumentType.Trim());
        }

        if (!string.IsNullOrWhiteSpace(summary.DateOfService))
        {
            visitParts.Add(summary.DateOfService.Trim());
        }

        yield return visitParts.Count > 0
            ? $"Visit: {string.Join(", ", visitParts)}."
            : "Visit: added to the sterile chart.";

        var mainItems = summary.MainClinicalFacts
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Take(4)
            .ToList();

        if (mainItems.Count > 0)
        {
            yield return $"Main items: {string.Join("; ", mainItems)}.";
        }
        else if (!string.IsNullOrWhiteSpace(summary.ClinicalGestalt))
        {
            yield return $"Main story: {summary.ClinicalGestalt.Trim()}.";
        }

        var pendingItems = summary.PendingItems
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Take(4)
            .ToList();

        if (pendingItems.Count > 0)
        {
            yield return $"Pending items: {string.Join("; ", pendingItems)}.";
        }

        var topicPages = summary.TopicPages
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Take(4)
            .ToList();

        if (topicPages.Count > 0)
        {
            yield return $"Filed under: {string.Join(", ", topicPages)}.";
        }
        else if (!string.IsNullOrWhiteSpace(summary.EncounterNodePath))
        {
            yield return $"Filed at: {Path.GetFileName(summary.EncounterNodePath)}.";
        }
    }

    private static IReadOnlyList<CareGapRow> FindPossibleResolvedCareGaps(
        GeminiBackendPipelineResult backendPipelineResult,
        string vaultRoot,
        ChartContext chartContext)
    {
        if (backendPipelineResult.SummaryItems.Count == 0)
        {
            return [];
        }

        var careGapsPath = Path.Combine(vaultRoot, chartContext.ChartFolderName, "wiki", "Care_Gaps.md");
        if (!File.Exists(careGapsPath))
        {
            return [];
        }

        var summaryText = NormalizeMatchText(string.Join(
            " ",
            backendPipelineResult.SummaryItems.SelectMany(item =>
                item.MainClinicalFacts
                    .Concat(item.PendingItems)
                    .Concat(item.TopicPages)
                    .Append(item.ClinicalGestalt)
                    .Append(item.DocumentType))));

        if (string.IsNullOrWhiteSpace(summaryText))
        {
            return [];
        }

        return File.ReadLines(careGapsPath)
            .Select(ParseCareGapRow)
            .Where(row => row is not null)
            .Cast<CareGapRow>()
            .Where(row => row.Status.Equals("open", StringComparison.OrdinalIgnoreCase))
            .Select(row => new
            {
                Row = row,
                Score = ScoreCareGapSummaryMatch(summaryText, row)
            })
            .Where(candidate => candidate.Score >= 12)
            .OrderByDescending(candidate => candidate.Score)
            .Select(candidate => candidate.Row)
            .Take(3)
            .ToList();
    }

    private static int ScoreCareGapSummaryMatch(string summaryText, CareGapRow row)
    {
        var title = NormalizeMatchText(row.Title);
        var score = 0;

        foreach (var token in title.Split(' ', StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (token.Length < 4 ||
                token is "pending" or "needed" or "result" or "results" or "follow" or "with" or "from")
            {
                continue;
            }

            if (summaryText.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                score += token.Length;
            }
        }

        if (title.Contains("echocardiogram", StringComparison.OrdinalIgnoreCase) &&
            (summaryText.Contains("echocardiogram", StringComparison.OrdinalIgnoreCase) ||
             summaryText.Contains("echo", StringComparison.OrdinalIgnoreCase)))
        {
            score += 20;
        }

        if ((title.Contains("cpet", StringComparison.OrdinalIgnoreCase) ||
             title.Contains("cardiopulmonary", StringComparison.OrdinalIgnoreCase)) &&
            (summaryText.Contains("cpet", StringComparison.OrdinalIgnoreCase) ||
             summaryText.Contains("cardiopulmonary", StringComparison.OrdinalIgnoreCase)))
        {
            score += 20;
        }

        if (title.Contains("ct", StringComparison.OrdinalIgnoreCase) &&
            summaryText.Contains("ct", StringComparison.OrdinalIgnoreCase))
        {
            score += 8;
        }

        return score;
    }

    private static IEnumerable<string> BuildEncounterDraftMessageLines(
        EncounterDraft draft,
        DraftValidationResult validation)
    {
        yield return $"Mock encounter draft ready: {draft.DocumentType}, {draft.RiskTier}.";
        yield return $"Validation: {(validation.IsValid ? "passed" : "needs review")}.";
        yield return "No API call was made and no wiki encounter was written yet.";

        foreach (var message in validation.Messages.Take(3))
        {
            yield return message;
        }
    }

    private static IEnumerable<string> BuildLocalModelReviewMessageLines(LocalModelReviewResult review)
    {
        yield return review.IsAvailable
            ? "The local privacy gate completed."
            : $"The local privacy gate was unavailable: {review.Status}.";
        yield return review.FailoverUsed
            ? "I switched local workers during the review and kept the process bounded."
            : "The local Presidio service handled this review.";
        if (!string.IsNullOrWhiteSpace(review.RouteStatus))
        {
            yield return $"Worker status: {review.RouteStatus}.";
        }
        yield return $"Remaining PHI risk: {review.RemainingPhiRisk}.";
        yield return review.Recommendation;

        foreach (var finding in review.Findings.Take(3))
        {
            yield return $"Finding: {finding}";
        }
    }

    private static string FormatRouteSummary(string route, string endpoint, string modelName, bool failoverUsed)
    {
        var routeLabel = string.IsNullOrWhiteSpace(route) ? "unknown_route" : route;
        var endpointLabel = string.IsNullOrWhiteSpace(endpoint) ? "unknown endpoint" : endpoint;
        var modelLabel = string.IsNullOrWhiteSpace(modelName) ? "unknown model" : modelName;
        var failoverLabel = failoverUsed ? ", failover used" : ", no failover";
        return $"{routeLabel} via {endpointLabel} using {modelLabel}{failoverLabel}";
    }

    private static string HumanizeStepName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Chart step";
        }

        return value
            .Replace('_', ' ')
            .Replace("Gemini", "Chart writer", StringComparison.OrdinalIgnoreCase)
            .Replace("Backend", "Chart writer", StringComparison.OrdinalIgnoreCase)
            .Trim();
    }

    private static string HumanizeStatus(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "completed" or "success" or "ok" => "finished",
            "running" => "in progress",
            "failed" or "error" => "stopped",
            "skipped" => "skipped",
            _ => string.IsNullOrWhiteSpace(value) ? "updated" : value.Trim()
        };
    }

    private static string HumanizeException(Exception exception)
    {
        return exception switch
        {
            TaskCanceledException => "a local or backend worker timed out",
            HttpRequestException => "a worker connection failed",
            JsonException => "a model returned unreadable structured data",
            IOException => "a chart file could not be read or written",
            UnauthorizedAccessException => "Windows blocked access to a needed file",
            InvalidOperationException => "the run reached an invalid app state",
            _ => string.IsNullOrWhiteSpace(exception.Message)
                ? exception.GetType().Name
                : exception.Message
        };
    }

    private static IEnumerable<string> BuildSecondPassScrubMessageLines(IReadOnlyList<SecondPassScrubResult> results)
    {
        yield return results.Count == 1
            ? "I saved one scrubbed version for chart writing."
            : $"I saved {results.Count} scrubbed versions for chart writing.";
        yield return "The original source stayed unchanged.";

        foreach (var result in results.Take(3))
        {
            yield return $"{result.DisplayName}: {result.ReplacementCount} privacy replacement(s).";
        }
    }

    private static IEnumerable<string> BuildEncounterNodeWriterMessageLines(IReadOnlyList<EncounterNodeWriteResult> results)
    {
        yield return results.Count == 1
            ? "I wrote one sterile encounter note into the wiki."
            : $"I wrote {results.Count} sterile encounter notes into the wiki.";
        var topicCount = results.Sum(result => result.TopicPagePaths.Count);
        yield return topicCount > 0
            ? $"I updated {topicCount} related topic page(s)."
            : "No topic pages were proposed by the extraction result.";
        yield return "Dolly can now answer from the sterile wiki for this material.";

        foreach (var result in results.Take(3))
        {
            yield return Path.GetFileName(result.EncounterNodePath);
        }
    }

    private static IEnumerable<string> BuildGeminiBackendPipelineMessageLines(GeminiBackendPipelineResult result)
    {
        yield return result.ApiSubmittedSourcePaths.Count > 0
            ? "The scrubbed content reached the chart writer."
            : "The chart writer did not receive any scrubbed content.";
        yield return result.WikiWrittenSourcePaths.Count > 0
            ? $"I accepted {result.WikiWrittenSourcePaths.Count} validated wiki update(s)."
            : "No wiki update passed validation.";

        foreach (var message in result.Messages)
        {
            yield return message;
        }

        foreach (var step in result.Steps.Take(6))
        {
            yield return $"{HumanizeStepName(step.Name)}: {HumanizeStatus(step.Status)}. {step.Detail}";
        }

        if (result.WriteResults.Count > 0)
        {
            foreach (var line in BuildEncounterNodeWriterMessageLines(result.WriteResults))
            {
                yield return line;
            }

            yield return "Tip: say \"run Dream Runner\" when you want a report-only audit of the active chart.";
        }
    }

    private static IEnumerable<string> BuildWikiPatchMessageLines(
        GeminiWikiPatchResult patchPlan,
        WikiPatchApplyResult applyResult)
    {
        yield return string.IsNullOrWhiteSpace(patchPlan.PatchSummary)
            ? "I updated the living wiki with the user-reported information."
            : patchPlan.PatchSummary;

        yield return "I marked the update as user-reported and unverified, with the original text saved as a manual-entry source.";

        foreach (var message in applyResult.Messages.Take(3))
        {
            yield return message;
        }

        foreach (var file in applyResult.UpdatedFiles.Take(5))
        {
            yield return $"Updated: {Path.GetFileName(file)}";
        }
    }

    private static IEnumerable<string> BuildImageReviewMessageLines(IReadOnlyList<ImageDocumentReviewResult> results)
    {
        foreach (var result in results.Take(3))
        {
            yield return $"{result.DisplayName}: classified as {result.ImageDocumentType}.";

            if (result.OfficialReadRequired && !result.OfficialReadPresent)
            {
                yield return result.RequiredFollowup;
            }
            else
            {
                yield return "Official read gate satisfied or not applicable.";
            }
        }
    }

    private static IEnumerable<string> BuildDreamRunnerAuditMessageLines(DreamRunnerAuditResult result)
    {
        yield return $"Dream Runner audit completed: {result.Status}.";
        yield return $"Checked {result.FilesChecked} wiki file(s). Missing required files: {result.MissingRequiredFiles}. Broken links: {result.BrokenLinks}.";
        yield return $"Open care gaps: {result.OpenCareGaps}. Active conflicts: {result.ActiveConflicts}. Unverified pages: {result.UnverifiedPages}.";
        yield return $"Report written: {Path.GetFileName(result.LogPath)}.";

        foreach (var finding in result.Findings.Take(5))
        {
            yield return $"{finding.Severity}: {Path.GetFileName(finding.FilePath)} - {finding.Message}";
        }
    }

    private static IEnumerable<string> BuildSpecialtyWeaverMessageLines(SpecialtyWeaverResult result)
    {
        yield return $"Specialty Weaver completed: {result.Status}.";
        yield return $"Ensured {result.SpecialtyFilesEnsured} specialty file(s), scanned {result.EncounterNodesScanned} encounter node(s), and wrote {result.RowsWritten} new row(s).";
        yield return $"Duplicate rows skipped: {result.DuplicateRowsSkipped}. Nodes skipped: {result.NodesSkipped}.";

        if (result.UpdatedSpecialties.Count > 0)
        {
            yield return $"Updated specialties: {string.Join(", ", result.UpdatedSpecialties.OrderBy(value => value))}.";
        }
        else
        {
            yield return "No specialty rows needed updating; the active chart may already be organized.";
        }

        yield return $"Report written: {Path.GetFileName(result.LogPath)}.";
    }

    private static IEnumerable<string> BuildPatternLinkerMessageLines(PatternLinkerResult result)
    {
        yield return $"Symptom Pattern Scan completed: {result.Status}.";
        yield return $"Scanned {result.ChartsScanned} chart(s) and {result.SymptomRowsScanned} symptom journal row(s).";
        yield return $"New patterns written: {result.PatternsWritten}. Duplicate patterns skipped: {result.DuplicatePatternsSkipped}.";
        yield return $"Care gaps created from higher-signal patterns: {result.CareGapsWritten}.";
        yield return "Prototype safety: this is report-only longitudinal pattern detection, not diagnosis, triage, or treatment advice.";

        foreach (var message in result.Messages.Take(5))
        {
            yield return message;
        }
    }

    private static IEnumerable<string> BuildDrugInteractionMessageLines(DrugInteractionScanResult result)
    {
        yield return $"Drug Interaction Scanner completed: {result.Status}.";
        yield return $"Reviewed {result.ActiveMedicationsReviewed} medication row(s) from the sterile index.";
        yield return $"Prototype findings written: {result.FindingsWritten}.";
        yield return "Scope note: v0 uses limited known rules only. It is not a complete drug database or pharmacist review.";

        foreach (var finding in result.Findings.Take(5))
        {
            yield return finding;
        }
    }

    private static IEnumerable<string> BuildPreVisitBriefMessageLines(PreVisitBriefResult result)
    {
        yield return result.Status.Contains("ready", StringComparison.OrdinalIgnoreCase) || result.Status.Contains("completed", StringComparison.OrdinalIgnoreCase)
            ? "Your pre-visit brief is ready."
            : $"The pre-visit brief stopped with status: {result.Status}.";
        yield return $"I pulled together {result.SectionsIncluded} safe chart section(s) for visit prep.";
        yield return "High-risk details still need source or clinician confirmation before anyone relies on them.";
    }

    private static IEnumerable<string> BuildSessionOpenMessageLines(
        SessionOpenAwarenessResult awareness,
        DollyWorkingSummaryResult? workingSummary,
        DollyTaskBoardResult? taskBoard)
    {
        foreach (var line in awareness.Lines)
        {
            yield return line;
        }

        if (workingSummary?.WasWritten == true)
        {
            yield return $"Dolly working summary is ready with {workingSummary.SectionsIncluded} section(s).";
        }

        if (taskBoard is not null)
        {
            yield return taskBoard.ActiveTaskCount == 0
                ? "Dolly task board is clear for this chart."
                : $"Dolly task board has {taskBoard.ActiveTaskCount} active task(s).";
        }
    }

    private static IEnumerable<string> BuildDollyWorkingSummaryMessageLines(DollyWorkingSummaryResult result)
    {
        yield return "I refreshed Dolly's local chart memory for this patient.";
        yield return $"It now has {result.SectionsIncluded} safe section(s) available for quick conversation while heavier work runs.";

        foreach (var hook in result.ConversationHooks.Take(5))
        {
            yield return hook;
        }
    }

    private static IEnumerable<string> BuildDollyTaskBoardMessageLines(DollyTaskBoardResult result)
    {
        if (result.ActiveTaskCount == 0)
        {
            yield return result.CompletedTaskCount == 0
                ? "I do not have any chart tasks running right now."
                : $"I do not have anything running right now. I have finished {result.CompletedTaskCount} tracked task(s) for this chart.";
        }
        else
        {
            yield return result.ActiveTaskCount == 1
                ? "I have one chart task running right now."
                : $"I have {result.ActiveTaskCount} chart tasks running right now.";
        }

        var friendlyLines = result.Lines
            .Select(SummarizeTaskBoardLineForUser)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToList();

        foreach (var line in friendlyLines)
        {
            yield return line;
        }
    }

    private static string SummarizeTaskBoardLineForUser(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return string.Empty;
        }

        if (line.Contains("No active Dolly tasks", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("Nothing is running", StringComparison.OrdinalIgnoreCase))
        {
            return "The task board is clear for the active chart.";
        }

        var cleaned = Regex.Replace(line, @"\bDTB-\d{8}-\d{6}:?\s*", string.Empty, RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"\([^)]*P\d+[^)]*\)", string.Empty, RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @";\s*elapsed\s+[^.]+\.?", ".", RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"\bP\d+\b", string.Empty, RegexOptions.IgnoreCase);
        cleaned = cleaned.Replace("completed -", "Recently finished:", StringComparison.OrdinalIgnoreCase);
        cleaned = cleaned.Replace("previous ", "A previous ", StringComparison.OrdinalIgnoreCase);
        cleaned = cleaned.Replace(" appears stale/interrupted at ", " may need review; it last stopped at ", StringComparison.OrdinalIgnoreCase);
        cleaned = cleaned.Replace("Dolly local context and family context written", "Dolly refreshed the local chart and family context", StringComparison.OrdinalIgnoreCase);
        cleaned = cleaned.Replace("Backend chart answer", "Chart answer", StringComparison.OrdinalIgnoreCase);
        cleaned = cleaned.Replace("backend chart answer", "chart answer", StringComparison.OrdinalIgnoreCase);
        cleaned = cleaned.Replace("Ingested", "I finished ingesting", StringComparison.OrdinalIgnoreCase);
        cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();

        if (string.IsNullOrWhiteSpace(cleaned))
        {
            return string.Empty;
        }

        return cleaned.EndsWith(".", StringComparison.Ordinal) ? cleaned : $"{cleaned}.";
    }

    private async Task<IEnumerable<string>> BuildConcurrentChatReplyAsync(string userText)
    {
        var chartContext = GetActiveChart();
        var localSessionContext = BuildLocalSessionContextForGemma(chartContext);

        if (string.IsNullOrWhiteSpace(localSessionContext))
        {
            return [
                "I am still working on the active task, and I do not have a local working-summary packet loaded yet.",
                "I can give task progress now, but I need the summary refresh or the backend chart context before I can answer chart questions from stored data."
            ];
        }

        var gemmaAnswer = await AskGemmaForConcurrentAnswerAsync(
            chartContext,
            userText,
            localSessionContext);
        var fallbackAnswer = BuildGemmaUnavailableFallback(chartContext, localSessionContext);

        return [
            string.IsNullOrWhiteSpace(gemmaAnswer) ? fallbackAnswer : gemmaAnswer,
            "I am answering from Dolly's local sterile context while the background task continues; new uploaded data may change this after ingest finishes."
        ];
    }

    private async Task<string> AskGemmaForConcurrentAnswerAsync(
        ChartContext chartContext,
        string userText,
        string localSessionContext)
    {
        ApplyLocalModelRoute(DesktopGemmaEndpoint, PreferredGemmaModelName);
        var endpoint = LocalModelEndpoint;
        var modelName = LocalModelName;

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(35));
        SetLocalLlmState(endpoint, true, "Local helper answering chat");
        string answer;

        try
        {
            answer = await _dollyVaultChatService.AnswerFromLocalSessionContextAsync(
                endpoint,
                modelName,
                chartContext.LocalDisplayName,
                userText,
                BuildRecentConversationContext(),
                localSessionContext,
                timeout.Token);
        }
        finally
        {
            SetLocalLlmState(endpoint, false, "Local helper ready");
        }

        return IsSummaryChatFailure(answer) || LooksLikeInternalPacketAnswer(answer) || answer.StartsWith("LOCAL_GEMMA_SESSION_CHAT_", StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : answer;
    }

    private async Task<GemmaActionPacketResult> AskGemmaForActionPacketAsync(
        string userText,
        ChartContext chartContext,
        string localSessionContext,
        ChatMessageViewModel? thinkingMessage = null)
    {
        var knownPatients = _patientRegistryService.GetAllIdentities();
        var endpoint = LocalModelEndpoint;
        var modelName = LocalModelName;
        const string routeUsed = "host_local_action_packet";
        var scrubbedUserText = await ScrubOmniboxTextWithPresidioForApiAsync(userText);
        using var presence = new CancellationTokenSource();
        var presenceTask = thinkingMessage is null
            ? Task.CompletedTask
            : RunDollyApiPresenceTimerAsync(thinkingMessage, presence.Token);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(40));
            SetLocalLlmState(endpoint, true, "Local intent mapping");
            GemmaActionPacketResult result;

            try
            {
                result = await _gemmaActionPacketService.BuildPacketAsync(
                    endpoint,
                    modelName,
                    scrubbedUserText,
                    BuildRecentConversationContext(),
                    localSessionContext,
                    chartContext.LocalDisplayName,
                    knownPatients,
                    timeout.Token);
            }
            finally
            {
                presence.Cancel();
                stopwatch.Stop();
                SetLocalLlmState(endpoint, false, "Local helper idle");
            }

            if (thinkingMessage is not null)
            {
                UpdateDollyStatus(thinkingMessage, $"I have the safe read back. That took {FormatElapsed(stopwatch.Elapsed)}.");
            }

            AppendGemmaActionPacketAudit(scrubbedUserText, chartContext, routeUsed, modelName, result);
            return result;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            presence.Cancel();
            stopwatch.Stop();
            SetLocalLlmState(endpoint, false, "Local helper unavailable");
            var result = new GemmaActionPacketResult
            {
                WasValid = false,
                Status = exception is TaskCanceledException ? "DOLLY_API_ACTION_PACKET_TIMEOUT" : "DOLLY_API_ACTION_PACKET_UNAVAILABLE",
                Errors = ["Dolly API did not return an action packet."]
            };
            AppendGemmaActionPacketAudit(scrubbedUserText, chartContext, routeUsed, modelName, result);
            return result;
        }
        finally
        {
            try
            {
                await presenceTask;
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    private async Task RunDollyApiPresenceTimerAsync(ChatMessageViewModel message, CancellationToken cancellationToken)
    {
        var updates = new (TimeSpan Delay, string Text)[]
        {
            (TimeSpan.FromSeconds(2), "I scrubbed the wording locally and I am reading the safe version now."),
            (TimeSpan.FromSeconds(6), "Still with it. I am matching your request to the right chart action."),
            (TimeSpan.FromSeconds(12), "This is taking a little longer than usual, but I am still working from the safe context.")
        };

        foreach (var update in updates)
        {
            await Task.Delay(update.Delay, cancellationToken);
            UpdateDollyStatus(message, update.Text);
        }
    }

    private static string FormatElapsed(TimeSpan elapsed)
    {
        return elapsed.TotalSeconds < 1
            ? "under a second"
            : $"{Math.Max(1, (int)Math.Round(elapsed.TotalSeconds))} seconds";
    }

    private void AppendGemmaActionPacketAudit(
        string userText,
        ChartContext chartContext,
        string routeUsed,
        string modelName,
        GemmaActionPacketResult result)
    {
        try
        {
            var knownPatients = _patientRegistryService.GetAllIdentities();
            var scrubbedPreview = DeidentifyForApi(userText, knownPatients);
            _dollyAgentStateService.AppendPacketAudit(
                VaultRootPath,
                chartContext,
                routeUsed,
                modelName,
                scrubbedPreview,
                result);
            TrackActionPacketVisibility(
                chartContext,
                result.WasValid ? "validated" : "blocked",
                BuildPacketAuditPreview(result),
                result.Packet);
        }
        catch (IOException)
        {
            // Packet audit is useful for prototype review, but it should never block a user turn.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private string BuildPacketAuditPreview(GemmaActionPacketResult result)
    {
        if (result.Packet is null)
        {
            var error = result.Errors.FirstOrDefault();
            return string.IsNullOrWhiteSpace(error)
                ? $"{AgentDisplayName} did not return a usable action."
                : $"{AgentDisplayName} did not return a usable action: {error}";
        }

        var confirmation = result.Packet.RequiresConfirmation
            ? "confirmation needed"
            : "no confirmation needed";
        return $"{result.Packet.Intent} ({result.Packet.Confidence:0.00}) - {confirmation}";
    }

    private void TrackActionPacketVisibility(
        ChartContext chartContext,
        string stage,
        string summary,
        GemmaActionPacket? packet = null)
    {
        var line = $"{DateTime.Now:h:mm:ss tt} - {NormalizeAuditStage(stage)}: {summary}";
        _recentActionPacketAuditLines.Enqueue(line);

        while (_recentActionPacketAuditLines.Count > 8)
        {
            _recentActionPacketAuditLines.Dequeue();
        }

        try
        {
            _chronosLedgerService.RecordEvent(
                VaultRootPath,
                chartContext,
                $"action_packet_{NormalizeAuditStage(stage)}",
                summary,
                AgentDisplayName,
                "action_packet_audit");

            AppendActionPacketDispatchAudit(chartContext, stage, summary, packet);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private void AppendActionPacketDispatchAudit(
        ChartContext chartContext,
        string stage,
        string summary,
        GemmaActionPacket? packet)
    {
        var folder = Path.Combine(VaultRootPath, "_System", "Dolly");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "Packet_Audit.md");
        var packetIntent = packet?.Intent ?? "none";
        var tool = packet?.Tool ?? string.Empty;
        var owner = packet?.ResponseOwner ?? string.Empty;

        File.AppendAllText(path, $"""

        ### {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz} - {NormalizeAuditStage(stage)}
        - Chart: {chartContext.ChartId} / {ExtractFirstNameForDisplay(chartContext.LocalDisplayName)}
        - Intent: {SanitizeAuditText(packetIntent)}
        - Owner: {SanitizeAuditText(owner)}
        - Tool: {SanitizeAuditText(tool)}
        - Summary: {SanitizeAuditText(summary)}

        """);
    }

    private static string NormalizeAuditStage(string stage)
    {
        return string.IsNullOrWhiteSpace(stage)
            ? "noted"
            : Regex.Replace(stage.Trim().ToLowerInvariant(), @"[^a-z0-9_]+", "_");
    }

    private static string SanitizeAuditText(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.ReplaceLineEndings(" ").Replace("|", "/", StringComparison.Ordinal).Trim();
    }

    private async Task DispatchGemmaActionPacketAsync(
        GemmaActionPacket packet,
        string userText,
        ChatMessageViewModel thinkingMessage,
        ChartContext fallbackChartContext,
        string localSessionContext,
        int packetAttempts,
        bool releaseThinkingWhenDone = true)
    {
        var chartContext = ResolvePacketChartContext(packet) ?? fallbackChartContext;

        if (packet.Confidence < 0.6)
        {
            FinishGemmaActionMessage(
                thinkingMessage,
                string.IsNullOrWhiteSpace(packet.UserFacingAnswer)
                    ? ["I am not confident enough to map that to a safe VitaMR action. Which patient or task did you mean?"]
                    : [packet.UserFacingAnswer],
                "gemma_action_low_confidence",
                chartContext,
                releaseThinkingWhenDone);
            return;
        }

        if (packet.Intent.Equals("set_active_patient", StringComparison.OrdinalIgnoreCase))
        {
            HandleNaturalChartSwitch(packet, userText, thinkingMessage, fallbackChartContext, releaseThinkingWhenDone);
            return;
        }

        if (packet.Intent.Equals("delete_patient_chart", StringComparison.OrdinalIgnoreCase))
        {
            if (TryBuildPatientDelete(packet, userText, out var pendingDelete, out var deleteLines))
            {
                _pendingPatientDelete = pendingDelete;
                FinishGemmaActionMessage(
                    thinkingMessage,
                    deleteLines,
                    "gemma_action_delete_patient_confirm",
                    fallbackChartContext,
                    releaseThinkingWhenDone);
                return;
            }

            FinishGemmaActionMessage(
                thinkingMessage,
                [
                    "I understood that as a chart deletion request, but I could not match one exact patient file.",
                    "Please give the full patient name."
                ],
                "gemma_action_delete_patient_unmatched",
                fallbackChartContext,
                releaseThinkingWhenDone);
            return;
        }

        if (packet.Intent.Equals("review_chart_cleanup", StringComparison.OrdinalIgnoreCase))
        {
            FinishGemmaActionMessage(
                thinkingMessage,
                BuildChartCleanupLines(),
                "gemma_action_chart_cleanup_review",
                fallbackChartContext,
                releaseThinkingWhenDone);
            return;
        }

        if (packet.Intent.Equals("request_patient_photo", StringComparison.OrdinalIgnoreCase))
        {
            var photoChartContext = ResolvePacketChartContext(packet) ?? fallbackChartContext;
            ActiveChartId = photoChartContext.ChartId;
            ActivePatientDisplayName = photoChartContext.LocalDisplayName;
            _pendingPhotoUpdate = new PendingPhotoUpdate(photoChartContext);
            FinishGemmaActionMessage(
                thinkingMessage,
                [
                    $"Sure. Attach a JPG, PNG, GIF, BMP, or WEBP image for {ExtractFirstNameForDisplay(photoChartContext.LocalDisplayName)}.",
                    "I will use it only as the chart photo and will not process it as a medical document."
                ],
                "gemma_action_patient_photo_request",
                photoChartContext,
                releaseThinkingWhenDone);
            return;
        }

        ActiveChartId = chartContext.ChartId;
        ActivePatientDisplayName = chartContext.LocalDisplayName;
        CaptureHealthPreferenceIfPresent(chartContext, userText);
        _dollyAgentStateService.StartTurn(VaultRootPath, chartContext, DeidentifyForApi(userText, _patientRegistryService.GetAllIdentities()), packet);

        if (IsVaccineListQuestion(userText))
        {
            FinishGemmaActionMessage(
                thinkingMessage,
                BuildVaccineAnswerLines(chartContext),
                "gemma_action_vaccine_answer",
                chartContext,
                releaseThinkingWhenDone);
            return;
        }

        if (IsBackendOwnedChartAnswer(packet))
        {
            await RunBackendOwnedChartAnswerFromGemmaPacketAsync(packet, userText, thinkingMessage, chartContext, releaseThinkingWhenDone);
            return;
        }

        switch (packet.Intent.ToLowerInvariant())
        {
            case "show_roster":
                FinishGemmaActionMessage(
                    thinkingMessage,
                    BuildRosterDisplayLines(),
                    "gemma_action_show_roster",
                    chartContext,
                    releaseThinkingWhenDone);
                return;

            case "run_task_board":
                _lastDollyTaskBoardResult = _dollyTaskBoardService.BuildStatus(VaultRootPath, chartContext);
                FinishGemmaActionMessage(
                    thinkingMessage,
                    BuildDollyTaskBoardMessageLines(_lastDollyTaskBoardResult),
                    "gemma_action_task_board",
                    chartContext,
                    releaseThinkingWhenDone);
                return;

            case "run_dream_runner":
                await RunDreamRunnerFromGemmaPacketAsync(thinkingMessage, chartContext, releaseThinkingWhenDone);
                return;

            case "run_specialty_weaver":
                await RunSpecialtyWeaverFromGemmaPacketAsync(thinkingMessage, chartContext, releaseThinkingWhenDone);
                return;

            case "run_symptom_scan":
                await RunSymptomScanFromGemmaPacketAsync(thinkingMessage, chartContext, releaseThinkingWhenDone);
                return;

            case "run_drug_scan":
                await RunDrugScanFromGemmaPacketAsync(thinkingMessage, chartContext, releaseThinkingWhenDone);
                return;

            case "run_pre_visit_brief":
                await RunPreVisitBriefFromGemmaPacketAsync(thinkingMessage, chartContext, releaseThinkingWhenDone);
                return;

            case "fetch_original_source":
                await FetchOriginalSourceFromGemmaPacketAsync(thinkingMessage, chartContext, userText, releaseThinkingWhenDone);
                return;

            case "refresh_working_summary":
                await RunWorkingSummaryFromGemmaPacketAsync(thinkingMessage, chartContext, releaseThinkingWhenDone);
                return;

            case "edit_chart":
                if (TryBuildCareGapResolution(packet.EditAction, chartContext, out var pendingResolution, out var resolutionLines))
                {
                    _pendingCareGapResolution = pendingResolution;
                    FinishGemmaActionMessage(
                        thinkingMessage,
                        resolutionLines,
                        "gemma_action_care_gap_resolution_confirm",
                        chartContext,
                        releaseThinkingWhenDone);
                    return;
                }

                FinishGemmaActionMessage(
                    thinkingMessage,
                    [
                        "I understood that as a chart edit, but I could not validate one exact pending item to change.",
                        "Please name the pending item exactly, or ask me to show the open care gaps first."
                    ],
                    "gemma_action_edit_chart_unmatched",
                    chartContext,
                    releaseThinkingWhenDone);
                return;

            case "store_vaccine_record":
                if (TryBuildVaccineUpdate(packet.VaccineAction, chartContext, out var pendingVaccineUpdate, out var vaccineLines))
                {
                    _pendingVaccineUpdate = pendingVaccineUpdate;
                    FinishGemmaActionMessage(
                        thinkingMessage,
                        vaccineLines,
                        "gemma_action_vaccine_record_confirm",
                        chartContext,
                        releaseThinkingWhenDone);
                    return;
                }

                FinishGemmaActionMessage(
                    thinkingMessage,
                    [
                        "I understood that as a vaccine record, but I need the vaccine name and date before I can add it.",
                        "Try: Tdap on 2025-03-13, or Flu on 2025-10-20."
                    ],
                    "gemma_action_vaccine_record_incomplete",
                    chartContext,
                    releaseThinkingWhenDone);
                return;

            case "store_user_reminder":
                if (TryBuildReminderUpdate(packet.ReminderAction, out var pendingReminderUpdate, out var reminderLines))
                {
                    _pendingReminderUpdate = pendingReminderUpdate;
                    FinishGemmaActionMessage(
                        thinkingMessage,
                        reminderLines,
                        "gemma_action_user_reminder_confirm",
                        chartContext,
                        releaseThinkingWhenDone);
                    return;
                }

                FinishGemmaActionMessage(
                    thinkingMessage,
                    [
                        "I can add that reminder, but I need both what to remind you about and when.",
                        "Try: remind me on 2026-06-01 to schedule the follow-up."
                    ],
                    "gemma_action_user_reminder_incomplete",
                    chartContext,
                    releaseThinkingWhenDone);
                return;

            case "store_patient_update":
                _pendingTypedCapture = new PendingTypedCapture(userText, chartContext);
                FinishGemmaActionMessage(
                    thinkingMessage,
                    [
                        string.IsNullOrWhiteSpace(packet.UserFacingAnswer)
                            ? $"This sounds like chart information for {chartContext.LocalDisplayName}. Should I store it in that chart?"
                            : packet.UserFacingAnswer,
                        "Please reply yes or no. I will not write it unless you confirm."
                    ],
                    "gemma_action_store_update_confirm",
                    chartContext,
                    releaseThinkingWhenDone);
                return;

            case "answer_question":
            case "query_chart":
            case "small_talk":
            case "clarify_patient":
            case "unknown":
            default:
                var answer = packet.UserFacingAnswer;

                if (string.IsNullOrWhiteSpace(answer) || LooksLikeInternalPacketAnswer(answer))
                {
                    answer = await AskGemmaForConcurrentAnswerAsync(chartContext, userText, localSessionContext);
                }

                FinishGemmaActionMessage(
                    thinkingMessage,
                    [string.IsNullOrWhiteSpace(answer) ? BuildGemmaUnavailableFallback(chartContext, localSessionContext) : answer],
                    "gemma_action_answer",
                    chartContext,
                    releaseThinkingWhenDone);
                return;
        }
    }

    private async Task RunBackendOwnedChartAnswerFromGemmaPacketAsync(
        GemmaActionPacket packet,
        string userText,
        ChatMessageViewModel thinkingMessage,
        ChartContext chartContext,
        bool releaseThinkingWhenDone = true)
    {
        ActiveChartId = chartContext.ChartId;
        ActivePatientDisplayName = chartContext.LocalDisplayName;

        var interstitial = FirstNonEmpty(
            packet.InterstitialMessage,
            packet.UserFacingAnswer,
            $"I am checking {ExtractFirstNameForDisplay(chartContext.LocalDisplayName)}'s safe chart context now.");

        VaultWriteStatus = "Chart answer running";
        UpdateDollyStatus(thinkingMessage, interstitial);
        RefreshPreview();

        var taskId = StartDollyTask(
            chartContext,
            "Backend chart answer",
            "retrieving sterile context");

        var knownPatients = _patientRegistryService.GetAllIdentities();
        var scrubbedUserText = DeidentifyForApi(userText, knownPatients);
        var contextPacket = _vaultContextService.BuildSterileContext(
            VaultRootPath,
            chartContext,
            scrubbedUserText);

        UpdateDollyTask(
            chartContext,
            taskId,
            "running",
            "writing the chart answer",
            "Dolly is only showing status while the chart answer is written.");
        UpdateDollyStatus(
            thinkingMessage,
            "I found the safe chart context. The chart answer is being written now, and I will not rewrite it after it comes back.");
        RefreshPreview();

        SetApiLlmState(true, $"{AgentDisplayName} chart answer");
        GeminiOmniboxAnswerResult backendAnswer;
        var streamedAnswer = new StringBuilder();

        try
        {
            backendAnswer = await _geminiOmniboxAnswerService.AnswerStreamingAsync(
                EnableGeminiBackendProcessing,
                GeminiThinkingModelName,
                scrubbedUserText,
                BuildRecentConversationContext(),
                contextPacket,
                delta => AppendStreamingDraft(thinkingMessage, streamedAnswer, delta));
        }
        finally
        {
            SetApiLlmState(false, "API ready");
        }

        CompleteDollyTask(
            chartContext,
            taskId,
            backendAnswer.Status,
            backendAnswer.WasAvailable
                ? "Chart answer ready."
                : "Chart answer was not available; Dolly did not invent a fallback.");

        VaultWriteStatus = backendAnswer.WasAvailable
            ? "Chart answer ready"
            : backendAnswer.Status;
        _chronosLedgerService.RecordEvent(
            VaultRootPath,
            chartContext,
            backendAnswer.WasAvailable ? "chart_answer_ready" : "chart_answer_unavailable",
            backendAnswer.WasAvailable
                ? $"Answered chart question from safe context: {TruncateForChronos(scrubbedUserText, 120)}"
                : $"Chart answer unavailable: {backendAnswer.Status}",
            AgentDisplayName,
            "chart_answer");
        UpdateDollyStatus(thinkingMessage, "The chart answer is ready. I am displaying it without rewriting it.");
        RefreshPreview();

        Messages.Remove(thinkingMessage);
        AddArchivedMessage(
            MessageAuthor.Dolly,
            AgentLabel,
            BuildLockedBackendAnswerLines(backendAnswer, chartContext, knownPatients),
            mode: "backend_locked_chart_answer",
            linkedChartId: chartContext.ChartId);

        PromptText = string.Empty;
        if (releaseThinkingWhenDone)
        {
            IsThinking = false;
        }

        RefreshPreview();
        SendMessageCommand.RaiseCanExecuteChanged();
    }

    private void HandleNaturalChartSwitch(
        GemmaActionPacket packet,
        string userText,
        ChatMessageViewModel thinkingMessage,
        ChartContext fallbackChartContext,
        bool releaseThinkingWhenDone)
    {
        var result = ResolveNaturalChartSwitch(packet, userText, fallbackChartContext);

        if (!result.WasResolved)
        {
            FinishGemmaActionMessage(
                thinkingMessage,
                result.MessageLines,
                "gemma_action_set_active_patient_clarify",
                fallbackChartContext,
                releaseThinkingWhenDone);
            return;
        }

        var previousChartId = ActiveChartId;
        ActiveChartId = result.ChartContext.ChartId;
        ActivePatientDisplayName = result.ChartContext.LocalDisplayName;
        CaptureHealthPreferenceIfPresent(result.ChartContext, userText);
        _dollyAgentStateService.StartTurn(VaultRootPath, result.ChartContext, DeidentifyForApi(userText, _patientRegistryService.GetAllIdentities()), packet);
        _chronosLedgerService.RecordEvent(
            VaultRootPath,
            result.ChartContext,
            "active_chart_switched",
            $"Active chart switched from {previousChartId} to {result.ChartContext.ChartId}.",
            AgentDisplayName,
            "session");

        FinishGemmaActionMessage(
            thinkingMessage,
            [string.IsNullOrWhiteSpace(packet.UserFacingAnswer)
                ? $"Okay, I am focused on {ExtractFirstNameForDisplay(result.ChartContext.LocalDisplayName)} now."
                : packet.UserFacingAnswer],
            "gemma_action_set_active_patient",
            result.ChartContext,
            releaseThinkingWhenDone);
    }

    private ChartSwitchResolution ResolveNaturalChartSwitch(
        GemmaActionPacket packet,
        string userText,
        ChartContext fallbackChartContext)
    {
        if (!string.IsNullOrWhiteSpace(packet.PatientDisplayName))
        {
            var identity = ResolveIdentityByNaturalName(packet.PatientDisplayName);
            if (identity is not null)
            {
                return ChartSwitchResolution.Resolved(new ChartContext(identity.ChartId, identity.PatientDisplayName));
            }
        }

        var normalizedText = NormalizeConfirmation($"{packet.Reason} {userText}");
        if (LooksLikePreviousChartRequest(normalizedText))
        {
            return ResolveChartIdFromSettings(_settings.LastActiveChartId, fallbackChartContext, "previous chart");
        }

        if (LooksLikeNewestChartRequest(normalizedText))
        {
            return ResolveNewestChart(fallbackChartContext);
        }

        var nameMatches = ResolveIdentityMatchesFromText(userText);
        if (nameMatches.Count == 1)
        {
            var match = nameMatches[0];
            return ChartSwitchResolution.Resolved(new ChartContext(match.ChartId, match.PatientDisplayName));
        }

        if (nameMatches.Count > 1)
        {
            return ChartSwitchResolution.Clarify(
                [
                    "I found more than one patient that could match that.",
                    $"Closest matches: {string.Join(", ", nameMatches.Take(4).Select(patient => patient.PatientDisplayName))}.",
                    "Which chart should I open?"
                ]);
        }

        return ChartSwitchResolution.Clarify(
            [
                "I can switch charts, but I could not match that to one patient yet.",
                "Try saying the patient name, like Open Sharon or Switch to Bruce."
            ]);
    }

    private ChartSwitchResolution ResolveChartIdFromSettings(
        string chartId,
        ChartContext fallbackChartContext,
        string label)
    {
        if (string.IsNullOrWhiteSpace(chartId) ||
            chartId.Equals(fallbackChartContext.ChartId, StringComparison.OrdinalIgnoreCase))
        {
            return ChartSwitchResolution.Clarify([$"I do not have a different {label} saved yet."]);
        }

        var identity = _patientRegistryService.GetAllIdentities()
            .FirstOrDefault(patient => patient.ChartId.Equals(chartId, StringComparison.OrdinalIgnoreCase));

        return identity is null
            ? ChartSwitchResolution.Clarify([$"I do not see that {label} in the sealed registry anymore."])
            : ChartSwitchResolution.Resolved(new ChartContext(identity.ChartId, identity.PatientDisplayName));
    }

    private ChartSwitchResolution ResolveNewestChart(ChartContext fallbackChartContext)
    {
        var configured = ResolveChartIdFromSettings(_settings.MostRecentlyCreatedChartId, fallbackChartContext, "new chart");
        if (configured.WasResolved)
        {
            return configured;
        }

        var newest = _patientRegistryService.GetAllIdentities()
            .Where(patient => !patient.ChartId.Equals(fallbackChartContext.ChartId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(patient => patient.CreatedAt)
            .FirstOrDefault();

        return newest is null
            ? configured
            : ChartSwitchResolution.Resolved(new ChartContext(newest.ChartId, newest.PatientDisplayName));
    }

    private PatientIdentityRecord? ResolveIdentityByNaturalName(string value)
    {
        var matches = ResolveIdentityMatchesFromText(value);
        return matches.Count == 1 ? matches[0] : null;
    }

    private IReadOnlyList<PatientIdentityRecord> ResolveIdentityMatchesFromText(string value)
    {
        var normalized = NormalizePersonLookupText(value);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return [];
        }

        var patients = _patientRegistryService.GetAllIdentities()
            .Where(patient => !string.IsNullOrWhiteSpace(patient.PatientDisplayName))
            .ToList();

        var exact = patients
            .Where(patient => NormalizePersonLookupText(patient.PatientDisplayName).Equals(normalized, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (exact.Count > 0)
        {
            return exact;
        }

        return patients
            .Where(patient =>
                ExtractFirstNameForDisplay(patient.PatientDisplayName).Equals(normalized, StringComparison.OrdinalIgnoreCase) ||
                normalized.Contains(NormalizePersonLookupText(patient.PatientDisplayName), StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private static bool LooksLikePreviousChartRequest(string normalizedText)
    {
        return normalizedText.Contains("previous chart", StringComparison.OrdinalIgnoreCase) ||
               normalizedText.Contains("previous patient", StringComparison.OrdinalIgnoreCase) ||
               normalizedText.Contains("last chart", StringComparison.OrdinalIgnoreCase) ||
               normalizedText.Contains("last patient", StringComparison.OrdinalIgnoreCase) ||
               normalizedText.Contains("go back", StringComparison.OrdinalIgnoreCase) ||
               normalizedText.Contains("back to", StringComparison.OrdinalIgnoreCase) ||
               normalizedText.Contains("previous_chart", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeNewestChartRequest(string normalizedText)
    {
        return normalizedText.Contains("new chart", StringComparison.OrdinalIgnoreCase) ||
               normalizedText.Contains("new patient", StringComparison.OrdinalIgnoreCase) ||
               normalizedText.Contains("newest chart", StringComparison.OrdinalIgnoreCase) ||
               normalizedText.Contains("latest chart", StringComparison.OrdinalIgnoreCase) ||
               normalizedText.Contains("show me the new chart", StringComparison.OrdinalIgnoreCase) ||
               normalizedText.Contains("newest_chart", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePersonLookupText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.ToLowerInvariant();
        normalized = Regex.Replace(normalized, @"\b(open|switch|focus|show|me|the|to|chart|patient|pt|file|go|back|last|previous|new|newest|latest|please|for)\b", " ");
        normalized = Regex.Replace(normalized, @"[^a-z0-9\s'-]+", " ");
        return Regex.Replace(normalized, @"\s+", " ").Trim();
    }

    private bool TryBuildPatientDelete(
        GemmaActionPacket packet,
        string userText,
        out PendingPatientDelete pendingDelete,
        out IReadOnlyList<string> messageLines)
    {
        pendingDelete = default!;
        messageLines = [];

        var requestedName = FirstNonEmpty(
            packet.DeletePatientAction?.PatientDisplayName ?? string.Empty,
            packet.DeletePatientAction?.TargetHint ?? string.Empty,
            packet.PatientDisplayName,
            ExtractPatientName(userText));

        var matches = ResolveIdentityMatchesFromText(requestedName);
        if (matches.Count == 0)
        {
            matches = ResolveIdentityMatchesFromText(userText);
        }

        if (matches.Count != 1)
        {
            if (matches.Count > 1)
            {
                messageLines =
                [
                    "I found more than one patient that could match that delete request.",
                    $"Closest matches: {string.Join(", ", matches.Take(4).Select(patient => patient.PatientDisplayName))}.",
                    "Please give the full patient name before I prepare the wastebasket move."
                ];
                return false;
            }

            messageLines = ["Please give the full patient name before I prepare the wastebasket move."];
            return false;
        }

        var patient = matches[0];
        pendingDelete = new PendingPatientDelete(patient);
        messageLines =
        [
            "I found the patient file, but I will not delete or move anything from a natural-language request alone.",
            $"Patient file: {patient.PatientDisplayName}",
            $"Only {ResolveChartManagerName()} can delete patient files.",
            $"To move this chart to the 7-day wastebasket, type exactly: {BuildChartManagerPendingDeletePhrase()}"
        ];
        return true;
    }

    private string ResolveChartManagerName()
    {
        return string.IsNullOrWhiteSpace(_settings.ChartManagerName)
            ? "the chart manager"
            : _settings.ChartManagerName.Trim();
    }

    private bool TryBuildVaccineUpdate(
        VaccineActionPacket? vaccineAction,
        ChartContext chartContext,
        out PendingVaccineUpdate pendingUpdate,
        out IReadOnlyList<string> messageLines)
    {
        pendingUpdate = default!;
        messageLines = [];

        if (vaccineAction is null ||
            vaccineAction.Records.Count == 0)
        {
            return false;
        }

        var records = new List<VaccineRecord>();
        foreach (var record in vaccineAction.Records)
        {
            if (string.IsNullOrWhiteSpace(record.VaccineName) ||
                string.IsNullOrWhiteSpace(record.DateGiven) ||
                !TryNormalizeVaccineDate(record.DateGiven, out var dateGiven))
            {
                continue;
            }

            records.Add(new VaccineRecord(
                NormalizeVaccineName(record.VaccineName),
                dateGiven,
                string.IsNullOrWhiteSpace(record.DoseOrSeries) ? "Unknown" : record.DoseOrSeries.Trim(),
                string.IsNullOrWhiteSpace(record.LocationOrProvider) ? "Unknown" : record.LocationOrProvider.Trim(),
                string.IsNullOrWhiteSpace(record.Notes) ? "user-reported" : record.Notes.Trim()));
        }

        if (records.Count == 0)
        {
            return false;
        }

        var wikiFolder = Path.Combine(VaultRootPath, chartContext.ChartFolderName, "wiki");
        pendingUpdate = new PendingVaccineUpdate(
            chartContext,
            Path.Combine(wikiFolder, "Vaccines.md"),
            Path.Combine(wikiFolder, "Timeline.md"),
            records,
            string.IsNullOrWhiteSpace(vaccineAction.SourceHint) ? "typed user update" : vaccineAction.SourceHint.Trim());

        messageLines =
        [
            records.Count == 1
                ? "I found one vaccine record to add."
                : $"I found {records.Count} vaccine records to add.",
            .. records.Select(record => $"- {record.VaccineName}: {record.DateGiven}"),
            "I will not change the chart unless you confirm. Reply yes to add this, or no to cancel."
        ];
        return true;
    }

    private bool TryBuildReminderUpdate(
        ReminderActionPacket? reminderAction,
        out PendingReminderUpdate pendingUpdate,
        out IReadOnlyList<string> messageLines)
    {
        pendingUpdate = default!;
        messageLines = [];

        if (reminderAction is null ||
            reminderAction.Records.Count == 0)
        {
            return false;
        }

        var reminders = new List<UserReminderRecord>();
        foreach (var record in reminderAction.Records)
        {
            if (string.IsNullOrWhiteSpace(record.ReminderText) ||
                string.IsNullOrWhiteSpace(record.DueDate) ||
                !DateOnly.TryParse(record.DueDate, out var dueDate))
            {
                continue;
            }

            var relatedChart = ResolveReminderChartLabel(record.RelatedPatientDisplayName);
            reminders.Add(new UserReminderRecord(
                record.ReminderText.Trim(),
                dueDate.ToString("yyyy-MM-dd"),
                string.IsNullOrWhiteSpace(relatedChart) ? "none" : relatedChart,
                NormalizeReminderPriority(record.Priority),
                string.IsNullOrWhiteSpace(record.Notes) ? "user-requested" : record.Notes.Trim()));
        }

        if (reminders.Count == 0)
        {
            return false;
        }

        pendingUpdate = new PendingReminderUpdate(
            Path.Combine(VaultRootPath, "User_Reminders.md"),
            reminders);

        messageLines =
        [
            reminders.Count == 1
                ? "I found one reminder to add."
                : $"I found {reminders.Count} reminders to add.",
            .. reminders.Select(reminder => $"- {reminder.DueDate}: {reminder.ReminderText}"),
            "I will not add it unless you confirm. Reply yes to save the reminder, or no to cancel."
        ];
        return true;
    }

    private IReadOnlyList<string> ApplyReminderUpdate(PendingReminderUpdate pendingUpdate)
    {
        SchedulerFileService.EnsureSchedulerFiles(VaultRootPath);
        var content = File.Exists(pendingUpdate.RemindersPath)
            ? File.ReadAllText(pendingUpdate.RemindersPath)
            : "# User Reminders\n\n| Reminder ID | Due Date | Status | Reminder | Related Chart | Created | Notes |\n|---|---|---|---|---|---|---|\n";
        var added = new List<UserReminderRecord>();

        foreach (var reminder in pendingUpdate.Records)
        {
            var marker = BuildReminderMarker(reminder);
            if (content.Contains(marker, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var row = $"| {NextReminderId(content)} | {reminder.DueDate} | open | {NormalizeTableCellForVitaMr(reminder.ReminderText)} | {NormalizeTableCellForVitaMr(reminder.RelatedChart)} | {DateTime.Now:yyyy-MM-dd} | {NormalizeTableCellForVitaMr(reminder.Notes)} {marker} |";
            content = InsertAfterMarkdownTableHeader(content, string.Empty, row);
            added.Add(reminder);
        }

        if (added.Count == 0)
        {
            return ["That reminder was already present, so I left the reminder list unchanged."];
        }

        File.WriteAllText(pendingUpdate.RemindersPath, NormalizeMarkdownContent(content));
        _chronosLedgerService.RecordEvent(
            VaultRootPath,
            GetActiveChart(),
            "user_reminder_added",
            $"{added.Count} reminder(s) added: {string.Join(", ", added.Select(reminder => $"{reminder.DueDate} {reminder.ReminderText}"))}",
            AgentDisplayName,
            "user_reminder");

        return
        [
            added.Count == 1
                ? "Done. I added one reminder."
                : $"Done. I added {added.Count} reminders.",
            .. added.Select(reminder => $"{reminder.DueDate}: {reminder.ReminderText}")
        ];
    }

    private string ResolveReminderChartLabel(string patientDisplayName)
    {
        if (string.IsNullOrWhiteSpace(patientDisplayName))
        {
            return ActivePatientDisplayName;
        }

        var identity = ResolveIdentityByNaturalName(patientDisplayName);
        return identity?.PatientDisplayName ?? patientDisplayName.Trim();
    }

    private static string NormalizeReminderPriority(string value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? "routine" : value.Trim().ToLowerInvariant();
        return normalized is "high" or "routine" or "low" ? normalized : "routine";
    }

    private static string BuildReminderMarker(UserReminderRecord reminder)
    {
        var key = Regex.Replace($"{reminder.DueDate}-{reminder.RelatedChart}-{reminder.ReminderText}".ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
        return $"<!-- reminder:{key} -->";
    }

    private static string NextReminderId(string content)
    {
        var next = Regex.Matches(content, @"REM-(\d+)", RegexOptions.IgnoreCase)
            .Select(match => int.TryParse(match.Groups[1].Value, out var number) ? number : 0)
            .DefaultIfEmpty(0)
            .Max() + 1;
        return $"REM-{next:000}";
    }

    private IReadOnlyList<string> ApplyVaccineUpdate(PendingVaccineUpdate pendingUpdate)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(pendingUpdate.VaccinesPath)!);
        EnsureVaccinesFile(pendingUpdate.VaccinesPath);
        EnsureTimelineFile(pendingUpdate.TimelinePath, pendingUpdate.ChartContext);

        var content = File.ReadAllText(pendingUpdate.VaccinesPath);
        var timeline = File.ReadAllText(pendingUpdate.TimelinePath);
        var added = new List<VaccineRecord>();

        foreach (var record in pendingUpdate.Records)
        {
            var marker = BuildVaccineMarker(record);
            if (content.Contains(marker, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var source = NormalizeTableCellForVitaMr(pendingUpdate.SourceHint);
            content = InsertAfterMarkdownTableHeader(
                content,
                string.Empty,
                $"| {record.DateGiven} | {NormalizeTableCellForVitaMr(record.VaccineName)} | {NormalizeTableCellForVitaMr(record.DoseOrSeries)} | {NormalizeTableCellForVitaMr(record.LocationOrProvider)} | {source} {marker} | {NormalizeTableCellForVitaMr(record.Notes)} |");
            timeline = InsertAfterMarkdownTableHeader(
                timeline,
                string.Empty,
                $"| {record.DateGiven} | Vaccine | {NormalizeTableCellForVitaMr(record.VaccineName)} recorded. | low | [[Vaccines]] {marker} |");
            added.Add(record);
        }

        if (added.Count == 0)
        {
            return ["That vaccine record was already present, so I left the chart unchanged."];
        }

        File.WriteAllText(pendingUpdate.VaccinesPath, NormalizeMarkdownContent(content));
        File.WriteAllText(pendingUpdate.TimelinePath, NormalizeMarkdownContent(timeline));

        _chronosLedgerService.RecordEvent(
            VaultRootPath,
            pendingUpdate.ChartContext,
            "vaccine_record_added",
            $"{added.Count} vaccine record(s) added: {string.Join(", ", added.Select(record => $"{record.VaccineName} {record.DateGiven}"))}",
            "C#",
            "wiki_update");

        return
        [
            added.Count == 1
                ? "Done. I added one vaccine record."
                : $"Done. I added {added.Count} vaccine records.",
            .. added.Select(record => $"{record.VaccineName}: {record.DateGiven}"),
            "I also added this to the timeline so Dolly can find it later."
        ];
    }

    private static void EnsureVaccinesFile(string path)
    {
        if (File.Exists(path))
        {
            return;
        }

        File.WriteAllText(
            path,
            NormalizeMarkdownContent(
                "# Vaccines\n\n| Date Given | Vaccine | Dose / Series | Location / Provider | Source | Notes |\n|---|---|---|---|---|---|\n"));
    }

    private static void EnsureTimelineFile(string path, ChartContext chartContext)
    {
        if (File.Exists(path))
        {
            return;
        }

        File.WriteAllText(
            path,
            NormalizeMarkdownContent(
                $"# {chartContext.ChartId} Timeline\n\n| Date | Event Type | Summary | Risk Level | Source |\n|---|---|---|---|---|\n"));
    }

    private static bool TryNormalizeVaccineDate(string value, out string normalized)
    {
        if (DateOnly.TryParse(value, out var parsed))
        {
            normalized = parsed.ToString("yyyy-MM-dd");
            return true;
        }

        var cleaned = Regex.Replace(value.Trim(), @"\s+", " ");
        var rangeMatch = Regex.Match(cleaned, @"\b(?<start>20\d{2}|19\d{2})\s*(?:-|to|through|thru|until)\s*(?<end>20\d{2}|19\d{2})\b", RegexOptions.IgnoreCase);
        if (rangeMatch.Success)
        {
            normalized = $"{rangeMatch.Groups["start"].Value}-{rangeMatch.Groups["end"].Value}";
            return true;
        }

        var yearMatch = Regex.Match(cleaned, @"\b(?<year>20\d{2}|19\d{2})\b");
        if (yearMatch.Success)
        {
            normalized = yearMatch.Groups["year"].Value;
            return true;
        }

        normalized = string.Empty;
        return false;
    }

    private static string NormalizeVaccineName(string value)
    {
        var normalized = Regex.Replace(value.Trim(), @"\s+", " ");
        return normalized.Equals("flu", StringComparison.OrdinalIgnoreCase)
            ? "Influenza"
            : normalized;
    }

    private IReadOnlyList<string> BuildVaccineAnswerLines(ChartContext chartContext)
    {
        var path = Path.Combine(VaultRootPath, chartContext.ChartFolderName, "wiki", "Vaccines.md");
        var rows = ReadVaccineRows(path);

        if (rows.Count == 0)
        {
            return
            [
                $"I do not see any vaccine records filed for {ExtractFirstNameForDisplay(chartContext.LocalDisplayName)} yet.",
                "If you have a vaccine card, portal note, or typed vaccine dates, I can add them after confirmation."
            ];
        }

        return
        [
            $"{ExtractFirstNameForDisplay(chartContext.LocalDisplayName)} has these vaccine records on file:",
            .. rows.Select(row => $"- {row.Vaccine}: {row.DateGiven}{BuildVaccineDetailSuffix(row)}")
        ];
    }

    private static string BuildVaccineDetailSuffix(VaccineTableRow row)
    {
        var details = new List<string>();

        if (!string.IsNullOrWhiteSpace(row.DoseOrSeries) &&
            !row.DoseOrSeries.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
        {
            details.Add(row.DoseOrSeries);
        }

        if (!string.IsNullOrWhiteSpace(row.Notes) &&
            !row.Notes.Equals("user-reported", StringComparison.OrdinalIgnoreCase))
        {
            details.Add(row.Notes);
        }

        return details.Count == 0
            ? string.Empty
            : $" ({string.Join("; ", details)})";
    }

    private static IReadOnlyList<VaccineTableRow> ReadVaccineRows(string path)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        var rows = new List<VaccineTableRow>();
        foreach (var line in File.ReadAllLines(path))
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith('|') ||
                trimmed.StartsWith("|---", StringComparison.Ordinal) ||
                trimmed.Contains("Date Given", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var columns = trimmed.Trim('|')
                .Split('|')
                .Select(column => Regex.Replace(column, @"<!--.*?-->", string.Empty).Trim())
                .ToList();

            if (columns.Count < 6 ||
                string.IsNullOrWhiteSpace(columns[0]) ||
                string.IsNullOrWhiteSpace(columns[1]))
            {
                continue;
            }

            rows.Add(new VaccineTableRow(
                columns[0],
                columns[1],
                columns[2],
                columns[3],
                columns[4],
                columns[5]));
        }

        return rows
            .OrderByDescending(row => ReadSortableVaccineDate(row.DateGiven))
            .ThenBy(row => row.Vaccine, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static int ReadSortableVaccineDate(string value)
    {
        if (DateOnly.TryParse(value, out var date))
        {
            return (date.Year * 10000) + (date.Month * 100) + date.Day;
        }

        var yearMatch = Regex.Match(value, @"\b(20\d{2}|19\d{2})\b");
        return yearMatch.Success && int.TryParse(yearMatch.Value, out var year)
            ? year * 10000
            : 0;
    }

    private static bool IsVaccineListQuestion(string userText)
    {
        if (string.IsNullOrWhiteSpace(userText))
        {
            return false;
        }

        return Regex.IsMatch(
            userText,
            @"\b(vaccine|vaccines|immunization|immunizations|shots?|immunized)\b",
            RegexOptions.IgnoreCase) &&
            Regex.IsMatch(
                userText,
                @"\b(what|which|list|show|have|has|record|records|history|doc|docs|paperwork)\b",
                RegexOptions.IgnoreCase);
    }

    private static string BuildVaccineMarker(VaccineRecord record)
    {
        var key = Regex.Replace($"{record.VaccineName}-{record.DateGiven}".ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
        return $"<!-- vaccine:{key} -->";
    }

    private static string InsertAfterMarkdownTableHeader(string content, string sectionName, string row)
    {
        var lines = content.Replace("\r\n", "\n").Split('\n').ToList();
        var start = string.IsNullOrWhiteSpace(sectionName)
            ? 0
            : lines.FindIndex(line => line.Trim().Equals(sectionName, StringComparison.OrdinalIgnoreCase));

        if (start < 0)
        {
            start = 0;
        }

        for (var index = start; index < lines.Count; index++)
        {
            if (lines[index].StartsWith("|---", StringComparison.Ordinal))
            {
                lines.Insert(index + 1, row);
                return string.Join(Environment.NewLine, lines);
            }
        }

        lines.Add(row);
        return string.Join(Environment.NewLine, lines);
    }

    private static string NormalizeTableCellForVitaMr(string value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? "Unknown" : Regex.Replace(value.Trim(), @"\s+", " ");
        return normalized.Replace("|", "/", StringComparison.Ordinal);
    }

    private static string NormalizeMarkdownContent(string content)
    {
        return content.Replace("\r\n", "\n").TrimEnd() + Environment.NewLine;
    }

    private bool TryBuildCareGapResolution(
        ChartEditActionPacket? editAction,
        ChartContext chartContext,
        out PendingCareGapResolution pendingResolution,
        out IReadOnlyList<string> messageLines)
    {
        pendingResolution = default!;
        messageLines = [];

        if (editAction is null ||
            !editAction.TargetType.Equals("care_gap", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var careGapsPath = Path.Combine(VaultRootPath, chartContext.ChartFolderName, "wiki", "Care_Gaps.md");
        if (!File.Exists(careGapsPath))
        {
            return false;
        }

        var rows = File.ReadAllLines(careGapsPath)
            .Select(ParseCareGapRow)
            .Where(row => row is not null)
            .Cast<CareGapRow>()
            .Where(row => row.Status.Equals("open", StringComparison.OrdinalIgnoreCase))
            .Select(row => new
            {
                Row = row,
                Score = ScoreCareGapMatch(editAction, row)
            })
            .Where(candidate => candidate.Score > 0)
            .OrderByDescending(candidate => candidate.Score)
            .ToList();

        if (rows.Count == 0)
        {
            messageLines =
            [
                $"I can help {DescribeEditOperation(editAction.Operation)} a pending chart item, but I could not match Dolly's target to one specific open item yet.",
                "Try naming the pending item exactly, like Stat Echo, CPET, Cardiac CT, or pre-procedural labs."
            ];
            return true;
        }

        if (rows.Count > 1 &&
            rows[0].Score == rows[1].Score)
        {
            messageLines =
            [
                "I found more than one open pending item that could match Dolly's edit target.",
                $"Closest matches: {string.Join("; ", rows.Take(3).Select(row => $"{row.Row.GapId} - {row.Row.Title}"))}.",
                "Please name the exact pending item to change."
            ];
            return true;
        }

        var best = rows[0].Row;
        var newStatus = NormalizeCareGapStatus(editAction.NewStatus, editAction.Operation);
        var reason = string.IsNullOrWhiteSpace(editAction.Reason)
            ? editAction.TargetHint
            : editAction.Reason;
        pendingResolution = new PendingCareGapResolution(chartContext, careGapsPath, best.GapId, best.Title, newStatus, reason);
        messageLines =
        [
            $"I found this open pending item: {best.GapId} - {best.Title}.",
            $"I can mark it {newStatus}. I will not change the chart unless you confirm.",
            "Reply yes to apply this change, or no to leave it open."
        ];
        return true;
    }

    private IReadOnlyList<string> ApplyCareGapResolution(PendingCareGapResolution pendingResolution)
    {
        var lines = File.ReadAllLines(pendingResolution.CareGapsPath).ToList();
        var changed = false;
        var auditNote = BuildCareGapResolutionNote(pendingResolution);

        for (var index = 0; index < lines.Count; index++)
        {
            var row = ParseCareGapRow(lines[index]);
            if (row is null ||
                !row.GapId.Equals(pendingResolution.GapId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            row.Columns[3] = pendingResolution.NewStatus;
            row.Columns[7] = string.IsNullOrWhiteSpace(row.Columns[7])
                ? auditNote
                : $"{row.Columns[7]} {auditNote}";
            lines[index] = BuildMarkdownTableRow(row.Columns);
            changed = true;
            break;
        }

        if (!changed)
        {
            return ["I could not find that pending item anymore, so I left the chart unchanged."];
        }

        File.WriteAllLines(pendingResolution.CareGapsPath, lines);
        _chronosLedgerService.RecordEvent(
            VaultRootPath,
            pendingResolution.ChartContext,
            "care_gap_updated",
            $"{pendingResolution.GapId} marked {pendingResolution.NewStatus}: {pendingResolution.Title}",
            "C#",
            "wiki_update");

        return
        [
            $"Done. I marked {pendingResolution.GapId} as {pendingResolution.NewStatus}.",
            pendingResolution.Title
        ];
    }

    private static string NormalizeCareGapStatus(string requestedStatus, string operation)
    {
        if (requestedStatus.Equals("open", StringComparison.OrdinalIgnoreCase) ||
            requestedStatus.Equals("resolved", StringComparison.OrdinalIgnoreCase) ||
            requestedStatus.Equals("canceled", StringComparison.OrdinalIgnoreCase))
        {
            return requestedStatus.ToLowerInvariant();
        }

        return operation.Equals("cancel_care_gap", StringComparison.OrdinalIgnoreCase)
            ? "canceled"
            : "resolved";
    }

    private static string DescribeEditOperation(string operation)
    {
        return operation.Equals("cancel_care_gap", StringComparison.OrdinalIgnoreCase)
            ? "cancel"
            : "resolve";
    }

    private static int ScoreCareGapMatch(ChartEditActionPacket editAction, CareGapRow row)
    {
        if (!string.IsNullOrWhiteSpace(editAction.TargetId) &&
            row.GapId.Equals(editAction.TargetId, StringComparison.OrdinalIgnoreCase))
        {
            return 10_000;
        }

        var request = NormalizeMatchText(editAction.TargetHint);
        var candidate = NormalizeMatchText(row.Title);
        var score = 0;

        foreach (var token in request.Split(' ', StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (token.Length < 3 ||
                token is "the" or "for" or "and" or "with" or "that" or "this" or "pending" or "cancel" or "canceled" or "cancelled" or "close" or "closed" or "resolve" or "resolved")
            {
                continue;
            }

            if (candidate.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                score += token.Length;
            }
        }

        if ((request.Contains("stat echo", StringComparison.OrdinalIgnoreCase) ||
             request.Contains("stat echocardiogram", StringComparison.OrdinalIgnoreCase)) &&
            candidate.Contains("echocardiogram", StringComparison.OrdinalIgnoreCase))
        {
            score += 20;
        }

        if (request.Contains("stat", StringComparison.OrdinalIgnoreCase) &&
            candidate.Contains("stat", StringComparison.OrdinalIgnoreCase))
        {
            score += 40;
        }

        if ((request.Contains("cpet", StringComparison.OrdinalIgnoreCase) ||
             request.Contains("cardiopulm", StringComparison.OrdinalIgnoreCase)) &&
            (candidate.Contains("cpet", StringComparison.OrdinalIgnoreCase) ||
             candidate.Contains("cardiopulmonary", StringComparison.OrdinalIgnoreCase)))
        {
            score += 20;
        }

        return score;
    }

    private static string BuildCareGapResolutionNote(PendingCareGapResolution pendingResolution)
    {
        var reason = string.IsNullOrWhiteSpace(pendingResolution.Reason)
            ? "user-confirmed chart edit"
            : pendingResolution.Reason;

        return $"Updated {DateTime.Now:yyyy-MM-dd}: marked {pendingResolution.NewStatus} after user confirmation. Reason: {reason}.";
    }

    private static string NormalizeMatchText(string value)
    {
        var normalized = Regex.Replace(value.ToLowerInvariant(), @"[^a-z0-9\s]+", " ");
        normalized = normalized.Replace("echo", "echocardiogram", StringComparison.OrdinalIgnoreCase);
        return Regex.Replace(normalized, @"\s+", " ").Trim();
    }

    private static CareGapRow? ParseCareGapRow(string line)
    {
        if (string.IsNullOrWhiteSpace(line) ||
            !line.StartsWith("|", StringComparison.Ordinal) ||
            line.Contains("---", StringComparison.Ordinal))
        {
            return null;
        }

        var columns = line.Trim().Trim('|').Split('|').Select(part => part.Trim()).ToList();
        if (columns.Count < 8 ||
            columns[0].Equals("Gap ID", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return new CareGapRow(columns);
    }

    private static string BuildMarkdownTableRow(IReadOnlyList<string> columns)
    {
        return $"| {string.Join(" | ", columns)} |";
    }

    private static bool IsBackendOwnedChartAnswer(GemmaActionPacket packet)
    {
        return packet.Intent.Equals("query_chart", StringComparison.OrdinalIgnoreCase) ||
               packet.ResponseOwner.Equals("backend", StringComparison.OrdinalIgnoreCase) ||
               packet.DisplayMode.Equals("chart_answer", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> BuildLockedBackendAnswerLines(
        GeminiOmniboxAnswerResult backendAnswer,
        ChartContext chartContext,
        IReadOnlyList<PatientIdentityRecord> knownPatients)
    {
        if (!backendAnswer.WasAvailable)
        {
            yield return "I could not complete that chart answer yet. I stopped instead of guessing from incomplete context.";
            yield break;
        }

        foreach (var line in PolishChartAnswerForChat(backendAnswer.Answer, chartContext, knownPatients))
        {
            yield return line;
        }
    }

    private static IReadOnlyList<string> PolishChartAnswerForChat(
        string answer,
        ChartContext chartContext,
        IReadOnlyList<PatientIdentityRecord> knownPatients)
    {
        var cleaned = string.IsNullOrWhiteSpace(answer)
            ? "I checked the safe chart context, but I do not have a clear answer yet."
            : answer.Trim();

        cleaned = Regex.Replace(cleaned, @"`?[^`\r\n]*\.final-scrubbed\.txt`?", "the safe chart note", RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"`?[^`\r\n]*\.scrubbed\.txt`?", "the safe chart note", RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"\b(?:backend|Gemini|API|payload|packet|schema)\b", "chart", RegexOptions.IgnoreCase);

        foreach (var patient in knownPatients
                     .Where(patient => !string.IsNullOrWhiteSpace(patient.PatientDisplayName))
                     .OrderByDescending(patient => patient.PatientDisplayName.Length))
        {
            var firstName = ExtractFirstNameForDisplay(patient.PatientDisplayName);
            cleaned = Regex.Replace(
                cleaned,
                $@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(patient.PatientDisplayName.Trim())}(?![\p{{L}}\p{{N}}])",
                firstName,
                RegexOptions.IgnoreCase);
        }

        return SplitChartAnswerIntoFriendlyParagraphs(cleaned);
    }

    private static IReadOnlyList<string> SplitChartAnswerIntoFriendlyParagraphs(string answer)
    {
        var normalized = answer
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Trim();

        if (string.IsNullOrWhiteSpace(normalized))
        {
            return ["I checked the safe chart context, but I do not have a clear answer yet."];
        }

        normalized = Regex.Replace(
            normalized,
            @"(?im)^\s*according to (?:the )?(?:encounter )?documentation\s*:?\s*$",
            "From the safe chart notes:");
        normalized = Regex.Replace(normalized, @"\*\*(?<label>[^:*]{2,40}):\*\*", "${label}:", RegexOptions.IgnoreCase);
        normalized = Regex.Replace(normalized, @"\*\*(?<label>[^*]{2,40})\*\*\s*:", "${label}:", RegexOptions.IgnoreCase);
        normalized = Regex.Replace(normalized, @"\*\*(?<text>[^*]+)\*\*", "${text}", RegexOptions.IgnoreCase);

        var paragraphs = new List<string>();

        foreach (var block in Regex.Split(normalized, @"\n\s*\n"))
        {
            var lines = block
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            if (lines.Count == 0)
            {
                continue;
            }

            if (lines.All(IsMarkdownBulletLine))
            {
                paragraphs.AddRange(lines.Select(CleanBulletLine).Where(line => !string.IsNullOrWhiteSpace(line)));
                continue;
            }

            var joined = string.Join(
                    " ",
                    lines.Select(line => IsMarkdownBulletLine(line) ? CleanBulletLine(line) : CleanMarkdownText(line)))
                .Trim();

            if (!string.IsNullOrWhiteSpace(joined))
            {
                paragraphs.Add(joined);
            }
        }

        return paragraphs.Count == 0
            ? [CleanMarkdownText(normalized)]
            : paragraphs;
    }

    private static bool IsMarkdownBulletLine(string line)
    {
        return Regex.IsMatch(line, @"^\s*(?:[*\-+]|\d+[.)])\s+");
    }

    private static string CleanBulletLine(string line)
    {
        var cleaned = Regex.Replace(line, @"^\s*(?:[*\-+]|\d+[.)])\s+", string.Empty);
        return CleanMarkdownText(cleaned);
    }

    private static string CleanMarkdownText(string value)
    {
        var cleaned = value.Trim();
        cleaned = Regex.Replace(cleaned, @"\*\*(?<text>[^*]+)\*\*", "${text}");
        cleaned = Regex.Replace(cleaned, @"\*(?<text>[^*]+)\*", "${text}");
        cleaned = Regex.Replace(cleaned, @"`(?<text>[^`]+)`", "${text}");
        cleaned = Regex.Replace(cleaned, @"\s{2,}", " ");
        return cleaned.Trim();
    }

    private static string FirstNonEmpty(params string[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
    }

    private static string ExtractFirstNameForDisplay(string displayName)
    {
        var parts = displayName
            .Trim()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return parts.Length == 0 ? displayName.Trim() : parts[0];
    }

    private void OnActivePatientVisualChanged()
    {
        OnPropertyChanged(nameof(ActivePatientPhotoPath));
        OnPropertyChanged(nameof(ActivePatientPhotoImage));
        OnPropertyChanged(nameof(ActivePatientInitials));
        OnPropertyChanged(nameof(PatientPhotoVisibility));
        OnPropertyChanged(nameof(PatientInitialsVisibility));
    }

    private static string BuildInitials(string displayName)
    {
        var parts = displayName
            .Trim()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(part => part.Length > 0)
            .Take(2)
            .ToList();

        if (parts.Count == 0)
        {
            return "?";
        }

        return string.Concat(parts.Select(part => char.ToUpperInvariant(part[0])));
    }

    private ChartContext? ResolvePacketChartContext(GemmaActionPacket packet)
    {
        if (string.IsNullOrWhiteSpace(packet.PatientDisplayName))
        {
            return null;
        }

        var identity = _patientRegistryService.TryResolveIdentityByName(packet.PatientDisplayName);
        return identity is null
            ? null
            : new ChartContext(identity.ChartId, identity.PatientDisplayName);
    }

    private IEnumerable<string> BuildRosterDisplayLines()
    {
        var patients = _patientRegistryService.GetAllIdentities()
            .Where(patient => !string.IsNullOrWhiteSpace(patient.PatientDisplayName))
            .Where(patient => Directory.Exists(Path.Combine(VaultRootPath, patient.ChartId)))
            .OrderBy(patient => patient.PatientDisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(patient => patient.PatientDisplayName)
            .ToList();

        if (patients.Count == 0)
        {
            return ["I do not see any active patient files in the sealed registry yet."];
        }

        return ["I currently have these patient files:", .. patients.Select(patient => $"- {patient}")];
    }

    private void FinishGemmaActionMessage(
        ChatMessageViewModel thinkingMessage,
        IEnumerable<string> lines,
        string mode,
        ChartContext chartContext,
        bool releaseThinkingWhenDone = true)
    {
        var responseLines = lines.ToList();
        Messages.Remove(thinkingMessage);
        AddArchivedMessage(
            MessageAuthor.Dolly,
            AgentLabel,
            responseLines,
            mode: mode,
            linkedChartId: chartContext.ChartId);
        TrackActionPacketVisibility(
            chartContext,
            AuditStageForMode(mode),
            $"{mode}: {TruncateForChronos(string.Join(" ", responseLines), 140)}");

        VaultWriteStatus = $"{AgentDisplayName} handled that";
        PromptText = string.Empty;
        if (releaseThinkingWhenDone)
        {
            IsThinking = false;
        }

        RefreshPreview();
        SendMessageCommand.RaiseCanExecuteChanged();
    }

    private static string AuditStageForMode(string mode)
    {
        if (mode.Contains("confirm", StringComparison.OrdinalIgnoreCase) ||
            mode.Contains("waiting", StringComparison.OrdinalIgnoreCase))
        {
            return "awaiting_confirmation";
        }

        if (mode.Contains("unmatched", StringComparison.OrdinalIgnoreCase) ||
            mode.Contains("incomplete", StringComparison.OrdinalIgnoreCase) ||
            mode.Contains("invalid", StringComparison.OrdinalIgnoreCase) ||
            mode.Contains("low_confidence", StringComparison.OrdinalIgnoreCase) ||
            mode.Contains("error", StringComparison.OrdinalIgnoreCase))
        {
            return "blocked";
        }

        return "dispatched";
    }

    private void FinishConcurrentGemmaActionMessage(
        ChatMessageViewModel thinkingMessage,
        IEnumerable<string> lines,
        string mode,
        ChartContext chartContext)
    {
        FinishGemmaActionMessage(
            thinkingMessage,
            lines,
            mode,
            chartContext,
            releaseThinkingWhenDone: false);
    }

    private static bool IsAgentTurnCompletionMode(string mode)
    {
        return mode.StartsWith("gemma_action", StringComparison.OrdinalIgnoreCase) ||
               mode.Equals("backend_locked_chart_answer", StringComparison.OrdinalIgnoreCase);
    }

    private async Task RunDreamRunnerFromGemmaPacketAsync(ChatMessageViewModel thinkingMessage, ChartContext chartContext, bool releaseThinkingWhenDone = true)
    {
        VaultWriteStatus = "Running Dream Runner audit";
        UpdateDollyStatus(thinkingMessage, "I am running Dream Runner as a report-only audit now.");
        RefreshPreview();
        await Task.Yield();

        var taskId = StartDollyTask(chartContext, "Dream Runner audit", "checking required sterile wiki files");
        var auditResult = _dreamRunnerAuditService.AuditChart(VaultRootPath, chartContext);
        _lastDreamRunnerAuditResult = auditResult;
        CompleteDollyTask(chartContext, taskId, auditResult.Status, $"Open gaps: {auditResult.OpenCareGaps}; conflicts: {auditResult.ActiveConflicts}.");
        FinishGemmaActionMessage(thinkingMessage, BuildDreamRunnerAuditMessageLines(auditResult), "gemma_action_dream_runner", chartContext, releaseThinkingWhenDone);
    }

    private async Task RunSpecialtyWeaverFromGemmaPacketAsync(ChatMessageViewModel thinkingMessage, ChartContext chartContext, bool releaseThinkingWhenDone = true)
    {
        VaultWriteStatus = "Running Specialty Weaver";
        UpdateDollyStatus(thinkingMessage, "Dolly mapped this to Specialty Weaver. I am organizing the chart into specialty lenses now.");
        RefreshPreview();
        await Task.Yield();

        var taskId = StartDollyTask(chartContext, "Specialty Weaver", "routing encounter nodes into specialty lenses");
        var result = _specialtyWeaverService.WeaveChart(VaultRootPath, chartContext);
        _lastSpecialtyWeaverResult = result;
        CompleteDollyTask(chartContext, taskId, result.Status, $"Rows written: {result.RowsWritten}.");
        FinishGemmaActionMessage(thinkingMessage, BuildSpecialtyWeaverMessageLines(result), "gemma_action_specialty_weaver", chartContext, releaseThinkingWhenDone);
    }

    private async Task RunSymptomScanFromGemmaPacketAsync(ChatMessageViewModel thinkingMessage, ChartContext chartContext, bool releaseThinkingWhenDone = true)
    {
        VaultWriteStatus = "Running symptom pattern scan";
        UpdateDollyStatus(thinkingMessage, "Dolly mapped this to Symptom Pattern Scan. I am scanning sterile symptom journals now.");
        RefreshPreview();
        await Task.Yield();

        var taskId = StartDollyTask(chartContext, "Symptom Pattern Scan", "scanning sterile symptom journals");
        var result = _patternLinkerService.ScanAllCharts(VaultRootPath, _patientRegistryService.GetAllIdentities());
        _lastPatternLinkerResult = result;
        CompleteDollyTask(chartContext, taskId, result.Status, $"Patterns written: {result.PatternsWritten}.");
        FinishGemmaActionMessage(thinkingMessage, BuildPatternLinkerMessageLines(result), "gemma_action_symptom_scan", chartContext, releaseThinkingWhenDone);
    }

    private async Task RunDrugScanFromGemmaPacketAsync(ChatMessageViewModel thinkingMessage, ChartContext chartContext, bool releaseThinkingWhenDone = true)
    {
        VaultWriteStatus = "Running drug interaction scan";
        UpdateDollyStatus(thinkingMessage, "Dolly mapped this to Drug Interaction Scan. I am checking the sterile medication rows now.");
        RefreshPreview();
        await Task.Yield();

        var taskId = StartDollyTask(chartContext, "Drug Interaction Scan", "checking sterile medication rows");
        var result = _drugInteractionScannerService.ScanChart(VaultRootPath, chartContext, "Dolly API action packet");
        _lastDrugInteractionScanResult = result;
        CompleteDollyTask(chartContext, taskId, result.Status, $"Prototype findings: {result.FindingsWritten}.");
        FinishGemmaActionMessage(thinkingMessage, BuildDrugInteractionMessageLines(result), "gemma_action_drug_scan", chartContext, releaseThinkingWhenDone);
    }

    private async Task RunPreVisitBriefFromGemmaPacketAsync(ChatMessageViewModel thinkingMessage, ChartContext chartContext, bool releaseThinkingWhenDone = true)
    {
        VaultWriteStatus = "Generating pre-visit brief";
        UpdateDollyStatus(thinkingMessage, "Dolly mapped this to Pre-Visit Brief. I am compiling the sterile visit-prep packet now.");
        RefreshPreview();
        await Task.Yield();

        var taskId = StartDollyTask(chartContext, "Pre-Visit Brief", "compiling sterile visit-prep packet");
        var result = _preVisitBriefService.GenerateBrief(VaultRootPath, chartContext);
        _lastPreVisitBriefResult = result;
        CompleteDollyTask(chartContext, taskId, result.Status, $"Sections included: {result.SectionsIncluded}.");
        FinishGemmaActionMessage(thinkingMessage, BuildPreVisitBriefMessageLines(result), "gemma_action_pre_visit_brief", chartContext, releaseThinkingWhenDone);
    }

    private async Task FetchOriginalSourceFromGemmaPacketAsync(
        ChatMessageViewModel thinkingMessage,
        ChartContext chartContext,
        string userText,
        bool releaseThinkingWhenDone = true)
    {
        VaultWriteStatus = "Fetching saved original source";
        UpdateDollyStatus(thinkingMessage, "I am looking for the saved original file. If I find it, I will attach it unchanged.");
        RefreshPreview();
        await Task.Yield();

        var taskId = StartDollyTask(chartContext, "Fetch original source", "searching saved raw originals");
        var result = _originalSourceRetrievalService.FindOriginalSources(VaultRootPath, chartContext, userText);
        CompleteDollyTask(chartContext, taskId, result.Status, result.WasFound
            ? $"Attached {result.SourcePaths.Count} original file(s)."
            : "No matching original source found.");

        Messages.Remove(thinkingMessage);
        AddArchivedMessage(
            MessageAuthor.Dolly,
            AgentLabel,
            result.Messages,
            result.SourcePaths.Select(path => new AttachmentViewModel(path)).ToList(),
            mode: "gemma_action_original_source",
            linkedChartId: chartContext.ChartId);

        VaultWriteStatus = result.Status;
        PromptText = string.Empty;
        if (releaseThinkingWhenDone)
        {
            IsThinking = false;
        }

        RefreshPreview();
        SendMessageCommand.RaiseCanExecuteChanged();
    }

    private async Task RunWorkingSummaryFromGemmaPacketAsync(ChatMessageViewModel thinkingMessage, ChartContext chartContext, bool releaseThinkingWhenDone = true)
    {
        VaultWriteStatus = "Refreshing Dolly working summary";
        UpdateDollyStatus(thinkingMessage, "Dolly mapped this to a working-summary refresh. I am rebuilding Dolly's sterile local context now.");
        RefreshPreview();
        await RunDollyWorkingSummaryThroughGemmaAsync(
            chartContext,
            "Dolly API action packet",
            allowDesktopFallback: true,
            announceCompletion: false,
            taskType: "user_task",
            priority: 2);

        FinishGemmaActionMessage(
            thinkingMessage,
            BuildDollyWorkingSummaryMessageLines(_lastDollyWorkingSummaryResult!),
            "gemma_action_working_summary",
            chartContext,
            releaseThinkingWhenDone);
    }

    private string BuildLocalSessionContextForGemma(ChartContext chartContext)
    {
        var builder = new StringBuilder();
        var patientContext = EnsureConcurrentWorkingSummaryContext(chartContext);
        var familyContextPath = Path.Combine(VaultRootPath, "_System", "Family_Context.md");
        var familyContext = File.Exists(familyContextPath) ? File.ReadAllText(familyContextPath) : BuildFamilyContextTextFallback();
        var taskBoard = _dollyTaskBoardService.BuildStatus(VaultRootPath, chartContext);
        var preferenceUserName = ResolvePreferenceUserName(chartContext);
        var preferenceContext = _healthPreferenceService.BuildPreferenceContext(VaultRootPath, preferenceUserName);
        var chartFileContext = ReadActiveChartFileContext(chartContext);
        var patientPersonaContext = ReadActivePatientPersonaContext(chartContext);
        var managerPersonaContext = ReadChartManagerPersonaContext();
        _lastDollyTaskBoardResult = taskBoard;

        builder.AppendLine("# Local Session Context");
        builder.AppendLine();
        builder.AppendLine("## Dolly Orientation");
        builder.AppendLine(BuildDollyOrientationContext(chartContext));
        builder.AppendLine();
        builder.AppendLine("## Healthspan Mode Boundary");
        foreach (var line in BuildHealthspanModeContextLines())
        {
            builder.AppendLine($"- {line}");
        }

        builder.AppendLine();
        builder.AppendLine("## Chronos Ledger");
        builder.AppendLine(_chronosLedgerService.BuildRecentContext(VaultRootPath, chartContext, 12));
        builder.AppendLine();
        builder.AppendLine("## User Health Preferences / Northstar");
        builder.AppendLine($"Preference profile: {preferenceUserName}");
        builder.AppendLine(preferenceContext);
        builder.AppendLine();
        builder.AppendLine("## Chart Manager Persona");
        builder.AppendLine(managerPersonaContext);
        builder.AppendLine();
        builder.AppendLine("## Family Vault");
        builder.AppendLine(familyContext);
        builder.AppendLine();
        builder.AppendLine("## Active Patient Context");
        builder.AppendLine(patientContext);
        builder.AppendLine();
        builder.AppendLine("## Active Chart File");
        builder.AppendLine(chartFileContext);
        builder.AppendLine();
        builder.AppendLine("## Active Patient Persona");
        builder.AppendLine(patientPersonaContext);
        builder.AppendLine();
        builder.AppendLine("## Current Background Task Status");
        foreach (var line in BuildDollyTaskBoardMessageLines(taskBoard))
        {
            builder.AppendLine($"- {line}");
        }

        builder.AppendLine();
        builder.AppendLine("## Dolly Agent Runtime");
        builder.AppendLine(_dollyAgentStateService.BuildAgentContext(VaultRootPath));

        return builder.ToString();
    }

    private string ReadActiveChartFileContext(ChartContext chartContext)
    {
        var path = Path.Combine(VaultRootPath, chartContext.ChartFolderName, $"_Chart_{chartContext.ChartId}.md");
        return File.Exists(path)
            ? File.ReadAllText(path)
            : $"No active chart file exists yet at {chartContext.ChartFolderName}/_Chart_{chartContext.ChartId}.md.";
    }

    private string ReadActivePatientPersonaContext(ChartContext chartContext)
    {
        var path = Path.Combine(VaultRootPath, chartContext.ChartFolderName, $"_Persona_{chartContext.ChartId}.md");
        return File.Exists(path)
            ? File.ReadAllText(path)
            : $"No patient persona file exists yet at {chartContext.ChartFolderName}/_Persona_{chartContext.ChartId}.md.";
    }

    private string ReadChartManagerPersonaContext()
    {
        var path = Path.Combine(GetChartManagerFolderPath(), "Manager_Profile.md");
        return File.Exists(path)
            ? File.ReadAllText(path)
            : "No Chart Manager persona file exists yet.";
    }

    private string BuildDollyOrientationContext(ChartContext chartContext)
    {
        var now = DateTimeOffset.Now;
        var timeZone = TimeZoneInfo.Local;
        var ownerName = string.IsNullOrWhiteSpace(_settings.VaultOwnerName) ? "the vault owner" : _settings.VaultOwnerName.Trim();
        var ownerRole = string.IsNullOrWhiteSpace(_settings.VaultOwnerRole) ? "user" : _settings.VaultOwnerRole.Trim();
        var activeUserName = FirstNonEmpty(_settings.ActiveUserName, _settings.ChartManagerNickname, _settings.ChartManagerName, ownerName);
        var managerNickname = FirstNonEmpty(_settings.ChartManagerNickname, "not set");

        return string.Join(
            Environment.NewLine,
            [
                $"- Agent name: {AgentDisplayName}.",
                "- Role: VitaMR front-desk health chart agent for conversation, intent mapping, task presence, and user-facing clarity.",
                $"- Current local date/time: {now:yyyy-MM-dd HH:mm:ss zzz}.",
                $"- Local time zone: {timeZone.Id} ({timeZone.DisplayName}).",
                $"- Serving: {ownerName} ({ownerRole}).",
                $"- Active user: {activeUserName} ({FirstNonEmpty(_settings.ActiveUserRole, "Chart Manager")}).",
                $"- Chart manager: {ResolveChartManagerName()} (nickname: {managerNickname}). Only the chart manager may create or delete patient chart files.",
                $"- Chart manager persona path: {GetChartManagerPersonaRelativePath()}. This is separate from any patient chart for the same person.",
                $"- Active chart label: {ExtractFirstNameForDisplay(chartContext.LocalDisplayName)}.",
                $"- Active chart id: {chartContext.ChartId}.",
                "- Location: VitaMR local Windows desktop app.",
                "- C# authority: identity validation, safety gates, file reads/writes, audit logs, sealed registry checks, final name display, and task execution.",
                "- Agent access: scrubbed user text, scrubbed recent conversation, known patient labels, sterile chart context, task board, health preferences, and agent runtime notes.",
                "- Normal blocked access: raw source files, unsupervised external API calls, direct file mutation, autonomous diagnosis, triage, prescribing, or clinical escalation.",
                "- Available external reasoning path: C# may send safe scrubbed text to the configured API for intent mapping, chart answers, wiki patch planning, and rewrite verification.",
                "- Available local paths: C# may use local privacy scrubbing, local OCR, deterministic attachment safety checks, and local fallback only when explicitly routed.",
                $"- Healthspan mode: {HealthspanModeSummary}.",
                $"- Healthspan boundary: {HealthspanModeDetail}",
                "- Temporal rule: treat relative dates from the current local date/time and keep event dates attached when reasoning over chart context."
            ]);
    }

    private IReadOnlyList<string> BuildHealthspanModeContextLines()
    {
        if (!EnableHealthspanMode ||
            HealthspanCoachingMode.Equals("Record Assistant Mode", StringComparison.OrdinalIgnoreCase))
        {
            return
            [
                "Healthspan mode is off. Dolly must stay in Record Assistant Mode.",
                "Focus on medical-record organization, retrieval, upload support, summaries, and safe chart questions.",
                "Do not provide healthspan coaching, longevity performance accountability, best-practice coaching, or Goal-Action Integrity Gap language unless the user explicitly turns Healthspan Mode on."
            ];
        }

        return HealthspanCoachingMode switch
        {
            "Healthspan Support Mode" =>
            [
                "Healthspan mode is on: Healthspan Support Mode.",
                "Dolly may offer gentle, paced healthspan support only after respecting record context and safety boundaries.",
                "Do not diagnose, prescribe, triage, order tests, or treat best-practice ideas as chart facts."
            ],
            "Longevity Performance Mode" =>
            [
                "Healthspan mode is on: Longevity Performance Mode.",
                "Direct accountability and Goal-Action Integrity Gap language are allowed when relevant to user-chosen goals.",
                "Keep coaching respectful, non-abusive, non-diagnostic, and separate from medical advice."
            ],
            "Elite / Experimental Longevity Mode" =>
            [
                "Healthspan mode is on: Elite / Experimental Longevity Mode.",
                "Rigorous tracking and cutting-edge research awareness are allowed when clearly separated from medical advice and verified chart evidence.",
                "Do not diagnose, prescribe, triage, order tests, or blur research guidance into chart facts."
            ],
            _ =>
            [
                $"Healthspan mode is on: {HealthspanCoachingMode}.",
                "Respect the selected coaching intensity while preserving all medical safety boundaries."
            ]
        };
    }

    private string ResolvePreferenceUserName(ChartContext chartContext)
    {
        var identity = _patientRegistryService.GetAllIdentities()
            .FirstOrDefault(record => string.Equals(record.ChartId, chartContext.ChartId, StringComparison.OrdinalIgnoreCase));

        if (identity is not null && IsAdultIdentity(identity, DateTime.Now))
        {
            return identity.PatientDisplayName;
        }

        return string.IsNullOrWhiteSpace(_settings.VaultOwnerName)
            ? "Vault Manager"
            : _settings.VaultOwnerName;
    }

    private static bool IsAdultIdentity(PatientIdentityRecord identity, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(identity.DateOfBirth) ||
            !DateTime.TryParse(identity.DateOfBirth, out var dob))
        {
            return false;
        }

        var age = now.Year - dob.Year;
        if (dob.Date > now.Date.AddYears(-age))
        {
            age--;
        }

        return age >= 18;
    }

    private string BuildFamilyContextTextFallback()
    {
        var patients = _patientRegistryService.GetAllIdentities()
            .Where(patient => !string.IsNullOrWhiteSpace(patient.PatientDisplayName))
            .Where(patient => Directory.Exists(Path.Combine(VaultRootPath, patient.ChartId)))
            .OrderBy(patient => patient.PatientDisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(patient => $"- {patient.PatientDisplayName}")
            .ToList();

        return patients.Count == 0
            ? "No active patient files are registered."
            : "Active patient files:\n" + string.Join(Environment.NewLine, patients);
    }

    private static string BuildGemmaUnavailableFallback(ChartContext chartContext, string localSessionContext)
    {
        var overview = ExtractMarkdownSectionText(localSessionContext, "## Human Overview", 4);

        if (overview.Count > 0)
        {
            return string.Join(Environment.NewLine + Environment.NewLine, overview);
        }

        return $"I have local context loaded for {ExtractFirstNameForDisplay(chartContext.LocalDisplayName)}, but the local fallback did not return an answer in time. The background task is still running.";
    }

    private IEnumerable<string> BuildConcurrentRosterReply()
    {
        var activePatients = _patientRegistryService.GetAllIdentities()
            .Where(patient => !string.IsNullOrWhiteSpace(patient.PatientDisplayName))
            .Where(patient => Directory.Exists(Path.Combine(VaultRootPath, patient.ChartId)))
            .OrderBy(patient => patient.PatientDisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (activePatients.Count == 0)
        {
            yield return "I do not see any active patient files in the sealed registry right now.";
            yield break;
        }

        yield return $"I found {activePatients.Count} active patient file(s) in the sealed registry:";

        foreach (var patient in activePatients)
        {
            yield return $"- {patient.PatientDisplayName}";
        }

        yield return "The background task is still running separately; I did not read any raw files to answer this.";
    }

    private string EnsureConcurrentWorkingSummaryContext(ChartContext chartContext)
    {
        if (!string.IsNullOrWhiteSpace(_lastGemmaWorkingSummaryContext) &&
            !IsWorkingSummaryGemmaFailure(_lastGemmaWorkingSummaryContext))
        {
            return _lastGemmaWorkingSummaryContext;
        }

        var gemmaContextPath = Path.Combine(VaultRootPath, chartContext.ChartFolderName, "wiki", "Dolly_Local_Context.md");

        if (File.Exists(gemmaContextPath))
        {
            var fileContext = File.ReadAllText(gemmaContextPath);

            if (!string.IsNullOrWhiteSpace(fileContext))
            {
                _lastGemmaWorkingSummaryContext = fileContext;
                return fileContext;
            }
        }

        var legacyContextPath = Path.Combine(VaultRootPath, chartContext.ChartFolderName, "wiki", "Dolly_Gemma_Context.md");

        if (File.Exists(legacyContextPath))
        {
            var fileContext = File.ReadAllText(legacyContextPath);

            if (!string.IsNullOrWhiteSpace(fileContext))
            {
                _lastGemmaWorkingSummaryContext = fileContext;
                return fileContext;
            }
        }

        var summaryPath = Path.Combine(VaultRootPath, chartContext.ChartFolderName, "wiki", "Dolly_Working_Summary.md");

        if (File.Exists(summaryPath))
        {
            return File.ReadAllText(summaryPath);
        }

        _lastDollyWorkingSummaryResult = _dollyWorkingSummaryService.RefreshSummary(VaultRootPath, chartContext);

        return _lastDollyWorkingSummaryResult.WasWritten && File.Exists(_lastDollyWorkingSummaryResult.SummaryPath)
            ? File.ReadAllText(_lastDollyWorkingSummaryResult.SummaryPath)
            : string.Empty;
    }

    private static string BuildDeterministicConcurrentSummaryAnswer(string userText, string workingSummaryContext)
    {
        if (workingSummaryContext.Contains("## Human Overview", StringComparison.OrdinalIgnoreCase))
        {
            var overview = ExtractMarkdownSectionText(workingSummaryContext, "## Human Overview", 5);
            var openLoops = ExtractMarkdownSectionBullets(workingSummaryContext, "## Open Loops", 3);

            if (openLoops.Count > 0)
            {
                overview.Add($"Open items to keep in view: {JoinHumanList(openLoops.Select(CleanPacketFact).ToList())}.");
            }

            return overview.Count == 0
                ? "I have Dolly's local context loaded, but it does not contain enough clean detail to answer that yet."
                : string.Join(Environment.NewLine + Environment.NewLine, overview.Select(CleanPacketFact));
        }

        if (workingSummaryContext.Contains("KNOWN HIGH-SIGNAL FACTS:", StringComparison.OrdinalIgnoreCase))
        {
            return BuildDeterministicPacketAnswer(workingSummaryContext);
        }

        var diagnoses = ExtractMarkdownTableRows(workingSummaryContext, "## Diagnoses And Conditions", 5);
        var meds = ExtractMarkdownTableRows(workingSummaryContext, "## Medications", 4);
        var gaps = ExtractMarkdownTableRows(workingSummaryContext, "## Open Care Gaps", 4);
        var timeline = ExtractMarkdownTableRows(workingSummaryContext, "## Recent Timeline", 4);
        var lines = new List<string>();

        lines.Add("From Bruce's sterile working summary, the high-level picture is:");
        lines.AddRange(diagnoses.Select(row => $"- Condition: {row}"));
        lines.AddRange(meds.Select(row => $"- Medication: {row}"));
        lines.AddRange(gaps.Take(3).Select(row => $"- Open item: {row}"));

        if (timeline.Count > 0)
        {
            lines.Add($"Most recent chart item: {timeline[0]}");
        }

        lines.Add("This is summary-level context only while the background task is running; new uploaded data may change the answer after ingest finishes.");

        return string.Join(Environment.NewLine, lines.Where(line => !string.IsNullOrWhiteSpace(line)).Take(12));
    }

    private static string BuildDeterministicPacketAnswer(string workingSummaryContext)
    {
        var facts = ExtractPacketBullets(workingSummaryContext, "KNOWN HIGH-SIGNAL FACTS:", 6);
        var openLoops = ExtractPacketBullets(workingSummaryContext, "OPEN LOOPS:", 4);
        var timeline = ExtractPacketBullets(workingSummaryContext, "RECENT TIMELINE:", 3);
        var conditions = facts
            .Where(fact => !fact.Contains("beta-blocker", StringComparison.OrdinalIgnoreCase))
            .Select(CleanPacketFact)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToList();
        var medications = facts
            .Where(fact => fact.Contains("beta-blocker", StringComparison.OrdinalIgnoreCase) ||
                           fact.Contains("metoprolol", StringComparison.OrdinalIgnoreCase) ||
                           fact.Contains("testosterone", StringComparison.OrdinalIgnoreCase))
            .Select(CleanPacketFact)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(2)
            .ToList();
        var openItems = openLoops
            .Select(CleanPacketFact)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToList();
        var latest = timeline.Select(CleanPacketFact).FirstOrDefault();
        var paragraphs = new List<string>
        {
            conditions.Count == 0
                ? "From Bruce's working summary, I have some chart context, but not enough clean detail to give a strong overview while the upload is still running."
                : $"From Bruce's working summary, the main picture is {JoinHumanList(conditions)}."
        };

        if (medications.Count > 0)
        {
            paragraphs.Add($"Medication context in the summary includes {JoinHumanList(medications)}.");
        }

        if (openItems.Count > 0)
        {
            paragraphs.Add($"The open items I would keep in view are {JoinHumanList(openItems)}.");
        }

        if (!string.IsNullOrWhiteSpace(latest))
        {
            paragraphs.Add($"The latest timeline item I see is: {latest}.");
        }

        return string.Join(Environment.NewLine + Environment.NewLine, paragraphs.Where(line => !string.IsNullOrWhiteSpace(line)));
    }

    private static string CleanPacketFact(string value)
    {
        var cleaned = value;
        cleaned = Regex.Replace(cleaned, @"\s+-\s+encounters/.*$", string.Empty, RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"^GAP-\d+\s+-\s+", string.Empty, RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"\s+-\s+(active|open|high|moderate|low|routine|Unknown)\b", string.Empty, RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim(' ', '.', '-');
        return cleaned;
    }

    private static string JoinHumanList(IReadOnlyList<string> values)
    {
        var cleanValues = values.Where(value => !string.IsNullOrWhiteSpace(value)).ToList();

        return cleanValues.Count switch
        {
            0 => string.Empty,
            1 => cleanValues[0],
            2 => $"{cleanValues[0]} and {cleanValues[1]}",
            _ => string.Join(", ", cleanValues.Take(cleanValues.Count - 1)) + $", and {cleanValues[^1]}"
        };
    }

    private static bool LooksLikeInternalPacketAnswer(string value)
    {
        return value.Contains("encounters/", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("GAP-", StringComparison.OrdinalIgnoreCase) ||
               value.Contains(" - active - ", StringComparison.OrdinalIgnoreCase) ||
               value.Contains(" - high - ", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> ExtractPacketBullets(string text, string heading, int maxRows)
    {
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var start = Array.FindIndex(lines, line => line.Trim().Equals(heading, StringComparison.OrdinalIgnoreCase));

        if (start < 0)
        {
            return [];
        }

        var rows = new List<string>();

        for (var index = start + 1; index < lines.Length; index++)
        {
            var line = lines[index].Trim();

            if (line.EndsWith(":", StringComparison.Ordinal) && !line.StartsWith("- ", StringComparison.Ordinal))
            {
                break;
            }

            if (line.StartsWith("- ", StringComparison.Ordinal))
            {
                rows.Add(line[2..].Trim());
            }

            if (rows.Count >= maxRows)
            {
                break;
            }
        }

        return rows;
    }

    private static IReadOnlyList<string> ExtractMarkdownTableRows(string markdown, string heading, int maxRows)
    {
        var lines = markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var start = Array.FindIndex(lines, line => line.Trim().Equals(heading, StringComparison.OrdinalIgnoreCase));

        if (start < 0)
        {
            return [];
        }

        var rows = new List<string>();

        for (var index = start + 1; index < lines.Length; index++)
        {
            var line = lines[index].Trim();

            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                break;
            }

            if (!line.StartsWith('|') ||
                line.Contains("---", StringComparison.Ordinal) ||
                line.Contains(" Gap ID ", StringComparison.OrdinalIgnoreCase) ||
                line.Contains(" Title ", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var cells = line.Trim('|')
                .Split('|')
                .Select(cell => StripWikiSyntax(cell.Trim()))
                .Where(cell => !string.IsNullOrWhiteSpace(cell))
                .Take(4)
                .ToList();

            if (cells.Count > 0)
            {
                rows.Add(string.Join(" - ", cells));
            }

            if (rows.Count >= maxRows)
            {
                break;
            }
        }

        return rows;
    }

    private static List<string> ExtractMarkdownSectionText(string markdown, string heading, int maxLines)
    {
        var lines = markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var start = Array.FindIndex(lines, line => line.Trim().Equals(heading, StringComparison.OrdinalIgnoreCase));

        if (start < 0)
        {
            return [];
        }

        return lines
            .Skip(start + 1)
            .TakeWhile(line => !line.TrimStart().StartsWith("## ", StringComparison.Ordinal))
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith("- ", StringComparison.Ordinal))
            .Take(maxLines)
            .ToList();
    }

    private static List<string> ExtractMarkdownSectionBullets(string markdown, string heading, int maxLines)
    {
        var lines = markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var start = Array.FindIndex(lines, line => line.Trim().Equals(heading, StringComparison.OrdinalIgnoreCase));

        if (start < 0)
        {
            return [];
        }

        return lines
            .Skip(start + 1)
            .TakeWhile(line => !line.TrimStart().StartsWith("## ", StringComparison.Ordinal))
            .Select(line => line.Trim())
            .Where(line => line.StartsWith("- ", StringComparison.Ordinal))
            .Select(line => line[2..].Trim())
            .Take(maxLines)
            .ToList();
    }

    private static string StripWikiSyntax(string value)
    {
        return value
            .Replace("[[", string.Empty, StringComparison.Ordinal)
            .Replace("]]", string.Empty, StringComparison.Ordinal)
            .Replace("**", string.Empty, StringComparison.Ordinal)
            .Trim();
    }

    private static bool IsSummaryChatFailure(string value)
    {
        return string.IsNullOrWhiteSpace(value) ||
               value.StartsWith("LOCAL_GEMMA_SUMMARY_CHAT_", StringComparison.OrdinalIgnoreCase);
    }

    private static string Shorten(string value, int maxLength)
    {
        var normalized = value.Replace("\r\n", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal).Trim();
        return normalized.Length <= maxLength
            ? normalized
            : normalized[..maxLength].TrimEnd() + "...";
    }

    private static IEnumerable<string> BuildTaskQueueMessageLines(string queuePath)
    {
        if (!File.Exists(queuePath))
        {
            yield return "No scheduler queue exists yet.";
            yield break;
        }

        yield return "Current Scheduler Queue:";

        foreach (var line in File.ReadAllLines(queuePath).Where(line => line.TrimStart().StartsWith("| TASK-", StringComparison.OrdinalIgnoreCase)))
        {
            yield return line;
        }
    }

    private static bool IsDreamRunnerRequest(string value)
    {
        var normalized = NormalizeConfirmation(value);

        return normalized.Contains("dream runner", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("dream review", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("run audit", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("audit chart", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSpecialtyWeaverRequest(string value)
    {
        var normalized = NormalizeConfirmation(value);

        return normalized.Contains("specialty weaver", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("organize specialties", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("update specialty", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("specialty files", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSymptomScanRequest(string value)
    {
        var normalized = NormalizeConfirmation(value);

        return normalized.Contains("run symptom scan", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("symptom pattern scan", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("check symptom patterns", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("run pattern linker", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsShowQueueRequest(string value)
    {
        var normalized = NormalizeConfirmation(value);

        return normalized.Equals("show queue", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("show scheduler queue", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("task queue", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTaskBoardRequest(string value)
    {
        var normalized = NormalizeConfirmation(value);

        return normalized.Contains("task board", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("dolly tasks", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("what is happening", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("what's happening", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("what are you doing", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDataHunterBasicStartRequest(string value)
    {
        var normalized = NormalizeConfirmation(value);

        return normalized.Contains("data hunter", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("data hunting", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("hunter basic", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("next quest", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("quest card", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("progress bar", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("personal data map", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("basic interview", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsWorkingSummaryRefreshRequest(string value)
    {
        var normalized = NormalizeConfirmation(value);

        return normalized.Contains("working summary", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("refresh summary", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("refresh dolly context", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("reload patient context", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDrugInteractionScanRequest(string value)
    {
        var normalized = NormalizeConfirmation(value);

        return normalized.Contains("drug interaction", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("medication interaction", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("scan meds", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("scan medications", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPreVisitBriefRequest(string value)
    {
        var normalized = NormalizeConfirmation(value);

        return normalized.Contains("pre visit brief", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("pre-visit brief", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("visit brief", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("visit prep", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeChartId(string value)
    {
        var trimmed = string.IsNullOrWhiteSpace(value)
            ? "VITA-0001"
            : value.Trim();

        var invalidCharacters = Path.GetInvalidFileNameChars();
        var safeCharacters = trimmed.Select(character =>
        {
            if (char.IsWhiteSpace(character))
            {
                return '_';
            }

            return invalidCharacters.Contains(character)
                ? '_'
                : character;
        });

        var safeName = new string(safeCharacters.ToArray()).Trim('_').ToUpperInvariant();
        return string.IsNullOrWhiteSpace(safeName)
            ? "VITA-0001"
            : safeName;
    }

    private static string NormalizeProvenanceField(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "Unknown"
            : value.Trim();
    }

    private static string NormalizeProviderDisplayName(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "Gemini"
            : value.Trim().ToLowerInvariant() switch
            {
                "google" or "gemini" => "Gemini",
                "openai" or "open ai" => "OpenAI",
                "anthropic" or "claude" => "Anthropic",
                "xai" or "x.ai" or "grok" => "xAI",
                "local" or "local model" => "Local",
                _ => "Gemini"
            };
    }

    private static bool IsApiEligibleScrubbedPayload(SecondPassScrubResult result)
    {
        return result.Status.Equals("FINAL_SCRUBBED_PAYLOAD_SAVED", StringComparison.OrdinalIgnoreCase) ||
               result.Status.Equals("FINAL_SCRUBBED_PAYLOAD_REWRITE_VALIDATED", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record PatientNameResolution(
        bool CanProceed,
        ChartContext? ChartContext,
        string Message,
        OmniboxPreflightResult? Preflight,
        bool ShouldConfirmTypedCapture);

    private sealed record PendingTypedCapture(
        string SourceText,
        ChartContext ChartContext);

    private sealed record PendingCareGapResolution(
        ChartContext ChartContext,
        string CareGapsPath,
        string GapId,
        string Title,
        string NewStatus,
        string Reason);

    private sealed record PendingVaccineUpdate(
        ChartContext ChartContext,
        string VaccinesPath,
        string TimelinePath,
        IReadOnlyList<VaccineRecord> Records,
        string SourceHint);

    private sealed record VaccineRecord(
        string VaccineName,
        string DateGiven,
        string DoseOrSeries,
        string LocationOrProvider,
        string Notes);

    private sealed record VaccineTableRow(
        string DateGiven,
        string Vaccine,
        string DoseOrSeries,
        string LocationOrProvider,
        string Source,
        string Notes);

    private sealed record PendingReminderUpdate(
        string RemindersPath,
        IReadOnlyList<UserReminderRecord> Records);

    private sealed record PendingPhotoUpdate(ChartContext ChartContext);

    private sealed class DataHunterPersonalDataMap
    {
        public string ChartId { get; set; } = string.Empty;

        public string PatientDisplayName { get; set; } = string.Empty;

        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;

        public List<DataHunterPersonalDataAnswer> Answers { get; set; } = [];

        public List<DataHunterCategoryReward> CategoryRewards { get; set; } = [];

        public List<DataHunterMasterQuest> MasterQuests { get; set; } = [];
    }

    private sealed class DataHunterPersonalDataAnswer
    {
        public string Key { get; set; } = string.Empty;

        public string Question { get; set; } = string.Empty;

        public string Answer { get; set; } = string.Empty;

        public string Status { get; set; } = DataHunterAnswerStatusAnswered;

        public string Category { get; set; } = string.Empty;

        public int XpAwarded { get; set; }

        public string Source { get; set; } = "user_provided_context";

        public string EvidenceStatus { get; set; } = "not_verified_medical_evidence";

        public DateTimeOffset AnsweredAt { get; set; } = DateTimeOffset.Now;

        public string SourceRoute { get; set; } = string.Empty;
    }

    private sealed class DataHunterMasterQuest
    {
        public string QuestId { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string Target { get; set; } = string.Empty;

        public string Status { get; set; } = DataHunterMasterQuestStatusPendingEvidence;

        public string EvidenceHint { get; set; } = string.Empty;

        public string SourceClueSummary { get; set; } = string.Empty;

        public string AcceptedEvidenceSummary { get; set; } = string.Empty;

        public string AcceptedEvidenceSourceId { get; set; } = string.Empty;

        public DateTimeOffset? AcceptedAt { get; set; }
    }

    private sealed class DataHunterCategoryReward
    {
        public string Category { get; set; } = string.Empty;

        public int XpAwarded { get; set; }

        public DateTimeOffset AwardedAt { get; set; } = DateTimeOffset.Now;
    }

    public sealed record AdminChartRowViewModel(
        string PatientDisplayName,
        string ChartId,
        string DateOfBirth,
        string RelationshipNotes,
        string Status,
        string CleanupHint,
        string FocusButtonLabel);

    public sealed class EvaluationQuestionViewModel : ObservableObject
    {
        private string _answer = "Not checked";
        private string _scoreText = string.Empty;

        public EvaluationQuestionViewModel(string prompt)
        {
            Prompt = prompt;
        }

        public string Prompt { get; }

        public IReadOnlyList<string> AnswerOptions { get; } =
        [
            "Not checked",
            "Yes",
            "No",
            "Partly",
            "N/A"
        ];

        public string Answer
        {
            get => _answer;
            set => SetProperty(ref _answer, string.IsNullOrWhiteSpace(value) ? "Not checked" : value.Trim());
        }

        public string ScoreText
        {
            get => _scoreText;
            set => SetProperty(ref _scoreText, value ?? string.Empty);
        }
    }

    public sealed class PhoneInboxItemViewModel : ObservableObject
    {
        private string _status;
        private string _masterHuntHint = string.Empty;

        public PhoneInboxItemViewModel(
            string localId,
            string createdAt,
            string chartId,
            string patientDisplayName,
            string kind,
            string note,
            string fileName,
            string status,
            string sourcePath)
        {
            LocalId = string.IsNullOrWhiteSpace(localId) ? Guid.NewGuid().ToString("N") : localId.Trim();
            CreatedAt = createdAt;
            ChartId = chartId;
            PatientDisplayName = patientDisplayName;
            Kind = kind;
            Note = note;
            FileName = fileName;
            SourcePath = sourcePath;
            _status = status;
        }

        public string LocalId { get; }

        public string CreatedAt { get; }

        public string ChartId { get; }

        public string PatientDisplayName { get; }

        public string Kind { get; }

        public string Note { get; }

        public string FileName { get; }

        public string SourcePath { get; }

        public string Title => string.IsNullOrWhiteSpace(FileName)
            ? $"{Kind} from {PatientDisplayName}"
            : $"{Kind}: {FileName}";

        public string Detail => $"{PatientDisplayName} - {CreatedAt}";

        public string SourceDetail => string.IsNullOrWhiteSpace(SourcePath) ? "Phone note" : SourcePath;

        public string ReviewParameter => $"review|{LocalId}";

        public string SummarizeParameter => $"summarize|{LocalId}";

        public string SaveParameter => $"save|{LocalId}";

        public string AddParameter => $"add|{LocalId}";

        public string AcceptEvidenceParameter => $"accept|{LocalId}";

        public string ArchiveParameter => $"archive|{LocalId}";

        public string Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }

        public string MasterHuntHint
        {
            get => _masterHuntHint;
            set => SetProperty(ref _masterHuntHint, value ?? string.Empty);
        }
    }

    public sealed class PersonalVaultItemViewModel
    {
        public PersonalVaultItemViewModel(
            string lane,
            string kind,
            string note,
            string topicHint,
            string captureType,
            string createdAt,
            string receivedAt,
            string deviceName)
        {
            Lane = string.IsNullOrWhiteSpace(lane) ? "personal" : lane.Trim();
            Kind = string.IsNullOrWhiteSpace(kind)
                ? Lane.ToLowerInvariant() switch
                {
                    "pets" => "pet_note",
                    "lockbox" => "lockbox_note",
                    _ => "personal_text"
                }
                : kind.Trim();
            Note = note;
            TopicHint = string.IsNullOrWhiteSpace(topicHint) ? "Inbox" : topicHint.Trim();
            CaptureType = string.IsNullOrWhiteSpace(captureType) ? "note" : captureType.Trim();
            CreatedAt = string.IsNullOrWhiteSpace(createdAt) ? "unknown time" : createdAt.Trim();
            ReceivedAt = string.IsNullOrWhiteSpace(receivedAt) ? string.Empty : receivedAt.Trim();
            DeviceName = string.IsNullOrWhiteSpace(deviceName) ? "Phone companion" : deviceName.Trim();
        }

        public string Lane { get; }

        public string Kind { get; }

        public string Note { get; }

        public string TopicHint { get; }

        public string CaptureType { get; }

        public string CreatedAt { get; }

        public string ReceivedAt { get; }

        public string DeviceName { get; }

        public string Title => Lane.ToLowerInvariant() switch
        {
            "pets" => "Pet note",
            "lockbox" => "Lockbox note",
            _ when IsPersonalSessionKind(Kind) => "Phone session snapshot",
            _ => "Personal note"
        };

        public string Detail => $"{DeviceName} - {CreatedAt}";

        public string TopicDetail => $"{TopicHint} - {CaptureType.Replace('_', ' ')}";

        public string Boundary => Lane.ToLowerInvariant() switch
        {
            "pets" => "Pets lane: animal/pet context, not a human medical chart.",
            "lockbox" => "Lockbox lane: sensitive local/trusted-host note; excluded from Gemini and wiki weaving.",
            _ when IsPersonalSessionKind(Kind) => "Personal lane: phone sync history; excluded from derived wiki and timeline pages.",
            _ => "Personal lane: separate from medical chart evidence."
        };
    }

    private sealed record PersonalVaultTopic(
        string TopicHint,
        string TopicSlug);

    private sealed record PersonalVaultCapture(
        string CaptureId,
        string Lane,
        string Kind,
        string Note,
        string TopicHint,
        string TopicSlug,
        string CaptureType,
        string CreatedAt,
        string ReceivedAt,
        string DeviceId,
        string DeviceName);

    private sealed class YouTubeVideoContext
    {
        public string VideoId { get; set; } = string.Empty;

        public string Slug { get; set; } = string.Empty;

        public string WorkingTitle { get; set; } = string.Empty;

        public string Status { get; set; } = "active";

        public string CreatedAt { get; set; } = string.Empty;

        public string LastTouchedAt { get; set; } = string.Empty;

        public string ParentProject { get; set; } = YouTubeProjectSlug;

        public string MicroTransformation { get; set; } = string.Empty;
    }

    public sealed class VitaMasteryQuestRowViewModel
    {
        public VitaMasteryQuestRowViewModel(VitaMasteryQuestResult result)
        {
            Title = result.Quest.Title;
            Status = result.Status.ToString();
            EvidenceHint = result.Quest.EvidenceHint;
            Points = $"{result.EarnedPoints}/{result.Quest.Points}";
            Source = result.Quest.SourceName;
            EvidenceSummary = string.IsNullOrWhiteSpace(result.EvidenceSummary)
                ? "Evidence needed"
                : result.EvidenceSummary;
        }

        public string Title { get; }

        public string Status { get; }

        public string EvidenceHint { get; }

        public string Points { get; }

        public string Source { get; }

        public string EvidenceSummary { get; }
    }

    public sealed record DataHunterDebugRowViewModel(
        string Label,
        string Value,
        string Detail);

    private sealed record UserReminderRecord(
        string ReminderText,
        string DueDate,
        string RelatedChart,
        string Priority,
        string Notes);

    private sealed record ChartSwitchResolution(
        bool WasResolved,
        ChartContext ChartContext,
        IReadOnlyList<string> MessageLines)
    {
        public static ChartSwitchResolution Resolved(ChartContext chartContext)
        {
            return new ChartSwitchResolution(true, chartContext, []);
        }

        public static ChartSwitchResolution Clarify(IReadOnlyList<string> messageLines)
        {
            return new ChartSwitchResolution(false, new ChartContext(string.Empty, string.Empty), messageLines);
        }
    }

    private sealed record CareGapRow(List<string> Columns)
    {
        public string GapId => Columns.Count > 0 ? Columns[0] : string.Empty;

        public string Title => Columns.Count > 1 ? Columns[1] : string.Empty;

        public string Status => Columns.Count > 3 ? Columns[3] : string.Empty;
    }

    private sealed record PatientIntakeResolution(bool CanProceed, ChartContext? ChartContext, string SourceText);

    private sealed record PendingPatientDelete(PatientIdentityRecord Patient);

    private sealed class PendingPatientIntake
    {
        private readonly List<string> _candidateNames = [];
        private readonly List<string> _sourceTextLines = [];

        public string FullName { get; private set; } = string.Empty;

        public string DateOfBirth { get; private set; } = string.Empty;

        public string Address { get; private set; } = string.Empty;

        public string PhoneNumber { get; private set; } = string.Empty;

        public string Email { get; private set; } = string.Empty;

        public string SocialSecurityNumber { get; private set; } = string.Empty;

        public string RelationshipNotes { get; private set; } = string.Empty;

        public string PhotoPath { get; private set; } = string.Empty;

        public bool IdentityWasConfirmed { get; private set; }

        public bool AwaitingIdentityConfirmation { get; set; }

        public bool AwaitingCreateConfirmation { get; set; }

        public IReadOnlyList<string> CandidateNames => _candidateNames;

        public bool ShouldAskIdentityConfirmation =>
            !IdentityWasConfirmed &&
            _candidateNames.Count > 1;

        public bool HasAnyValue =>
            !string.IsNullOrWhiteSpace(FullName) ||
            !string.IsNullOrWhiteSpace(DateOfBirth) ||
            !string.IsNullOrWhiteSpace(Address) ||
            !string.IsNullOrWhiteSpace(PhoneNumber);

        public void RememberSourceText(string value)
        {
            if (!string.IsNullOrWhiteSpace(value) &&
                !NormalizeConfirmation(value).Equals("create chart", StringComparison.OrdinalIgnoreCase))
            {
                _sourceTextLines.Add(value.Trim());
            }
        }

        public void Merge(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            value = NormalizeIntakeText(value);

            FullName = PreferBetterName(FullName, PickFirst(ReadField(value, "Full Name|Patient Name|Name"), ExtractPatientName(value)));
            DateOfBirth = Coalesce(DateOfBirth, ReadField(value, "DOB|Date of Birth|Birthdate"));
            DateOfBirth = Coalesce(DateOfBirth, ReadDateNearLabel(value, "DOB|Date of Birth|Birthdate"));
            Address = Coalesce(Address, ReadField(value, "Address"));
            PhoneNumber = Coalesce(PhoneNumber, ReadField(value, "Phone|Phone Number|Cell|Mobile"));
            PhoneNumber = Coalesce(PhoneNumber, ReadPhone(value));
            Email = Coalesce(Email, ReadField(value, "Email"));
            Email = Coalesce(Email, ReadEmail(value));
            SocialSecurityNumber = Coalesce(SocialSecurityNumber, ReadField(value, "SSN|Social Security"));
            RelationshipNotes = Coalesce(RelationshipNotes, ReadField(value, "Relation|Relationship"));
            PhotoPath = Coalesce(PhotoPath, ReadField(value, "Photo|Photo Path"));
        }

        public void Merge(PatientIntakeExtractionResult extraction)
        {
            if (extraction is null)
            {
                return;
            }

            FullName = PreferBetterName(FullName, extraction.FullName);
            DateOfBirth = Coalesce(DateOfBirth, extraction.DateOfBirth);
            Address = Coalesce(Address, extraction.Address);
            PhoneNumber = Coalesce(PhoneNumber, extraction.PhoneNumber);
            Email = Coalesce(Email, extraction.Email);
            SocialSecurityNumber = Coalesce(SocialSecurityNumber, extraction.SocialSecurityNumber);
            RelationshipNotes = Coalesce(RelationshipNotes, extraction.RelationshipNotes);
            PhotoPath = Coalesce(PhotoPath, extraction.PhotoPath);
        }

        public void MergeCorrections(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            value = NormalizeIntakeText(value);

            var name = PickFirst(ReadField(value, "Full Name|Patient Name|Name"), ExtractPatientName(value));
            if (!string.IsNullOrWhiteSpace(name) && !LooksLikePlaceholderPatientName(name))
            {
                FullName = name.Trim();
                IdentityWasConfirmed = true;
            }

            DateOfBirth = ReplaceIfPresent(DateOfBirth, PickFirst(ReadField(value, "DOB|Date of Birth|Birthdate"), ReadDateNearLabel(value, "DOB|Date of Birth|Birthdate")));
            Address = ReplaceIfPresent(Address, ReadField(value, "Address"));
            PhoneNumber = ReplaceIfPresent(PhoneNumber, PickFirst(ReadField(value, "Phone|Phone Number|Cell|Mobile"), ReadPhone(value)));
            Email = ReplaceIfPresent(Email, PickFirst(ReadField(value, "Email"), ReadEmail(value)));
            SocialSecurityNumber = ReplaceIfPresent(SocialSecurityNumber, ReadField(value, "SSN|Social Security"));
            RelationshipNotes = ReplaceIfPresent(RelationshipNotes, ReadField(value, "Relation|Relationship"));
            PhotoPath = ReplaceIfPresent(PhotoPath, ReadField(value, "Photo|Photo Path"));
        }

        public void MergeCandidateNames(IEnumerable<string> names)
        {
            foreach (var candidate in names.Select(NormalizePersonName).Where(name => !string.IsNullOrWhiteSpace(name)))
            {
                if (_candidateNames.Any(existing => existing.Equals(candidate, StringComparison.OrdinalIgnoreCase)) ||
                    LooksLikePlaceholderPatientName(candidate))
                {
                    continue;
                }

                _candidateNames.Add(candidate);
            }

            if (_candidateNames.Count == 1 &&
                string.IsNullOrWhiteSpace(FullName))
            {
                FullName = _candidateNames[0];
            }
        }

        public bool TryConfirmCandidate(string userText)
        {
            var normalizedReply = NormalizePersonName(userText);
            if (string.IsNullOrWhiteSpace(normalizedReply))
            {
                return false;
            }

            var match = _candidateNames.FirstOrDefault(candidate =>
                candidate.Equals(normalizedReply, StringComparison.OrdinalIgnoreCase) ||
                candidate.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Equals(normalizedReply, StringComparison.OrdinalIgnoreCase) == true ||
                normalizedReply.Contains(candidate, StringComparison.OrdinalIgnoreCase));

            if (string.IsNullOrWhiteSpace(match))
            {
                return false;
            }

            FullName = match;
            IdentityWasConfirmed = true;
            AwaitingIdentityConfirmation = false;
            return true;
        }

        public IReadOnlyList<string> GetMissingRequiredFields()
        {
            var missing = new List<string>();

            if (string.IsNullOrWhiteSpace(FullName))
            {
                missing.Add("full name");
            }

            if (string.IsNullOrWhiteSpace(DateOfBirth))
            {
                missing.Add("DOB");
            }

            if (string.IsNullOrWhiteSpace(Address))
            {
                missing.Add("address");
            }

            if (string.IsNullOrWhiteSpace(PhoneNumber))
            {
                missing.Add("phone number");
            }

            return missing;
        }

        public string FormatCapturedFields()
        {
            var fields = new List<string>();
            AddIfPresent(fields, "name", FullName);
            AddIfPresent(fields, "DOB", DateOfBirth);
            AddIfPresent(fields, "address", Address);
            AddIfPresent(fields, "phone", PhoneNumber);
            AddIfPresent(fields, "email", Email);
            AddIfPresent(fields, "relationship", RelationshipNotes);
            return string.Join("; ", fields);
        }

        public string BuildSummary()
        {
            return
                $"Patient: {FullName}{Environment.NewLine}" +
                $"DOB: {DateOfBirth}{Environment.NewLine}" +
                $"Address: {Address}{Environment.NewLine}" +
                $"Phone: {PhoneNumber}{Environment.NewLine}" +
                $"Email: {Email}{Environment.NewLine}" +
                $"Relationship: {RelationshipNotes}";
        }

        public string BuildSourceText()
        {
            var sourceText = string.Join(
                $"{Environment.NewLine}{Environment.NewLine}",
                _sourceTextLines.Where(line => !string.IsNullOrWhiteSpace(line)).Distinct(StringComparer.OrdinalIgnoreCase));

            return string.IsNullOrWhiteSpace(sourceText)
                ? BuildSummary()
                : $"{sourceText}{Environment.NewLine}{Environment.NewLine}{BuildSummary()}";
        }

        private static void AddIfPresent(ICollection<string> fields, string label, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                fields.Add($"{label}: {value}");
            }
        }

        private static string Coalesce(string current, string candidate)
        {
            return string.IsNullOrWhiteSpace(current) && !string.IsNullOrWhiteSpace(candidate)
                ? candidate.Trim()
                : current;
        }

        private static string ReplaceIfPresent(string current, string candidate)
        {
            return string.IsNullOrWhiteSpace(candidate)
                ? current
                : candidate.Trim();
        }

        private static string PickFirst(string primary, string fallback)
        {
            return !string.IsNullOrWhiteSpace(primary) ? primary.Trim() : fallback.Trim();
        }

        private static string PreferBetterName(string current, string candidate)
        {
            candidate = string.IsNullOrWhiteSpace(candidate) ? string.Empty : candidate.Trim();

            if (string.IsNullOrWhiteSpace(candidate) ||
                LooksLikePlaceholderPatientName(candidate))
            {
                return current;
            }

            if (string.IsNullOrWhiteSpace(current) ||
                LooksLikePlaceholderPatientName(current) ||
                current.Contains('.', StringComparison.Ordinal) ||
                current.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length > 3)
            {
                return candidate;
            }

            return current;
        }

        private static string NormalizePersonName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var cleaned = Regex.Replace(value, @"\b(?:chart|patient|pt|person|for|is|this|the|it|create|please)\b", " ", RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(cleaned, @"[^A-Za-z.'\-\s]", " ");
            cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();
            return cleaned;
        }

        private static string ReadField(string value, string labelPattern)
        {
            var match = Regex.Match(
                value,
                $@"(?ims)(?:^|\s)(?:{labelPattern})\s*:\s*(?<value>.*?)(?=\s+(?:Full Name|Patient Name|Name|DOB|Date of Birth|Birthdate|Age|Address|Date of Service|Phone|Phone Number|Cell|Mobile|Email|SSN|Social Security|Relation|Relationship|Photo|Photo Path)\s*:|$)",
                RegexOptions.IgnoreCase);

            return match.Success ? match.Groups["value"].Value.Trim() : string.Empty;
        }

        private static string NormalizeIntakeText(string value)
        {
            var normalized = value.Replace("\r\n", " ", StringComparison.Ordinal)
                .Replace('\n', ' ')
                .Replace('\r', ' ');
            normalized = Regex.Replace(normalized, @"[*_`#>\[\]]", string.Empty);
            normalized = Regex.Replace(normalized, @"\s*;\s*", " ");
            normalized = Regex.Replace(normalized, @"\s{2,}", " ");
            return normalized.Trim();
        }

        private static string ReadDateNearLabel(string value, string labelPattern)
        {
            var match = Regex.Match(
                value,
                $@"(?:{labelPattern})\D{{0,12}}(?<value>\d{{1,2}}[/-]\d{{1,2}}[/-]\d{{2,4}}|\d{{4}}-\d{{2}}-\d{{2}})",
                RegexOptions.IgnoreCase);

            return match.Success ? match.Groups["value"].Value.Trim() : string.Empty;
        }

        private static string ReadPhone(string value)
        {
            var match = Regex.Match(value, @"\b(?:\+?1[-.\s]?)?(?:\(?\d{3}\)?[-.\s]?)\d{3}[-.\s]?\d{4}\b");
            return match.Success ? match.Value.Trim() : string.Empty;
        }

        private static string ReadEmail(string value)
        {
            var match = Regex.Match(value, @"\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b", RegexOptions.IgnoreCase);
            return match.Success ? match.Value.Trim() : string.Empty;
        }
    }
}

