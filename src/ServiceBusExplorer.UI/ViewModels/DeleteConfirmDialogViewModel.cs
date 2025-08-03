using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ServiceBusExplorer.UI;

public partial class DeleteConfirmDialogViewModel : ObservableObject
{
    [ObservableProperty] private string messageId = string.Empty;
    [ObservableProperty] private string entityPath = string.Empty;
    [ObservableProperty] private string messageType = string.Empty;
    [ObservableProperty] private bool isDeadLetter;
    
    public IRelayCommand DeleteCommand { get; }
    public IRelayCommand CancelCommand { get; }
    
    public event EventHandler<bool>? CloseRequested;
    
    public DeleteConfirmDialogViewModel()
    {
        DeleteCommand = new RelayCommand(OnDelete);
        CancelCommand = new RelayCommand(OnCancel);
    }
    
    public void Initialize(string messageId, string entityPath, bool isDeadLetter)
    {
        MessageId = messageId;
        EntityPath = entityPath;
        IsDeadLetter = isDeadLetter;
        MessageType = isDeadLetter ? "Type: Dead Letter Message" : "Type: Active Message";
    }
    
    private void OnDelete()
    {
        CloseRequested?.Invoke(this, true);
    }
    
    private void OnCancel()
    {
        CloseRequested?.Invoke(this, false);
    }
}
