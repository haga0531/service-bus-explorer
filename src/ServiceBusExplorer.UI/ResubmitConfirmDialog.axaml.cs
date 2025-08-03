using Avalonia.Controls;

namespace ServiceBusExplorer.UI;

public partial class ResubmitConfirmDialog : Window
{
    public ResubmitConfirmDialog()
    {
        InitializeComponent();
        
        // Subscribe to view model events when DataContext is set
        DataContextChanged += OnDataContextChanged;
    }
    
    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        if (DataContext is ResubmitConfirmDialogViewModel vm)
        {
            vm.CloseRequested += OnCloseRequested;
        }
    }
    
    private void OnCloseRequested(object? sender, (bool confirmed, bool deleteFromDeadLetter) result)
    {
        if (DataContext is ResubmitConfirmDialogViewModel vm)
        {
            vm.CloseRequested -= OnCloseRequested;
        }
        
        // For bulk operations, we only care about confirmed/cancelled
        Close(result.confirmed);
    }
}
