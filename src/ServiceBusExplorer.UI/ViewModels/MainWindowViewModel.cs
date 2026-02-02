// src/ServiceBusExplorer.UI/ViewModels/MainWindowViewModel.cs

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Azure.Messaging.ServiceBus.Administration;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ServiceBusExplorer.Core;
using ServiceBusExplorer.Core.Models;
using ServiceBusExplorer.Infrastructure;
using ServiceBusExplorer.Infrastructure.Models;

namespace ServiceBusExplorer.UI;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly Func<ConnectDialogViewModel> _dialogVmFactory;
    private readonly Func<ServiceBusAuthContext, MessageListViewModel> _msgVmFactory;
    private readonly Func<ServiceBusAuthContext, INamespaceProvider> _providerFactory;
    private readonly ILogService _logService;
    private ServiceBusAuthContext? _currentAuthContext;

    /// <summary>Hierarchical node list (TreeView ItemsSource)</summary>
    public ObservableCollection<NamespaceNode> Nodes { get; } = [];

    [ObservableProperty]
    private NamespaceNode? selectedNode;

    [ObservableProperty]
    private MessageListViewModel? messageList;
    
    [ObservableProperty]
    private bool isLoading = false;
    
    [ObservableProperty]
    private string loadingMessage = string.Empty;
    
    [ObservableProperty]
    private bool isConnecting = false;
    
    [ObservableProperty]
    private string connectionProgress = string.Empty;
    
    [ObservableProperty]
    private string? errorMessage;
    
    [ObservableProperty]
    private LogViewModel logViewModel;

        public IRelayCommand ConnectCommand { get; }
    public IRelayCommand ClearErrorCommand { get; }
    public IRelayCommand RefreshNodeCommand { get; }
    public IRelayCommand NodeExpandedCommand { get; }
    
    public MainWindowViewModel(
        Func<ConnectDialogViewModel> dialogVmFactory,
        Func<ServiceBusAuthContext, MessageListViewModel> msgVmFactory,
        Func<ServiceBusAuthContext, INamespaceProvider> providerFactory,
        LogViewModel logViewModel,
        ILogService logService)
    {
        _dialogVmFactory  = dialogVmFactory;
        _msgVmFactory     = msgVmFactory;
        _providerFactory  = providerFactory;
        LogViewModel      = logViewModel;
        _logService       = logService;
        ConnectCommand    = new AsyncRelayCommand(OpenConnectDialogAsync);
        ClearErrorCommand = new RelayCommand(() => ErrorMessage = null);
        RefreshNodeCommand = new AsyncRelayCommand<NamespaceNode>(RefreshNodeAsync);
        NodeExpandedCommand = new AsyncRelayCommand<NamespaceNode>(LoadNodeMessageCountsAsync);
    }

    partial void OnMessageListChanged(MessageListViewModel? oldValue, MessageListViewModel? newValue)
    {
        // Unsubscribe from old MessageList
        if (oldValue != null)
        {
            oldValue.PropertyChanged -= OnMessageListPropertyChanged;
        }

        // Subscribe to new MessageList
        if (newValue != null)
        {
            newValue.PropertyChanged += OnMessageListPropertyChanged;
        }
    }

    private void OnMessageListPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // Update sidebar counts when MessageList counts change
        if (SelectedNode != null && MessageList != null &&
            (e.PropertyName == nameof(MessageListViewModel.ActiveCount) ||
             e.PropertyName == nameof(MessageListViewModel.DeadLetterCount)))
        {
            SelectedNode.ActiveMessageCount = MessageList.ActiveCount;
            SelectedNode.DeadLetterMessageCount = MessageList.DeadLetterCount;
        }
    }

    partial void OnSelectedNodeChanged(NamespaceNode? value)
    {
        Console.WriteLine($"[OnSelectedNodeChanged] Node selected: {value?.Name}, Type: {value?.EntityType}, Path: {value?.FullPath}");

        if (value is null || _currentAuthContext is null)
        {
            Console.WriteLine("[OnSelectedNodeChanged] Early return - value or auth context is null");
            return;
        }

        // If this is a folder node (EntityType is null), don't try to load messages
        if (value.EntityType is null)
        {
            Console.WriteLine("[OnSelectedNodeChanged] Folder node selected, clearing message list");
            // Clear the message list for folder nodes
            MessageList = null;
            return;
        }

        // Ensure MessageList is initialized
        if (MessageList is null)
        {
            Console.WriteLine("[OnSelectedNodeChanged] Creating new MessageList");
            MessageList = _msgVmFactory(_currentAuthContext);
        }

        // Build full path
        var fullPath = GetFullPath(value);
        Console.WriteLine($"[OnSelectedNodeChanged] Full path: {fullPath}");
        
        string? subscription = null;
        string? topicPath = null;
        
        if (value.EntityType == NamespaceEntity.EntityType.Topic)
        {
            // For topics, we need to handle subscription selection
            Console.WriteLine("[OnSelectedNodeChanged] Topic selected, showing warning");
            ErrorMessage = "Please select a subscription to view messages. Topic-level message browsing is not supported.";
            MessageList.Messages.Clear();
            return;
        }
        else if (value.EntityType == NamespaceEntity.EntityType.Subscription)
        {
            // For subscriptions, we need to get the parent topic path
            if (value.Parent != null && value.Parent.IsTopic)
            {
                topicPath = value.Parent.FullPath;
                subscription = value.Name;
                Console.WriteLine($"[OnSelectedNodeChanged] Subscription selected: {subscription} under topic: {topicPath}");
                _ = LoadMessagesAsync(topicPath, subscription);
                return;
            }
        }

        Console.WriteLine($"[OnSelectedNodeChanged] Loading messages for queue: {fullPath}");
        _ = LoadMessagesAsync(fullPath, subscription);
    }
    
    private async Task LoadMessagesAsync(string fullPath, string? subscription)
    {
        try
        {
            Console.WriteLine($"[LoadMessagesAsync] Starting to load messages from: {fullPath}, subscription: {subscription}");
            IsLoading = true;
            LoadingMessage = $"Loading messages from: {fullPath}";
            ErrorMessage = null;
            
            if (MessageList != null)
            {
                await MessageList.LoadAsync(fullPath, subscription);
                Console.WriteLine($"[LoadMessagesAsync] Loaded {MessageList.MessageCount} messages");
                
                // Update message counts in the selected node
                if (SelectedNode != null)
                {
                    SelectedNode.ActiveMessageCount = MessageList.ActiveCount;
                    SelectedNode.DeadLetterMessageCount = MessageList.DeadLetterCount;
                    SelectedNode.MessageCountsLoaded = true;
                }
            }
            else
            {
                Console.WriteLine("[LoadMessagesAsync] MessageList is null!");
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error loading messages: {ex.Message}";
            Console.WriteLine($"[LoadMessagesAsync] Error loading messages: {ex}");
        }
        finally
        {
            IsLoading = false;
            LoadingMessage = string.Empty;
            Console.WriteLine("[LoadMessagesAsync] Finished loading");
        }
    }
    
    private string GetFullPath(NamespaceNode node)
    {
        return node.FullPath;
    }

    private async Task OpenConnectDialogAsync()
    {
        _logService.LogInfo("MainWindowViewModel", "Opening connection dialog");

        var dialogVm = _dialogVmFactory();
        var dialog   = new ConnectDialog { DataContext = dialogVm };

        var owner = (Application.Current?.ApplicationLifetime as
                     IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        var result = await dialog.ShowDialog<(IReadOnlyList<NamespaceEntity>? entities, ServiceBusAuthContext? authContext)>(owner!);
        if (result.entities is null || result.authContext is null)
        {
            _logService.LogInfo("MainWindowViewModel", "Connection dialog cancelled");
            return;
        }

        try
        {
            IsConnecting = true;
            ErrorMessage = null;
            ConnectionProgress = "Validating connection...";

            // Save auth context
            _currentAuthContext = result.authContext;

            _logService.LogInfo("MainWindowViewModel", "Connecting to Service Bus namespace");

            ConnectionProgress = "Creating service client...";
            // Create ViewModel for right pane
            MessageList = _msgVmFactory(_currentAuthContext);

            ConnectionProgress = "Loading entities...";
            // Create INamespaceProvider and NamespaceService to get hierarchical nodes
            await using var provider = _providerFactory(_currentAuthContext);
            var namespaceService = new NamespaceService(provider, _currentAuthContext);
            var nodes = await namespaceService.GetNodesAsync(includeMessageCounts: false);
            
            Nodes.Clear();
            foreach (var n in nodes)
            {
                Nodes.Add(n);
            }

            _logService.LogInfo("MainWindowViewModel", $"Successfully connected. Found {Nodes.Count} root nodes");

            // Do not automatically select and load messages
            // User should manually select a queue or topic
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error connecting: {ex.Message}";
            _logService.LogError("MainWindowViewModel", "Error connecting to Service Bus", ex);
        }
        finally
        {
            IsConnecting = false;
            ConnectionProgress = string.Empty;
        }
    }
    
    private async Task LoadNodeMessageCountsAsync(NamespaceNode? node)
    {
        if (node == null || _currentAuthContext == null)
        {
            return;
        }

        _logService.LogInfo("MainWindowViewModel", $"LoadNodeMessageCountsAsync called for node: {node.Name}, Children: {node.Children.Count}");

        try
        {
            var adminClient = _currentAuthContext.CreateAdminClient();
            await using var provider = _providerFactory(_currentAuthContext);
            
            // For topic nodes, load subscriptions first if not already loaded
            // Check if we have a placeholder child (Loading...)
            if (node.IsTopic && (node.Children.Count == 0 || 
                (node.Children.Count == 1 && node.Children[0].FullPath.EndsWith("__placeholder__"))))
            {
                _logService.LogInfo("MainWindowViewModel", $"Loading subscriptions for topic: {node.FullPath}");
                try
                {
                    var subscriptions = await provider.GetSubscriptionsAsync(node.FullPath);
                    
                    // Load subscriptions in parallel and check auto-forwarding
                    var subscriptionTasks = subscriptions.Select(async subscriptionName =>
                    {
                        var subscriptionNode = new NamespaceNode
                        {
                            Name = subscriptionName,
                            FullPath = $"{node.FullPath}/{subscriptionName}",
                            Parent = node,
                            EntityType = NamespaceEntity.EntityType.Subscription
                        };
                        
                        // Check if subscription has auto-forwarding enabled
                        try
                        {
                            var subscriptionProps = await provider.GetSubscriptionPropertiesAsync(node.FullPath, subscriptionName);
                            if (subscriptionProps != null && !string.IsNullOrEmpty(subscriptionProps.ForwardTo))
                            {
                                subscriptionNode.HasAutoForwarding = true;
                                _logService.LogInfo("MainWindowViewModel", $"Subscription {subscriptionName} has auto-forwarding enabled");
                            }
                        }
                        catch
                        {
                            // Ignore errors when checking auto-forwarding
                        }
                        
                        return subscriptionNode;
                    }).ToList();
                    
                    // Clear placeholder before adding real subscriptions
                    node.Children.Clear();
                    
                    var loadedSubscriptions = await Task.WhenAll(subscriptionTasks);
                    foreach (var subscriptionNode in loadedSubscriptions)
                    {
                        node.Children.Add(subscriptionNode);
                    }
                    
                    _logService.LogInfo("MainWindowViewModel", $"Loaded {node.Children.Count} subscriptions for topic: {node.FullPath}");
                }
                catch (Exception ex)
                {
                    _logService.LogError("MainWindowViewModel", $"Error loading subscriptions for topic {node.FullPath}: {ex.Message}");
                }
            }
            
            // Load message counts for direct child nodes only (not recursive)
            // Process in parallel for better performance
            var tasks = new List<Task>();
            
            foreach (var child in node.Children)
            {
                if (child.IsQueue && !child.MessageCountsLoaded)
                {
                    tasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            // First check if the queue has auto-forwarding enabled (if not already checked)
                            if (!child.HasAutoForwarding && !child.MessageCountsLoaded)
                            {
                                var queueProps = await provider.GetQueuePropertiesAsync(child.FullPath);
                                if (queueProps != null && !string.IsNullOrEmpty(queueProps.ForwardTo))
                                {
                                    child.HasAutoForwarding = true;
                                    _logService.LogInfo("MainWindowViewModel", $"Queue {child.Name} has auto-forwarding enabled, skipping message count");
                                    return;
                                }
                            }
                            
                            if (!child.HasAutoForwarding)
                            {
                                var properties = await adminClient.GetQueueRuntimePropertiesAsync(child.FullPath);
                                child.ActiveMessageCount = (int)properties.Value.ActiveMessageCount;
                                child.DeadLetterMessageCount = (int)properties.Value.DeadLetterMessageCount;
                                child.MessageCountsLoaded = true;
                                
                                _logService.LogInfo("MainWindowViewModel", $"Loaded counts for {child.Name}: Active={child.ActiveMessageCount}, DLQ={child.DeadLetterMessageCount}");
                            }
                        }
                        catch (Exception ex)
                        {
                            _logService.LogError("MainWindowViewModel", $"Failed to load message counts for {child.FullPath}: {ex.Message}");
                        }
                    }));
                }
                else if (child.IsSubscription && !child.MessageCountsLoaded && child.Parent != null)
                {
                    tasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            // For subscriptions, get message counts from parent topic
                            var topicPath = child.Parent.FullPath;
                            
                            // First check if the subscription has auto-forwarding enabled
                            var subscriptionProps = await provider.GetSubscriptionPropertiesAsync(topicPath, child.Name);
                            if (subscriptionProps != null && !string.IsNullOrEmpty(subscriptionProps.ForwardTo))
                            {
                                child.HasAutoForwarding = true;
                                _logService.LogInfo("MainWindowViewModel", $"Subscription {child.Name} has auto-forwarding enabled, skipping message count");
                                return;
                            }
                            
                            var properties = await adminClient.GetSubscriptionRuntimePropertiesAsync(topicPath, child.Name);
                            child.ActiveMessageCount = (int)properties.Value.ActiveMessageCount;
                            child.DeadLetterMessageCount = (int)properties.Value.DeadLetterMessageCount;
                            child.MessageCountsLoaded = true;
                            
                            _logService.LogInfo("MainWindowViewModel", $"Loaded counts for subscription {child.Name}: Active={child.ActiveMessageCount}, DLQ={child.DeadLetterMessageCount}");
                        }
                        catch (Exception ex)
                        {
                            _logService.LogError("MainWindowViewModel", $"Failed to load message counts for subscription {child.Name}: {ex.Message}");
                        }
                    }));
                }
            }
            
            // Wait for all tasks to complete
            await Task.WhenAll(tasks);
        }
        catch (Exception ex)
        {
            _logService.LogError("MainWindowViewModel", $"Error loading message counts: {ex.Message}");
        }
    }
    
    private async Task RefreshNodeAsync(NamespaceNode? node)
    {
        if (node is null || node.EntityType is null || MessageList is null)
        {
            return;
        }

        _logService.LogInfo("MainWindowViewModel", $"User initiated refresh for node: {node.FullPath}");
            
        try
        {
            IsLoading = true;
            LoadingMessage = $"Refreshing: {node.FullPath}";
            ErrorMessage = null;
            
            // Get subscription name if this is a subscription node
            string? subscription = null;
            var queueOrTopic = node.FullPath;
            
            if (node.IsSubscription && node.Parent?.IsTopic == true)
            {
                subscription = node.Name;
                queueOrTopic = node.Parent.FullPath; // Use parent topic path
            }
            
            // Reload messages
            await MessageList.LoadAsync(queueOrTopic, subscription);
            
            // Update message counts in the tree node
            node.ActiveMessageCount = MessageList.ActiveCount;
            node.DeadLetterMessageCount = MessageList.DeadLetterCount;
            node.MessageCountsLoaded = true;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error refreshing: {ex.Message}";
            Console.WriteLine($"Error refreshing messages: {ex}");
        }
        finally
        {
            IsLoading = false;
            LoadingMessage = string.Empty;
        }
    }
}
