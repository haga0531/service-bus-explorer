using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ServiceBusExplorer.Core;

namespace ServiceBusExplorer.UI;

public partial class PurgeConfirmDialogViewModel : ObservableObject
{
    [ObservableProperty] private string entityPath = string.Empty;
    [ObservableProperty] private int totalMessageCount;
    [ObservableProperty] private int activeMessageCount;
    [ObservableProperty] private int deadLetterMessageCount;
    [ObservableProperty] private PurgeOption selectedOption = PurgeOption.ActiveOnly;
    [ObservableProperty] private bool confirmationChecked;
    [ObservableProperty] private int messagesToPurgeCount;
    
    public IRelayCommand PurgeCommand { get; }
    public IRelayCommand CancelCommand { get; }
    
    public event EventHandler<(bool confirmed, PurgeOption option)>? CloseRequested;
    
    public PurgeConfirmDialogViewModel()
    {
        PurgeCommand = new RelayCommand(OnPurge, CanPurge);
        CancelCommand = new RelayCommand(OnCancel);
    }
    
    public void Initialize(string entityPath, int activeCount, int deadLetterCount)
    {
        EntityPath = entityPath;
        ActiveMessageCount = activeCount;
        DeadLetterMessageCount = deadLetterCount;
        TotalMessageCount = activeCount + deadLetterCount;
        
        // 自動的に適切なオプションを選択
        if (activeCount == 0 && deadLetterCount > 0)
        {
            SelectedOption = PurgeOption.DeadLetterOnly;
        }
        else if (activeCount > 0 && deadLetterCount == 0)
        {
            SelectedOption = PurgeOption.ActiveOnly;
        }
        else
        {
            SelectedOption = PurgeOption.All;
        }
        
        UpdateMessagesToPurgeCount();
    }
    
    partial void OnSelectedOptionChanged(PurgeOption value)
    {
        UpdateMessagesToPurgeCount();
        PurgeCommand.NotifyCanExecuteChanged();
    }
    
    private void UpdateMessagesToPurgeCount()
    {
        MessagesToPurgeCount = SelectedOption switch
        {
            PurgeOption.All => TotalMessageCount,
            PurgeOption.ActiveOnly => ActiveMessageCount,
            PurgeOption.DeadLetterOnly => DeadLetterMessageCount,
            _ => 0
        };
        
        Console.WriteLine($"[UpdateMessagesToPurgeCount] SelectedOption: {SelectedOption}, " +
                          $"ActiveCount: {ActiveMessageCount}, DeadLetterCount: {DeadLetterMessageCount}, " +
                          $"TotalCount: {TotalMessageCount}, MessagesToPurgeCount: {MessagesToPurgeCount}");
    }
    
    private bool CanPurge()
    {
        var canPurge = ConfirmationChecked && MessagesToPurgeCount > 0;
        
        Console.WriteLine($"[CanPurge] ConfirmationChecked: {ConfirmationChecked}, " +
                          $"MessagesToPurgeCount: {MessagesToPurgeCount}, " +
                          $"CanPurge: {canPurge}");
        
        return canPurge;
    }
    
    private void OnPurge()
    {
        CloseRequested?.Invoke(this, (true, SelectedOption));
    }
    
    private void OnCancel()
    {
        CloseRequested?.Invoke(this, (false, SelectedOption));
    }
    
    partial void OnConfirmationCheckedChanged(bool value)
    {
        PurgeCommand.NotifyCanExecuteChanged();
    }
}
