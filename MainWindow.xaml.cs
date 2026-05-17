using System.Windows;
using VitaMR.Services;
using VitaMR.ViewModels;

namespace VitaMR;

public partial class MainWindow : Window
{
    private readonly LocalPythonSidecarManager _sidecarManager = new();
    private readonly MobileApiHost _mobileApiHost;

    public MainWindow()
    {
        InitializeComponent();

        var testVaultPath = System.IO.Path.Combine(
            AppContext.BaseDirectory,
            "_TestVault");
        var promptCacheService = new PromptCacheService();
        var settingsService = new JsonSettingsService();
        var appSettings = settingsService.Load();
        var patientRegistryService = new JsonPatientRegistryService();
        var geminiApiKeyStore = new WindowsDpapiGeminiApiKeyStore();
        var geminiClassifierService = new GeminiDocumentClassifierService(geminiApiKeyStore, promptCacheService);
        var geminiExtractionService = new GeminiEncounterExtractionService(geminiApiKeyStore, promptCacheService);
        var geminiOmniboxAnswerService = new GeminiOmniboxAnswerService(geminiApiKeyStore, promptCacheService);
        var geminiWikiPatchService = new GeminiWikiPatchService(geminiApiKeyStore, promptCacheService);
        var geminiWikiRewriteService = new GeminiWikiRewriteService(geminiApiKeyStore, promptCacheService);
        var geminiEncounterWikiRewriteService = new GeminiEncounterWikiRewriteService(geminiApiKeyStore, promptCacheService);
        var encounterNodeWriterService = new MarkdownEncounterNodeWriterService(new MarkdownTopicPageWriterService());
        _mobileApiHost = new MobileApiHost(appSettings, patientRegistryService);

        var viewModel = new MainWindowViewModel(
            new FilePickerService(),
            new VaultIngestService(testVaultPath, new EntityScaffoldingService()),
            new FastRegexScrubberService(),
            new MockEncounterDraftService(),
            new PresidioScrubberService(),
            settingsService,
            new JsonIngestAuditService(),
            patientRegistryService,
            new SecondPassScrubberService(),
            new DeterministicImageDocumentSafetyService(),
            new OllamaOmniboxPreflightService(promptCacheService),
            new VaultContextService(),
            new OllamaDollyVaultChatService(promptCacheService),
            new JsonConversationArchiveService(),
            new OllamaLocalModelWarmupService(promptCacheService),
            geminiApiKeyStore,
            geminiApiKeyStore,
            geminiOmniboxAnswerService,
            new GeminiSmallTalkService(geminiApiKeyStore),
            new CSharpResponsePersonalizationService(),
            geminiWikiPatchService,
            geminiWikiRewriteService,
            new OllamaWikiPatchService(promptCacheService),
            new MarkdownWikiPatchApplyService(),
            new MarkdownDreamRunnerAuditService(),
            new MarkdownSpecialtyWeaverService(),
            new MarkdownSymptomWatcherService(),
            new MarkdownPatternLinkerService(),
            new MarkdownHealthGoalEngineService(),
            new MarkdownHealthPreferenceService(),
            new MarkdownDrugInteractionScannerService(),
            new MarkdownPreVisitBriefService(),
            new MarkdownOriginalSourceRetrievalService(),
            new MarkdownSessionOpenAwarenessService(),
            new MarkdownDollyWorkingSummaryService(),
            new MarkdownDollyTaskBoardService(),
            new MarkdownDollyAgentStateService(),
            new MarkdownChronosLedgerService(),
            new OllamaPatientIntakeExtractionService(),
            new OllamaGemmaActionPacketService(),
            new GeminiBackendPipelineService(
                geminiApiKeyStore,
                geminiClassifierService,
                geminiExtractionService,
                encounterNodeWriterService,
                geminiEncounterWikiRewriteService),
            new VitaMasteryService());
        _mobileApiHost.ChatHandler = viewModel.HandleMobileChatAsync;
        _mobileApiHost.ChartPacketHandler = viewModel.HandleMobileChartPacketAsync;
        _mobileApiHost.VitaMasteryHandler = viewModel.HandleMobileVitaMasteryAsync;
        _mobileApiHost.DataHunterQuestHandler = viewModel.HandleMobileDataHunterQuestAsync;
        _mobileApiHost.OfflineItemHandler = viewModel.HandleMobileOfflineItemAsync;
        _mobileApiHost.CaptureHandler = viewModel.HandleMobileCaptureAsync;
        _mobileApiHost.ChartPhotoHandler = viewModel.HandleMobileChartPhotoAsync;
        DataContext = viewModel;

        Loaded += HandleLoaded;
        Closed += HandleClosed;
    }

    private async void HandleLoaded(object sender, RoutedEventArgs e)
    {
        await _mobileApiHost.StartAsync();
        _ = StartSidecarsAsync();
    }

    private async void HandleClosed(object? sender, EventArgs e)
    {
        _sidecarManager.Dispose();
        await _mobileApiHost.DisposeAsync();
    }

    private async Task StartSidecarsAsync()
    {
        try
        {
            await _sidecarManager.StartAsync();
        }
        catch
        {
            // Sidecars are useful for ingest/OCR, but the mobile bridge should stay available.
        }
    }
}
