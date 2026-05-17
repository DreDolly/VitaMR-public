using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using VitaMR.Models;

namespace VitaMR.ViewModels;

public sealed class ChatMessageViewModel
{
    private static readonly Brush UserBubbleBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D9F3EA"));
    private static readonly Brush DollyBubbleBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F0EAFE"));
    private static readonly Brush MessageTextBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1F2933"));

    public ChatMessageViewModel(
        MessageAuthor author,
        string label,
        IEnumerable<string> paragraphs,
        IEnumerable<AttachmentViewModel>? attachments = null)
    {
        Author = author;
        Label = label;
        Paragraphs = new ObservableCollection<string>(paragraphs);
        Attachments = new ObservableCollection<AttachmentViewModel>(attachments ?? []);
    }

    public MessageAuthor Author { get; }

    public string Label { get; }

    public ObservableCollection<string> Paragraphs { get; }

    public ObservableCollection<AttachmentViewModel> Attachments { get; }

    public HorizontalAlignment RowAlignment => Author == MessageAuthor.User
        ? HorizontalAlignment.Right
        : HorizontalAlignment.Left;

    public Thickness RowMargin => Author == MessageAuthor.User
        ? new Thickness(220, 0, 0, 24)
        : new Thickness(0, 0, 220, 24);

    public Visibility DollyAvatarVisibility => Author == MessageAuthor.Dolly
        ? Visibility.Visible
        : Visibility.Collapsed;

    public Visibility UserAvatarVisibility => Author == MessageAuthor.User
        ? Visibility.Visible
        : Visibility.Collapsed;

    public HorizontalAlignment MetaAlignment => Author == MessageAuthor.User
        ? HorizontalAlignment.Right
        : HorizontalAlignment.Left;

    public Brush BubbleBrush => Author == MessageAuthor.User ? UserBubbleBrush : DollyBubbleBrush;

    public Brush TextBrush => MessageTextBrush;

    public CornerRadius BubbleCornerRadius => Author == MessageAuthor.User
        ? new CornerRadius(14, 14, 6, 14)
        : new CornerRadius(14, 14, 14, 6);
}
