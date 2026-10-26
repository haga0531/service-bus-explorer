using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace ServiceBusExplorer.UI.Models;

public record MessageProperty(string Key, string Value);

public class MessageViewModel(
    string messageId,
    string label,
    string contentType,
    DateTimeOffset enqueuedTime,
    string body,
    bool isDeadLetter = false,
    IReadOnlyDictionary<string, string>? applicationProperties = null) : INotifyPropertyChanged
{
    private bool _isSelected;

    public string MessageId { get; } = messageId;
    public string Label { get; } = label;
    public string ContentType { get; } = contentType;
    public DateTimeOffset EnqueuedTime { get; } = enqueuedTime;
    public string Body { get; } = body;
    public bool IsDeadLetter { get; } = isDeadLetter;
    public string Status => IsDeadLetter ? "Dead Letter" : "Active";
    public string StatusColor => IsDeadLetter ? "Red" : "Green";

    public IReadOnlyDictionary<string, string> ApplicationProperties { get; } =
        applicationProperties ?? new Dictionary<string, string>();
    public IReadOnlyList<MessageProperty> Properties =>
        [.. ApplicationProperties.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase).Select(p => new MessageProperty(p.Key, p.Value))];
    public bool HasProperties => ApplicationProperties.Count > 0;

    // Service Bus stores the dead-letter reason as application properties
    public string? DeadLetterReason => ApplicationProperties.GetValueOrDefault("DeadLetterReason");
    public string? DeadLetterErrorDescription => ApplicationProperties.GetValueOrDefault("DeadLetterErrorDescription");
    public bool HasDeadLetterReason =>
        !string.IsNullOrEmpty(DeadLetterReason) || !string.IsNullOrEmpty(DeadLetterErrorDescription);

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
