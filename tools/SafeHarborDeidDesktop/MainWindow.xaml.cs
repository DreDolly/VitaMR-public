using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using UglyToad.PdfPig;

namespace SafeHarborDeidDesktop;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private const string AiEndpoint = "http://127.0.0.1:11434";
    private const string PrivacyModel = "llama3.2:3b";
    private const string PreservationModel = "gemma4:e4b";

    private readonly SafeHarborDeidentifier _deidentifier = new();
    private readonly LocalModelReviewer _localModelReviewer = new();
    private readonly DispatcherTimer _copyPopupTimer = new()
    {
        Interval = TimeSpan.FromSeconds(3)
    };
    private DeidResult? _lastResult;
    private string _currentSourceName = "manual-input.txt";
    private string _statusText = "Paste text or open a supported file, then choose Clean Data.";

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        _copyPopupTimer.Tick += CopyPopupTimer_Tick;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string StatusText
    {
        get => _statusText;
        private set
        {
            if (_statusText == value)
            {
                return;
            }

            _statusText = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StatusText)));
        }
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        InputTextBox.Focus();
        UpdateInputPlaceholder();
    }

    private void InputTextBox_GotKeyboardFocus(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)
    {
        UpdateInputPlaceholder();
    }

    private void InputTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        UpdateInputPlaceholder();
    }

    private void UpdateInputPlaceholder()
    {
        InputPlaceholderTextBlock.Visibility = string.IsNullOrEmpty(InputTextBox.Text)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void OpenFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Open text or document",
            Filter = "Supported files|*.txt;*.md;*.log;*.csv;*.tsv;*.json;*.xml;*.html;*.htm;*.docx;*.pdf|Text files|*.txt;*.md;*.log;*.csv;*.tsv|Documents|*.docx;*.pdf|All files|*.*"
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        LoadFile(dialog.FileName);
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] { Length: > 0 } files)
        {
            e.Handled = true;
            return;
        }

        LoadFile(files[0]);
        e.Handled = true;
    }

    private void LoadFile(string path)
    {
        var extraction = LocalTextExtractor.Extract(path);
        _currentSourceName = Path.GetFileName(path);
        InputTextBox.Text = extraction.Text;
        OutputTextBox.Clear();
        StatusText = extraction.Text.Length == 0
            ? "That file could not be read by this first desktop version."
            : extraction.Warnings.Count > 0
                ? $"Loaded {Path.GetFileName(path)}. {extraction.Warnings[0]}"
                : $"Loaded {Path.GetFileName(path)}. Choose Clean Data when ready.";
    }

    private async void RunDeid_Click(object sender, RoutedEventArgs e)
    {
        var text = InputTextBox.Text;
        _lastResult = _deidentifier.Deidentify(text, _currentSourceName);

        if (AiModeCheckBox.IsChecked == true && _lastResult.Status != "blocked")
        {
            StatusText = "Running deterministic scrub, then local AI privacy and preservation review...";
            SetRunControlsEnabled(false);
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
                var aiReview = await _localModelReviewer.ReviewAsync(
                    AiEndpoint,
                    PrivacyModel,
                    PreservationModel,
                    text,
                    _lastResult.RedactedText,
                    timeout.Token);
                var warnings = _lastResult.Warnings.Concat(aiReview.Warnings).ToList();
                var findings = _lastResult.Findings
                    .Concat(aiReview.PrivacyFindings.Select(finding => new Finding(
                        $"ai_{finding.Type}",
                        finding.Text,
                        finding.Replacement)))
                    .ToList();
                var status = aiReview.Status == "over_redacted_needs_review"
                    ? "over_redacted_needs_review"
                    : _lastResult.Status == "safe_harbor_candidate"
                        ? "safe_harbor_candidate_ai_reviewed"
                        : _lastResult.Status;

                _lastResult = new DeidResult(
                    status,
                    aiReview.FinalText,
                    findings,
                    warnings,
                    _lastResult.BlockedReasons,
                    aiReview);
            }
            finally
            {
                SetRunControlsEnabled(true);
            }
        }

        OutputTextBox.Text = _lastResult.RedactedText;
        StatusText = _lastResult.Status switch
        {
            "safe_harbor_candidate_ai_reviewed" => $"Safe Harbor candidate with local AI review. Replacements: {_lastResult.Findings.Count}.",
            "over_redacted_needs_review" => "AI preservation review found possible over-redaction. Check the audit panel.",
            "safe_harbor_candidate" => $"Safe Harbor candidate. Replacements: {_lastResult.Findings.Count}.",
            "needs_review" => $"Needs review. Replacements: {_lastResult.Findings.Count}. Check the audit panel.",
            "blocked" => "Blocked. Check the audit panel for why.",
            _ => _lastResult.Status
        };
    }

    private async void AiModeButton_Click(object sender, RoutedEventArgs e)
    {
        AiModeButton.IsEnabled = false;
        StatusText = "Checking local AI Mode connection...";

        try
        {
            if (await IsLocalAiConnectedAsync())
            {
                AiModeCheckBox.Visibility = Visibility.Visible;
                AiModeCheckBox.IsChecked = true;
                StatusText = "AI Mode is enabled.";
                MessageBox.Show(
                    this,
                    "AI Mode is experimental and can be slow. It uses local models to review the deterministic scrub, so please review the audit before relying on the result.",
                    "AI Mode",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            AiModeCheckBox.Visibility = Visibility.Collapsed;
            AiModeCheckBox.IsChecked = false;
            StatusText = "AI Mode is not connected.";
            MessageBox.Show(
                this,
                "AI Mode is not connected yet. Ask Codex or Claude to help you set up AI Mode using the README instructions.",
                "AI Mode Setup",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        finally
        {
            AiModeButton.IsEnabled = true;
        }
    }

    private static async Task<bool> IsLocalAiConnectedAsync()
    {
        try
        {
            using var httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(3)
            };
            using var response = await httpClient.GetAsync($"{AiEndpoint}/api/tags");
            return response.IsSuccessStatusCode;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or UriFormatException)
        {
            return false;
        }
    }

    private void SaveResult_Click(object sender, RoutedEventArgs e)
    {
        if (_lastResult is null)
        {
            MessageBox.Show(this, "Choose Clean Data first, then save the result.", "Safe Harbor Cleaner", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Save redacted text",
            FileName = "safe-harbor-redacted.txt",
            Filter = "Text file|*.txt"
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        File.WriteAllText(dialog.FileName, _lastResult.RedactedText, Encoding.UTF8);
        var reportPath = Path.Combine(
            Path.GetDirectoryName(dialog.FileName) ?? Environment.CurrentDirectory,
            Path.GetFileNameWithoutExtension(dialog.FileName) + ".audit.json");
        File.WriteAllText(reportPath, _lastResult.ToAuditJson(), Encoding.UTF8);
        StatusText = $"Saved redacted text and audit report to {Path.GetDirectoryName(dialog.FileName)}.";
    }

    private void ClearInput_Click(object sender, RoutedEventArgs e)
    {
        InputTextBox.Clear();
        OutputTextBox.Clear();
        _lastResult = null;
        _currentSourceName = "manual-input.txt";
        InputTextBox.Focus();
        UpdateInputPlaceholder();
        StatusText = "Paste text or open a supported file, then choose Clean Data.";
    }

    private void CopyResult_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(OutputTextBox.Text))
        {
            Clipboard.SetText(OutputTextBox.Text);
            StatusText = "Clean result copied.";
            ShowCopyPopup("Clean result copied.");
        }
    }

    private void ShowCopyPopup(string message)
    {
        CopyPopupText.Text = message;
        CopyPopup.Visibility = Visibility.Visible;
        CopyPopup.Opacity = 1;
        _copyPopupTimer.Stop();
        _copyPopupTimer.Start();
    }

    private void CopyPopupTimer_Tick(object? sender, EventArgs e)
    {
        _copyPopupTimer.Stop();
        CopyPopup.Opacity = 0;
        CopyPopup.Visibility = Visibility.Collapsed;
    }

    private void SetRunControlsEnabled(bool enabled)
    {
        InputTextBox.IsEnabled = enabled;
        AiModeCheckBox.IsEnabled = enabled;
        AiModeButton.IsEnabled = enabled;
    }
}

internal static class LocalTextExtractor
{
    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".md", ".log", ".csv", ".tsv", ".json", ".xml", ".html", ".htm"
    };

    public static ExtractionResult Extract(string path)
    {
        var extension = Path.GetExtension(path);

        if (TextExtensions.Contains(extension))
        {
            return new ExtractionResult(File.ReadAllText(path, Encoding.UTF8), []);
        }

        if (extension.Equals(".docx", StringComparison.OrdinalIgnoreCase))
        {
            return new ExtractionResult(ExtractDocx(path), []);
        }

        if (extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            var text = ExtractPdfText(path);
            if (string.IsNullOrWhiteSpace(text))
            {
                return new ExtractionResult(
                    string.Empty,
                    ["No selectable PDF text was found. This may be a scanned or image-based PDF, which is still blocked."]);
            }

            return new ExtractionResult(
                text,
                ["Text PDF loaded. Embedded images, signatures, and scanned page content are not reviewed by this version."]);
        }

        if (extension.Equals(".doc", StringComparison.OrdinalIgnoreCase))
        {
            return new ExtractionResult(string.Empty, ["Legacy DOC files are blocked. Convert to DOCX or text first."]);
        }

        if (IsVisualOrMediaExtension(extension))
        {
            return new ExtractionResult(string.Empty, ["Images, audio, and video need visual/biometric review before Safe Harbor candidate status."]);
        }

        return new ExtractionResult(string.Empty, [$"Unsupported file type: {extension}"]);
    }

    private static string ExtractDocx(string path)
    {
        var builder = new StringBuilder();
        using var archive = ZipFile.OpenRead(path);
        foreach (var entry in archive.Entries.Where(entry =>
                     entry.FullName.StartsWith("word/", StringComparison.OrdinalIgnoreCase) &&
                     entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)))
        {
            using var stream = entry.Open();
            using var reader = new StreamReader(stream, Encoding.UTF8);
            var xml = reader.ReadToEnd();
            foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(xml, @"<w:t[^>]*>(.*?)</w:t>", System.Text.RegularExpressions.RegexOptions.Singleline))
            {
                builder.AppendLine(System.Net.WebUtility.HtmlDecode(match.Groups[1].Value));
            }
        }

        return builder.ToString();
    }

    private static string ExtractPdfText(string path)
    {
        var builder = new StringBuilder();
        using var document = PdfDocument.Open(path);

        foreach (var page in document.GetPages())
        {
            var text = page.Text?.Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            builder.AppendLine(text);
            builder.AppendLine();
        }

        return builder.ToString().Trim();
    }
    private static bool IsVisualOrMediaExtension(string extension)
    {
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

internal sealed record ExtractionResult(string Text, IReadOnlyList<string> Warnings);

