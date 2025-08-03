using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ServiceBusExplorer.Core;
using ServiceBusExplorer.Core.Models;

namespace ServiceBusExplorer.UI;

public partial class LogViewModel : ObservableObject
{
    private readonly ILogService _logService;
    private readonly ObservableCollection<LogEntryViewModel> _allLogs = [];
    
    [ObservableProperty] private ObservableCollection<LogEntryViewModel> logs = [];
    [ObservableProperty] private string searchText = string.Empty;
    [ObservableProperty] private string selectedLogLevel = "All";

    [ObservableProperty] private bool isExpanded = true;
    [ObservableProperty] private double panelHeight = 200;
    
    public ObservableCollection<string> LogLevels { get; } = ["All", "Info", "Warning", "Error"];
    
    public IRelayCommand ClearLogsCommand { get; }
    public IRelayCommand ToggleExpandCommand { get; }
    
    public LogViewModel(ILogService logService)
    {
        _logService = logService;
        _logService.LogAdded += OnLogAdded;
        
        ClearLogsCommand = new RelayCommand(ClearLogs);
        ToggleExpandCommand = new RelayCommand(ToggleExpand);
        
        // Load existing logs
        foreach (var log in _logService.GetLogs())
        {
            _allLogs.Add(new LogEntryViewModel(log));
        }
        ApplyFilter();
    }
    
    private void OnLogAdded(object? sender, LogEntry e)
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            var logVm = new LogEntryViewModel(e);
            _allLogs.Add(logVm);
            
            // Remove old entries if we have too many
            while (_allLogs.Count > 10000)
            {
                _allLogs.RemoveAt(0);
            }
            
            ApplyFilter();
        });
    }
    
    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
    }
    
    partial void OnSelectedLogLevelChanged(string value)
    {
        ApplyFilter();
    }
    
    private void ApplyFilter()
    {
        var filtered = _allLogs.AsEnumerable();
        
        // Filter by log level
        if (SelectedLogLevel != "All")
        {
            if (Enum.TryParse<LogLevel>(SelectedLogLevel, out var level))
            {
                filtered = filtered.Where(l => l.Level == level);
            }
        }
        
        // Filter by search text
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var searchLower = SearchText.ToLowerInvariant();
            filtered = filtered.Where(l => 
                l.Message.ToLowerInvariant().Contains(searchLower) ||
                l.Source.ToLowerInvariant().Contains(searchLower));
        }
        
        Logs.Clear();
        foreach (var log in filtered)
        {
            Logs.Add(log);
        }
    }
    
    private void ClearLogs()
    {
        _logService.Clear();
        _allLogs.Clear();
        Logs.Clear();
    }
    
    private void ToggleExpand()
    {
        IsExpanded = !IsExpanded;
        if (!IsExpanded)
        {
            PanelHeight = 30; // Just show header
        }
        else
        {
            PanelHeight = 200;
        }
    }
}

public class LogEntryViewModel(LogEntry entry)
{
    public LogEntry Entry { get; } = entry;
    public DateTime Timestamp => Entry.Timestamp;
    public LogLevel Level => Entry.Level;
    public string Source => Entry.Source;
    public string Message => Entry.Message;
    public string FormattedTime => Timestamp.ToString("HH:mm:ss.fff");
    public string FormattedMessage => Entry.Exception != null 
        ? $"{Message}\n{Entry.Exception}" 
        : Message;
}
