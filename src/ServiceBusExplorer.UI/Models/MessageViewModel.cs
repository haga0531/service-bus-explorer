using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ServiceBusExplorer.UI.Models;

public class MessageViewModel(
    string messageId,
    string label,
    string contentType,
    DateTimeOffset enqueuedTime,
    string body,
    bool isDeadLetter = false) : INotifyPropertyChanged
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
