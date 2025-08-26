using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ServiceBusExplorer.Core.Models;

public class NamespaceNode : INotifyPropertyChanged
{
    public string Name { get; init; } = default!;
    public string FullPath { get; init; } = default!;  // フルパスを保持
    public NamespaceEntity.EntityType? EntityType { get; set; }   // null = フォルダ
    public System.Collections.ObjectModel.ObservableCollection<NamespaceNode> Children { get; } = [];
    public NamespaceNode? Parent { get; set; }
    public bool IsQueue => EntityType == NamespaceEntity.EntityType.Queue;
    public bool IsSubscription => EntityType == NamespaceEntity.EntityType.Subscription;
    public bool IsTopic => EntityType == NamespaceEntity.EntityType.Topic;
    public bool IsFolder => EntityType == null;
    private bool _hasAutoForwarding;
    public bool HasAutoForwarding
    {
        get => _hasAutoForwarding;
        set
        {
            if (_hasAutoForwarding != value)
            {
                _hasAutoForwarding = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayName));
            }
        }
    }

    // Icon property
    public string Icon
    {
        get
        {
            if (IsFolder)
            {
                // Folder icons
                return Name switch
                {
                    "Queues" => "📨",  // Inbox tray icon for queues
                    "Topics" => "📢",  // Megaphone icon for topics
                    _ => "📁"         // Default folder icon
                };
            }
            else if (IsQueue)
            {
                return "📬";  // Mailbox icon for individual queue
            }
            else if (IsTopic)
            {
                return "📡";  // Satellite antenna icon for individual topic
            }
            else if (IsSubscription)
            {
                return "📥";  // Inbox tray icon for subscription
            }
            else
            {
                return "📄";  // Document icon as default
            }
        }
    }

    // Message counts
    private int _activeMessageCount;
    private int _deadLetterMessageCount;
    private bool _messageCountsLoaded;

    public int ActiveMessageCount
    {
        get => _activeMessageCount;
        set
        {
            _activeMessageCount = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayName));
        }
    }

    public int DeadLetterMessageCount
    {
        get => _deadLetterMessageCount;
        set
        {
            _deadLetterMessageCount = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayName));
        }
    }

    public bool MessageCountsLoaded
    {
        get => _messageCountsLoaded;
        set
        {
            _messageCountsLoaded = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayName));
        }
    }

    public string DisplayName
    {
        get
        {
            if ((IsQueue || IsSubscription) && HasAutoForwarding)
            {
                return $"{Name} (Auto-forwarding)";
            }
            else if ((IsQueue || IsTopic || IsSubscription) && MessageCountsLoaded)
            {
                return $"{Name} ({ActiveMessageCount:N0}, {DeadLetterMessageCount:N0})";
            }
            else
            {
                return Name;
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
