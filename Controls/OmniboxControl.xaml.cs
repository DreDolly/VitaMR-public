using System.Windows.Controls;
using System.Windows.Input;
using System.Windows;
using System.Windows.Data;
using VitaMR.ViewModels;

namespace VitaMR.Controls;

public partial class OmniboxControl : UserControl
{
    public OmniboxControl()
    {
        InitializeComponent();
    }

    private void PromptTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || Keyboard.Modifiers == ModifierKeys.Shift)
        {
            return;
        }

        e.Handled = true;

        PromptTextBox
            .GetBindingExpression(TextBox.TextProperty)
            ?.UpdateSource();

        if (DataContext is MainWindowViewModel viewModel &&
            viewModel.SendMessageCommand.CanExecute(null))
        {
            viewModel.SendMessageCommand.Execute(null);
        }
    }

    private void PromptTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var lineCount = Math.Max(PromptTextBox.LineCount, 1);
        PromptTextBox.Height = Math.Min(164, 18 + (lineCount * 22));
    }

    private void Omnibox_PreviewDragEnter(object sender, DragEventArgs e)
    {
        UpdateDragState(e, true);
    }

    private void Omnibox_PreviewDragOver(object sender, DragEventArgs e)
    {
        UpdateDragState(e, true);
    }

    private void Omnibox_PreviewDragLeave(object sender, DragEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.IsDragTargetActive = false;
        }
    }

    private void Omnibox_PreviewDrop(object sender, DragEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        viewModel.IsDragTargetActive = false;

        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            return;
        }

        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths)
        {
            viewModel.AddAttachmentPaths(paths);
        }

        e.Effects = DragDropEffects.Copy;
        e.Handled = true;
    }

    private void UpdateDragState(DragEventArgs e, bool isActive)
    {
        var hasFiles = e.Data.GetDataPresent(DataFormats.FileDrop);
        e.Effects = hasFiles ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;

        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.IsDragTargetActive = isActive && hasFiles;
        }
    }
}
