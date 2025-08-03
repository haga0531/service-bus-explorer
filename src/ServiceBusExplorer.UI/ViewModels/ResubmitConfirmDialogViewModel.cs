using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ServiceBusExplorer.UI;

public partial class ResubmitConfirmDialogViewModel : ObservableObject
{
    [ObservableProperty] private string messageId = string.Empty;
    [ObservableProperty] private string messageStatus = string.Empty;
    [ObservableProperty] private string entityPath = string.Empty;
    [ObservableProperty] private string contentType = string.Empty;
    [ObservableProperty] private bool isDeadLetter;
    [ObservableProperty] private bool deleteFromDeadLetter;
    [ObservableProperty] private int messageCount = 1;
    [ObservableProperty] private bool isMultiple;
    
    public IRelayCommand ResubmitCommand { get; }
    public IRelayCommand CancelCommand { get; }
    
    public event EventHandler<(bool confirmed, bool deleteFromDeadLetter)>? CloseRequested;
    
    public ResubmitConfirmDialogViewModel()
    {
        ResubmitCommand = new RelayCommand(OnResubmit);
        CancelCommand = new RelayCommand(OnCancel);
    }
    
    public void Initialize(string messageId, bool isDeadLetter, string entityPath, string contentType)
    {
        MessageId = messageId;
        IsDeadLetter = isDeadLetter;
        MessageStatus = isDeadLetter ? "Dead Letter" : "Active";
        EntityPath = entityPath;
        ContentType = contentType;
        DeleteFromDeadLetter = isDeadLetter; // Default to true for dead letter messages
    }
    
    private void OnResubmit()
    {
        CloseRequested?.Invoke(this, (true, DeleteFromDeadLetter));
    }
    
    private void OnCancel()
    {
        CloseRequested?.Invoke(this, (false, false));
    }
}
