using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ServiceBusExplorer.Core;
using ServiceBusExplorer.Infrastructure;
using ServiceBusExplorer.Infrastructure.Models;
using ServiceBusExplorer.UI.Models;

namespace ServiceBusExplorer.UI;

public partial class MessageListViewModel : ObservableObject
{
    private readonly MessageService _messageService;
    private readonly ServiceBusAuthContext _authContext;
    private readonly Func<ServiceBusAuthContext, string, SendMessageDialogViewModel> _sendDialogVmFactory;
    private readonly ILogService _logService;
    private readonly Func<ServiceBusAuthContext, INamespaceProvider> _providerFactory;
    private CancellationTokenSource? _loadingCts;

    [ObservableProperty] private bool isLoading;
    
    partial void OnIsLoadingChanged(bool value)
    {
        (CancelLoadingCommand as IRelayCommand)?.NotifyCanExecuteChanged();
    }
    [ObservableProperty] private ObservableCollection<MessageViewModel> messages = [];
    [ObservableProperty] private string? errorMessage;
    [ObservableProperty] private int messageCount;
    [ObservableProperty] private int activeCount;
    [ObservableProperty] private int deadLetterCount;
    [ObservableProperty] private MessageViewModel? selectedMessage;
    [ObservableProperty] private string? formattedMessageBody;
    
    // Pagination properties
    [ObservableProperty] private int currentPage = 1;
    [ObservableProperty] private int pageSize = 50;
    [ObservableProperty] private int totalPages = 1;
    [ObservableProperty] private bool canGoToPreviousPage;
    [ObservableProperty] private bool canGoToNextPage;
    [ObservableProperty] private string pageInfo = "No messages";
    [ObservableProperty] private ObservableCollection<int> pageSizeOptions = [50, 100, 200, 500];
    [ObservableProperty] private string messageFilter = "All";
    [ObservableProperty] private ObservableCollection<string> messageFilterOptions = ["All", "Active", "Dead Letter"];
    
    // Loading overlay properties
    [ObservableProperty] private string loadingMessage = "Loading messages...";
    [ObservableProperty] private string? progressMessage;
    
    // Bulk selection properties
    private bool _isAllSelected;
    public bool IsAllSelected
    {
        get => _isAllSelected;
        set
        {
            if (SetProperty(ref _isAllSelected, value))
            {
                UpdateAllMessagesSelection(value);
            }
        }
    }
    
    public int SelectedCount => Messages.Count(m => m.IsSelected);
    public bool HasSelectedMessages => SelectedCount > 0;

    public IRelayCommand SendMessageCommand { get; }
    public IRelayCommand RefreshCommand { get; }
    public IRelayCommand CopyMessageBodyCommand { get; }
    public IRelayCommand FormatMessageBodyCommand { get; }
    public IRelayCommand DeleteMessageCommand { get; }
    public IRelayCommand PurgeCommand { get; }
    public IRelayCommand CancelLoadingCommand { get; }
    public IRelayCommand BulkResubmitCommand { get; }

    public MessageListViewModel(
        MessageService messageService,
        ServiceBusAuthContext authContext,
        Func<ServiceBusAuthContext, string, SendMessageDialogViewModel> sendDialogVmFactory,
        ILogService logService,
        Func<ServiceBusAuthContext, INamespaceProvider> providerFactory)
    {
        _messageService = messageService;
        _authContext = authContext;
        _sendDialogVmFactory = sendDialogVmFactory;
        _logService = logService;
        _providerFactory = providerFactory;
        SendMessageCommand = new AsyncRelayCommand(OpenSendMessageDialogAsync);
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        CopyMessageBodyCommand = new AsyncRelayCommand(CopyMessageBodyAsync);
        FormatMessageBodyCommand = new RelayCommand(FormatMessageBody);
        DeleteMessageCommand = new AsyncRelayCommand(DeleteMessageAsync);
        PurgeCommand = new AsyncRelayCommand(PurgeMessagesAsync);
        CancelLoadingCommand = new RelayCommand(CancelLoading, () => IsLoading);
        BulkResubmitCommand = new AsyncRelayCommand(BulkResubmitAsync, () => HasSelectedMessages);
    }
    
    [RelayCommand(CanExecute = nameof(CanGoToPreviousPage))]
    private async Task GoToPreviousPageAsync()
    {
        if (CurrentPage > 1)
        {
            CurrentPage--;
            await LoadPageAsync();
        }
    }
    
    [RelayCommand(CanExecute = nameof(CanGoToNextPage))]
    private async Task GoToNextPageAsync()
    {
        if (CurrentPage < TotalPages)
        {
            CurrentPage++;
            await LoadPageAsync();
        }
    }
    
    [RelayCommand]
    private async Task GoToFirstPageAsync()
    {
        CurrentPage = 1;
        await LoadPageAsync();
    }
    
    [RelayCommand]
    private async Task GoToLastPageAsync()
    {
        CurrentPage = TotalPages;
        await LoadPageAsync();
    }
    

    
    private string? _currentEntityPath;
    
    private void CancelLoading()
    {
        _logService.LogInfo("MessageListViewModel", "Cancelling loading operation");
        _loadingCts?.Cancel();
    }
    
    partial void OnPageSizeChanged(int value)
    {
        Console.WriteLine($"[MessageListViewModel.OnPageSizeChanged] Page size changed to: {value}");
        
        // Reset to first page when page size changes
        CurrentPage = 1;
        
        // Reload with new page size
        if (!string.IsNullOrEmpty(_currentEntityPath))
        {
            _ = LoadPageAsync();
        }
    }
    
    partial void OnMessageFilterChanged(string value)
    {
                 Console.WriteLine($"[MessageListViewModel.OnMessageFilterChanged] Message filter changed to: {value}");
        
        // Reset to first page when filter changes
        CurrentPage = 1;
        
        // Reload with new filter
        if (!string.IsNullOrEmpty(_currentEntityPath))
        {
            _ = LoadPageAsync();
        }
    }
    


    public async Task LoadAsync(string entityPath, string? subscription)
    {
        // Cancel any previous loading operation
        _loadingCts?.Cancel();
        _loadingCts = new CancellationTokenSource();
        
        try
        {
            _logService.LogInfo("MessageListViewModel", $"Loading from: {entityPath}, subscription: {subscription}");
            IsLoading = true;
            LoadingMessage = $"Loading messages from {entityPath}...";
            ProgressMessage = null;
            ErrorMessage = null;
            ClearMessages();
            SelectedMessage = null;
            FormattedMessageBody = null;
            _currentEntityPath = entityPath;
            _currentSubscription = subscription;
            
            // Reset pagination
            CurrentPage = 1;

            // Check if queue or subscription has auto-forwarding enabled
            await using var provider = _providerFactory(_authContext);
            
            if (string.IsNullOrEmpty(subscription)) // It's a queue
            {
                var queueProps = await provider.GetQueuePropertiesAsync(entityPath, _loadingCts.Token);
                if (queueProps != null && !string.IsNullOrEmpty(queueProps.ForwardTo))
                {
                    ErrorMessage = $"This queue has auto-forwarding enabled (forwards to: {queueProps.ForwardTo}). Message browsing is not supported for queues with auto-forwarding.";
                    ActiveCount = 0;
                    DeadLetterCount = 0;
                    TotalPages = 0;
                    PageInfo = "Auto-forwarding enabled";
                    return;
                }
            }
            else // It's a subscription
            {
                var subscriptionProps = await provider.GetSubscriptionPropertiesAsync(entityPath, subscription, _loadingCts.Token);
                if (subscriptionProps != null && !string.IsNullOrEmpty(subscriptionProps.ForwardTo))
                {
                    ErrorMessage = $"This subscription has auto-forwarding enabled (forwards to: {subscriptionProps.ForwardTo}). Message browsing is not supported for subscriptions with auto-forwarding.";
                    ActiveCount = 0;
                    DeadLetterCount = 0;
                    TotalPages = 0;
                    PageInfo = "Auto-forwarding enabled";
                    return;
                }
            }

            // First, get message counts
            var (activeCount, deadLetterCount) = await _messageService.GetMessageCountsAsync(
                _authContext,
                entityPath,
                subscription,
                _loadingCts.Token);

            ActiveCount = activeCount;
            DeadLetterCount = deadLetterCount;
            MessageCount = ActiveCount + DeadLetterCount;

            _logService.LogInfo("MessageListViewModel", $"Total messages - Active: {ActiveCount:N0}, Dead Letter: {DeadLetterCount:N0}");
            
            // Update loading message with count
            LoadingMessage = $"Loading {MessageCount:N0} messages from {entityPath}...";
            
            // Load first page
            await LoadPageAsync();
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("[MessageListViewModel.LoadAsync] Operation was cancelled");
            ErrorMessage = "Loading was cancelled";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error loading messages: {ex.Message}";
            Console.WriteLine($"[MessageListViewModel.LoadAsync] Error: {ex}");
        }
        finally
        {
            IsLoading = false;
            _loadingCts?.Dispose();
            _loadingCts = null;
        }
    }
    
    private string? _currentSubscription;
    
    private async Task LoadPageAsync()
    {
        if (string.IsNullOrEmpty(_currentEntityPath))
        {
            return;
        }

        // Create a new CTS if we don't have one
        _loadingCts ??= new CancellationTokenSource();
            
        try
        {
            Console.WriteLine($"[MessageListViewModel.LoadPageAsync] Loading page {CurrentPage} (size: {PageSize})");
            IsLoading = true;
            ErrorMessage = null;
            ClearMessages();
            
            // Determine includeDeadLetter based on filter
            var includeDeadLetter = MessageFilter == "All";
            var activeOnly = MessageFilter == "Active";
            var deadLetterOnly = MessageFilter == "Dead Letter";
            
            // Get paged messages with filter
            var pagedResult = await _messageService.GetPagedMessagesAsync(
                _authContext,
                _currentEntityPath,
                _currentSubscription,
                CurrentPage,
                PageSize,
                activeOnly,
                deadLetterOnly,
                _loadingCts.Token);
                
            // Update pagination properties
            TotalPages = pagedResult.TotalPages;
            CanGoToPreviousPage = pagedResult.HasPreviousPage;
            CanGoToNextPage = pagedResult.HasNextPage;
            
            // Update page info based on filter
            var displayTotalCount = pagedResult.TotalCount;
            if (displayTotalCount > 0)
            {
                var startIndex = pagedResult.StartIndex;
                var endIndex = pagedResult.EndIndex;
                PageInfo = $"{startIndex:N0}-{endIndex:N0} of {displayTotalCount:N0}";
            }
            else
            {
                PageInfo = "No messages";
            }
            
            Console.WriteLine($"[MessageListViewModel.LoadPageAsync] Retrieved {pagedResult.Items.Count} messages for page {CurrentPage}");
            
            // Add messages to collection
            foreach (var message in pagedResult.Items)
            {
                // Replace newlines and excessive whitespace in body text for single-line display
                var bodyText = (message.Body ?? "")
                    .Replace("\r\n", " ")
                    .Replace("\n", " ")
                    .Replace("\r", " ")
                    .Replace("\t", " ");
                // Collapse multiple spaces into one
                while (bodyText.Contains("  "))
                {
                    bodyText = bodyText.Replace("  ", " ");
                }
                
                var messageViewModel = new MessageViewModel(
                    message.MessageId ?? "NO-ID",
                    message.Label ?? "",
                    message.ContentType ?? "",
                    message.EnqueuedTime,
                    bodyText.Trim(),
                    message.IsDeadLetter);
                
                // Subscribe to property changes to update selection count
                messageViewModel.PropertyChanged += OnMessagePropertyChanged;
                
                Messages.Add(messageViewModel);
            }
            
            // Notify command state changes
            GoToPreviousPageCommand.NotifyCanExecuteChanged();
            GoToNextPageCommand.NotifyCanExecuteChanged();
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("[MessageListViewModel.LoadPageAsync] Operation was cancelled");
            // Don't show error for cancellation
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error loading page: {ex.Message}";
            Console.WriteLine($"[MessageListViewModel.LoadPageAsync] Error: {ex}");
        }
        finally
        {
            IsLoading = false;
        }
    }
    
    private async Task OpenSendMessageDialogAsync()
    {
        if (string.IsNullOrEmpty(_currentEntityPath))
        {
            return;
        }

        _logService.LogInfo("MessageListViewModel", $"Opening send message dialog for: {_currentEntityPath}");
            
        var dialogVm = _sendDialogVmFactory(_authContext, _currentEntityPath);
        var dialog = new SendMessageDialog { DataContext = dialogVm };
        
        // Subscribe to close event
        void OnCloseRequested(object? sender, bool messageSent)
        {
            dialogVm.CloseRequested -= OnCloseRequested;
            dialog.Close(messageSent);
        }
        dialogVm.CloseRequested += OnCloseRequested;
        
        var owner = (Application.Current?.ApplicationLifetime as
                     IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        var result = await dialog.ShowDialog<bool>(owner!);
        
        // If message was sent successfully, refresh the message list
        if (result && !string.IsNullOrEmpty(_currentEntityPath))
        {
            await LoadAsync(_currentEntityPath, null);
        }
    }
    
    private async Task RefreshAsync()
    {
        if (string.IsNullOrEmpty(_currentEntityPath))
        {
            return;
        }

        _logService.LogInfo("MessageListViewModel", $"Refreshing messages for {_currentEntityPath}");
        
        try
        {
            // First, update message counts
            var (activeCount, deadLetterCount) = await _messageService.GetMessageCountsAsync(
                _authContext,
                _currentEntityPath,
                _currentSubscription);

            ActiveCount = activeCount;
            DeadLetterCount = deadLetterCount;
            MessageCount = ActiveCount + DeadLetterCount;
            
            _logService.LogInfo("MessageListViewModel", $"Updated counts - Active: {activeCount:N0}, Dead Letter: {deadLetterCount:N0}");
            
            // Then reload current page
            await LoadPageAsync();
            
            _logService.LogInfo("MessageListViewModel", "Refresh completed successfully");
        }
        catch (Exception ex)
        {
            _logService.LogError("MessageListViewModel", "Error during refresh", ex);
            throw;
        }
    }
    
    private async Task CopyMessageBodyAsync()
    {
        if (SelectedMessage == null || string.IsNullOrEmpty(SelectedMessage.Body))
        {
            return;
        }

        _logService.LogInfo("MessageListViewModel", $"Copying message body to clipboard (Message ID: {SelectedMessage.MessageId})");
            
        var clipboard = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow?.Clipboard
            : null;
            
        if (clipboard != null)
        {
            await clipboard.SetTextAsync(FormattedMessageBody ?? SelectedMessage.Body);
        }
    }
    
    private (string queueOrTopic, string? subscription) ParseEntityPath(string entityPath)
    {
        var parts = entityPath.Split('/');
        if (parts.Length == 1)
        {
            return (parts[0], null); // Queue
        }
        else if (parts.Length == 3 && parts[1] == "subscriptions")
        {
            return (parts[0], parts[2]); // Topic/Subscription
        }
        else
        {
            // Default to treating as queue
            return (entityPath, null);
        }
    }
    
    private void FormatMessageBody()
    {
        if (SelectedMessage == null || string.IsNullOrEmpty(SelectedMessage.Body))
        {
            return;
        }

        _logService.LogInfo("MessageListViewModel", $"Formatting message body (Message ID: {SelectedMessage.MessageId})");
            
        try
        {
            // Try to format as JSON
            var jsonDoc = JsonDocument.Parse(SelectedMessage.Body);
            var formattedJson = JsonSerializer.Serialize(jsonDoc.RootElement, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
            
            // Create a new MessageViewModel with formatted body
            var formattedMessage = new MessageViewModel(
                SelectedMessage.MessageId,
                SelectedMessage.Label,
                SelectedMessage.ContentType,
                SelectedMessage.EnqueuedTime,
                formattedJson,
                SelectedMessage.IsDeadLetter);
                
            // Update the selected message
            var index = Messages.IndexOf(SelectedMessage);
            if (index >= 0)
            {
                Messages[index] = formattedMessage;
                SelectedMessage = formattedMessage;
            }
        }
        catch (JsonException)
        {
            // Not JSON, try XML
            try
            {
                var xmlDoc = XDocument.Parse(SelectedMessage.Body);
                var formattedXml = xmlDoc.ToString();
                
                // Create a new MessageViewModel with formatted body
                var formattedMessage = new MessageViewModel(
                    SelectedMessage.MessageId,
                    SelectedMessage.Label,
                    SelectedMessage.ContentType,
                    SelectedMessage.EnqueuedTime,
                    formattedXml,
                    SelectedMessage.IsDeadLetter);
                    
                // Update the selected message
                var index = Messages.IndexOf(SelectedMessage);
                if (index >= 0)
                {
                    Messages[index] = formattedMessage;
                    SelectedMessage = formattedMessage;
                }
            }
            catch
            {
                // Neither JSON nor XML, leave as is
            }
        }
    }
    
    partial void OnSelectedMessageChanged(MessageViewModel? value)
    {
        if (value == null)
        {
            FormattedMessageBody = null;
            return;
        }
        
        // Auto-format message body when message is selected
        try
        {
            // Try to format as JSON
            var jsonDoc = JsonDocument.Parse(value.Body);
            FormattedMessageBody = JsonSerializer.Serialize(jsonDoc.RootElement, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
        }
        catch (JsonException)
        {
            // Not JSON, try XML
            try
            {
                var xmlDoc = XDocument.Parse(value.Body);
                FormattedMessageBody = xmlDoc.ToString();
            }
            catch
            {
                // Neither JSON nor XML, use original
                FormattedMessageBody = value.Body;
            }
        }
    }
    
    [RelayCommand]
    private async Task EditAndResendAsync()
    {
        if (SelectedMessage == null)
        {
            return;
        }

        try
        {
            Console.WriteLine($"[EditAndResendAsync] Opening edit dialog for message: {SelectedMessage.MessageId}");
            
            // Create and show edit dialog
            var dialog = new EditMessageDialog();
            var viewModel = new EditMessageDialogViewModel(dialog);
            viewModel.Initialize(
                SelectedMessage.MessageId,
                SelectedMessage.Body ?? string.Empty,
                SelectedMessage.ContentType ?? "application/json",
                SelectedMessage.IsDeadLetter);
            dialog.DataContext = viewModel;
            
            var owner = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;
                
            await dialog.ShowDialog(owner!);
            
            Console.WriteLine($"[EditAndResendAsync] Dialog closed - confirmed: {viewModel.DialogResult}");
            
            if (!viewModel.DialogResult)
            {
                return;
            }

            // Store message ID and properties before sending
            var originalMessageId = SelectedMessage.MessageId;
            var isDeadLetter = SelectedMessage.IsDeadLetter;
            var deleteOriginal = viewModel.DeleteOriginal;
            
            // Send the edited message as new
            Console.WriteLine($"[EditAndResendAsync] Sending edited message");
            await _messageService.ResubmitMessageAsync(
                _authContext,
                _currentEntityPath!,
                viewModel.MessageBody,
                viewModel.ContentType,
                null); // Label can be null for now
            Console.WriteLine($"[EditAndResendAsync] Message sent successfully");
                
            // Delete original message if requested
            if (deleteOriginal)
            {
                try
                {
                    Console.WriteLine($"[EditAndResendAsync] Deleting original message {originalMessageId}");
                    var (queueOrTopic, subscription) = ParseEntityPath(_currentEntityPath!);
                    
                    if (isDeadLetter)
                    {
                        await _messageService.DeleteDeadLetterMessageAsync(
                            _authContext,
                            queueOrTopic,
                            subscription,
                            originalMessageId);
                    }
                    else
                    {
                        await _messageService.DeleteActiveMessageAsync(
                            _authContext,
                            queueOrTopic,
                            subscription,
                            originalMessageId);
                    }
                    
                    Console.WriteLine($"[EditAndResendAsync] Original message deleted");
                }
                catch (Exception deleteEx)
                {
                    Console.WriteLine($"[EditAndResendAsync] Failed to delete original message: {deleteEx.Message}");
                    // Continue - send was successful even if delete failed
                }
            }
            
            // Refresh the message list
            await RefreshAsync();
            
            // Show success notification
            ErrorMessage = null;
            Console.WriteLine($"Message edited and sent successfully");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to edit and resend message: {ex.Message}";
            Console.WriteLine($"[EditAndResendAsync] Error: {ex.Message}");
        }
    }
    
    [RelayCommand]
    private async Task ResubmitMessageAsync()
    {
        if (SelectedMessage == null || string.IsNullOrEmpty(_currentEntityPath))
        {
            return;
        }

        _logService.LogInfo("MessageListViewModel", $"User initiated resubmit for message: {SelectedMessage.MessageId} (IsDeadLetter: {SelectedMessage.IsDeadLetter})");
            
        try
        {
            // Create and show confirmation dialog
            var dialogVm = new ResubmitConfirmDialogViewModel();
            dialogVm.Initialize(
                SelectedMessage.MessageId,
                SelectedMessage.IsDeadLetter,
                _currentEntityPath,
                SelectedMessage.ContentType);
                
            var dialog = new ResubmitConfirmDialog { DataContext = dialogVm };
            
            // Subscribe to close event
            var confirmed = false;
            var deleteFromDeadLetter = false;
            
            void OnCloseRequested(object? sender, (bool confirmed, bool deleteFromDeadLetter) result)
            {
                dialogVm.CloseRequested -= OnCloseRequested;
                confirmed = result.confirmed;
                deleteFromDeadLetter = result.deleteFromDeadLetter;
                dialog.Close();
            }
            dialogVm.CloseRequested += OnCloseRequested;
            
            var owner = (Application.Current?.ApplicationLifetime as
                         IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            Console.WriteLine($"[ResubmitMessageAsync] Dialog owner: {owner}");
            
            try
            {
                await dialog.ShowDialog(owner!);
            }
            catch (Exception dialogEx)
            {
                Console.WriteLine($"[ResubmitMessageAsync] Dialog error: {dialogEx.Message}");
                throw;
            }
            
            if (!confirmed)
            {
                _logService.LogInfo("MessageListViewModel", "Message resubmit cancelled by user");
                return;
            }
            
            _logService.LogInfo("MessageListViewModel", $"Resubmit confirmed - Delete from dead letter: {deleteFromDeadLetter}");
                
            // Store message ID and properties before refresh
            var messageId = SelectedMessage.MessageId;
            var isDeadLetter = SelectedMessage.IsDeadLetter;
            
            // Resubmit the message
            _logService.LogInfo("MessageListViewModel", $"Resubmitting message {messageId}");
            await _messageService.ResubmitMessageAsync(
                _authContext,
                _currentEntityPath,
                SelectedMessage.Body ?? string.Empty,
                SelectedMessage.ContentType,
                SelectedMessage.Label);
            _logService.LogInfo("MessageListViewModel", $"Message {messageId} resubmitted successfully");
                
            // Delete from dead letter if requested
            if (deleteFromDeadLetter && isDeadLetter)
            {
                try
                {
                    Console.WriteLine($"[ResubmitMessageAsync] Deleting message {messageId} from dead letter queue");
                    var (queueOrTopic, subscription) = ParseEntityPath(_currentEntityPath);
                    await _messageService.DeleteDeadLetterMessageAsync(
                        _authContext,
                        queueOrTopic,
                        subscription,
                        messageId);
                    Console.WriteLine($"[ResubmitMessageAsync] Message {messageId} deleted from dead letter queue");
                }
                catch (Exception deleteEx)
                {
                    Console.WriteLine($"[ResubmitMessageAsync] Failed to delete message from dead letter: {deleteEx.Message}");
                    // Continue - resubmit was successful even if delete failed
                }
            }
            
            // Refresh the message list
            Console.WriteLine($"[ResubmitMessageAsync] Refreshing message list");
            await RefreshAsync();
            Console.WriteLine($"[ResubmitMessageAsync] Message list refreshed");
            
            // Show success notification (you can implement a proper notification system)
            ErrorMessage = null;
            Console.WriteLine($"Message {messageId} resubmitted successfully");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to resubmit message: {ex.Message}";
            Console.WriteLine($"[ResubmitMessageAsync] Error resubmitting message: {ex.Message}");
            Console.WriteLine($"[ResubmitMessageAsync] Stack trace: {ex.StackTrace}");
            Console.WriteLine($"[ResubmitMessageAsync] Inner exception: {ex.InnerException?.Message}");
        }
    }
    
    private async Task PurgeMessagesAsync()
    {
        if (string.IsNullOrEmpty(_currentEntityPath))
        {
            return;
        }

        _logService.LogInfo("MessageListViewModel", $"User initiated purge for: {_currentEntityPath}");
            
        try
        {
            
            // Create and show purge confirmation dialog
            var dialog = new PurgeConfirmDialog();
            var viewModel = new PurgeConfirmDialogViewModel();
            viewModel.Initialize(_currentEntityPath, ActiveCount, DeadLetterCount);
            dialog.DataContext = viewModel;
            
            var owner = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;
                
            var dialogResult = (confirmed: false, selectedOption: PurgeOption.ActiveOnly);
            
            void OnCloseRequested(object? sender, (bool confirmed, PurgeOption option) result)
            {
                viewModel.CloseRequested -= OnCloseRequested;
                dialogResult = result;
                dialog.Close();
            }
            
            viewModel.CloseRequested += OnCloseRequested;
            
            Console.WriteLine($"[PurgeMessagesAsync] Dialog owner: {owner}");
            await dialog.ShowDialog(owner!);
            Console.WriteLine($"[PurgeMessagesAsync] Dialog closed - confirmed: {dialogResult.confirmed}, option: {dialogResult.selectedOption}");
            
            if (!dialogResult.confirmed)
            {
                return;
            }

            // Show loading
            IsLoading = true;
            ErrorMessage = null;
            
            try
            {
                Console.WriteLine($"[PurgeMessagesAsync] Starting purge operation - option: {dialogResult.selectedOption}");
                var (queueOrTopic, subscription) = ParseEntityPath(_currentEntityPath);
                
                var purgedCount = await _messageService.PurgeMessagesAsync(
                    _authContext,
                    queueOrTopic,
                    subscription,
                    dialogResult.selectedOption);
                    
                Console.WriteLine($"[PurgeMessagesAsync] Purged {purgedCount} messages successfully");
                
                // Refresh the message list
                await RefreshAsync();
                
                // Show success notification
                ErrorMessage = null;
                Console.WriteLine($"Successfully purged {purgedCount} messages");
            }
            finally
            {
                IsLoading = false;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to purge messages: {ex.Message}";
            Console.WriteLine($"[PurgeMessagesAsync] Error purging messages: {ex.Message}");
            Console.WriteLine($"[PurgeMessagesAsync] Stack trace: {ex.StackTrace}");
            IsLoading = false;
        }
    }
    
    private async Task DeleteMessageAsync()
    {
        if (SelectedMessage == null)
        {
            return;
        }

        try
        {
            _logService.LogInfo("MessageListViewModel", $"User initiated delete for message: {SelectedMessage.MessageId}");
            
            var dialog = new DeleteConfirmDialog();
            var viewModel = new DeleteConfirmDialogViewModel();
            viewModel.Initialize(SelectedMessage.MessageId, _currentEntityPath ?? "", SelectedMessage.IsDeadLetter);
            dialog.DataContext = viewModel;
            
            var owner = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;
                
            var dialogResult = false;
            
            void OnCloseRequested(object? sender, bool result)
            {
                viewModel.CloseRequested -= OnCloseRequested;
                dialogResult = result;
                dialog.Close();
            }
            
            viewModel.CloseRequested += OnCloseRequested;
            
            Console.WriteLine($"[DeleteMessageAsync] Dialog owner: {owner}");
            await dialog.ShowDialog(owner!);
            if (!dialogResult)
            {
                _logService.LogInfo("MessageListViewModel", "Message deletion cancelled by user");
                return;
            }
                
            IsLoading = true;
            ErrorMessage = null;
            
            try
            {
                // Store message info before deletion
                var messageId = SelectedMessage.MessageId;
                var isDeadLetter = SelectedMessage.IsDeadLetter;
                
                _logService.LogInfo("MessageListViewModel", $"Deleting message {messageId} from {(isDeadLetter ? "dead letter" : "active")} queue");
                
                var (queueOrTopic, subscription) = ParseEntityPath(_currentEntityPath!);
                
                if (isDeadLetter)
                {
                    await _messageService.DeleteDeadLetterMessageAsync(
                        _authContext,
                        queueOrTopic,
                        subscription,
                        messageId);
                }
                else
                {
                    await _messageService.DeleteActiveMessageAsync(
                        _authContext,
                        queueOrTopic,
                        subscription,
                        messageId);
                }
                
                _logService.LogInfo("MessageListViewModel", $"Message {messageId} deleted successfully");
                
                // Refresh the message list
                await RefreshAsync();
                
                ErrorMessage = null;
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to delete message: {ex.Message}";
                _logService.LogError("MessageListViewModel", $"Error deleting message: {ex.Message}", ex);
            }
            finally
            {
                IsLoading = false;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to delete message: {ex.Message}";
            Console.WriteLine($"[DeleteMessageAsync] Error opening delete dialog: {ex.Message}");
            Console.WriteLine($"[DeleteMessageAsync] Stack trace: {ex.StackTrace}");
            IsLoading = false;
        }
    }
    
    private void UpdateAllMessagesSelection(bool isSelected)
    {
        foreach (var message in Messages)
        {
            message.IsSelected = isSelected;
        }
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(HasSelectedMessages));
        (BulkResubmitCommand as IRelayCommand)?.NotifyCanExecuteChanged();
    }
    
    private void ClearMessages()
    {
        // Unsubscribe from property changed events to prevent memory leaks
        foreach (var message in Messages)
        {
            message.PropertyChanged -= OnMessagePropertyChanged;
        }
        Messages.Clear();
    }
    
    private void OnMessagePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MessageViewModel.IsSelected))
        {
            OnPropertyChanged(nameof(SelectedCount));
            OnPropertyChanged(nameof(HasSelectedMessages));
            (BulkResubmitCommand as IRelayCommand)?.NotifyCanExecuteChanged();
        }
    }
    
    private async Task BulkResubmitAsync()
    {
        var selectedMessages = Messages.Where(m => m.IsSelected && m.IsDeadLetter).ToList();
        if (!selectedMessages.Any())
        {
            ErrorMessage = "No dead letter messages selected for resubmit";
            return;
        }
        
        try
        {
            // Show confirmation dialog
            var mainWindow = (Application.Current?.ApplicationLifetime as
                IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            if (mainWindow == null)
            {
                return;
            }

            var dialogVm = new ResubmitConfirmDialogViewModel
            {
                MessageCount = selectedMessages.Count,
                IsMultiple = true,
                IsDeadLetter = true, // Bulk resubmit is only for dead letter messages
                DeleteFromDeadLetter = true
            };
            var dialog = new ResubmitConfirmDialog { DataContext = dialogVm };

            var confirmed = false;
            var deleteFromDeadLetter = false;

            void OnCloseRequested(object? sender, (bool confirmed, bool deleteFromDeadLetter) result)
            {
                dialogVm.CloseRequested -= OnCloseRequested;
                confirmed = result.confirmed;
                deleteFromDeadLetter = result.deleteFromDeadLetter;
                dialog.Close();
            }

            dialogVm.CloseRequested += OnCloseRequested;
            await dialog.ShowDialog(mainWindow);
            
            if (!confirmed)
            {
                return;
            }

            IsLoading = true;
            LoadingMessage = $"Resubmitting {selectedMessages.Count} messages...";
            ProgressMessage = null;
            
            var successCount = 0;
            var failCount = 0;
            
            for (var i = 0; i < selectedMessages.Count; i++)
            {
                var message = selectedMessages[i];
                ProgressMessage = $"Processing message {i + 1} of {selectedMessages.Count}...";
                
                try
                {
                    await _messageService.ResubmitDeadLetterMessageAsync(
                        _authContext,
                        _currentEntityPath!,
                        message.MessageId,
                        _currentSubscription,
                        deleteFromDeadLetter);
                    
                    successCount++;
                    _logService.LogInfo("MessageListViewModel", $"Message {message.MessageId} resubmitted successfully");
                }
                catch (Exception ex)
                {
                    failCount++;
                    _logService.LogError("MessageListViewModel", $"Failed to resubmit message {message.MessageId}: {ex.Message}");
                }
            }
            
            // Show result
            if (failCount == 0)
            {
                ErrorMessage = null;
                LoadingMessage = $"Successfully resubmitted {successCount} messages";
            }
            else
            {
                ErrorMessage = $"Resubmitted {successCount} messages, {failCount} failed";
            }
            
            // Clear selection
            IsAllSelected = false;
            
            // Refresh the list
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to resubmit messages: {ex.Message}";
            _logService.LogError("MessageListViewModel", "Error during bulk resubmit", ex);
        }
        finally
        {
            IsLoading = false;
            ProgressMessage = null;
        }
    }
    
    partial void OnMessagesChanged(ObservableCollection<MessageViewModel>? value)
    {
        // Update selection state when messages change
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(HasSelectedMessages));
        (BulkResubmitCommand as IRelayCommand)?.NotifyCanExecuteChanged();
    }
}
