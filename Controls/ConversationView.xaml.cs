using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using VitaMR.ViewModels;

namespace VitaMR.Controls;

public partial class ConversationView : UserControl
{
    private MainWindowViewModel? _viewModel;

    public ConversationView()
    {
        InitializeComponent();
        DataContextChanged += ConversationView_DataContextChanged;
    }

    private void ConversationView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.Messages.CollectionChanged -= Messages_CollectionChanged;
        }

        _viewModel = e.NewValue as MainWindowViewModel;

        if (_viewModel is not null)
        {
            _viewModel.Messages.CollectionChanged += Messages_CollectionChanged;
        }
    }

    private void Messages_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Dispatcher.BeginInvoke(() => ConversationScrollViewer.ScrollToEnd());
    }
}
