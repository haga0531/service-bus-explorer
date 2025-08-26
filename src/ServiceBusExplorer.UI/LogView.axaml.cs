using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace ServiceBusExplorer.UI;

public partial class LogView : UserControl
{
    private ScrollViewer? _scrollViewer;

    public LogView()
    {
        InitializeComponent();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        _scrollViewer = this.FindControl<ScrollViewer>("LogScrollViewer");

        if (DataContext is LogViewModel viewModel)
        {
            viewModel.Logs.CollectionChanged += (s, args) =>
            {
                // Auto-scroll to bottom when new items are added
                if (args.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
                {
                    Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        // Scroll to the end
                        _scrollViewer?.ScrollToEnd();
                    }, DispatcherPriority.Render);
                }
            };
        }
    }
}
