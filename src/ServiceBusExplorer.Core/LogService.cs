using ServiceBusExplorer.Core.Models;

namespace ServiceBusExplorer.Core;

public interface ILogService
{
    event EventHandler<LogEntry>? LogAdded;
    void Log(LogLevel level, string source, string message, Exception? exception = null);
    void LogInfo(string source, string message);
    void LogWarning(string source, string message);
    void LogError(string source, string message, Exception? exception = null);
    IEnumerable<LogEntry> GetLogs();
    void Clear();
}

public class LogService : ILogService
{
    private readonly List<LogEntry> _logs = [];
    private readonly object _lock = new();
    private const int MaxLogEntries = 10000;

    public event EventHandler<LogEntry>? LogAdded;

    public void Log(LogLevel level, string source, string message, Exception? exception = null)
    {
        var entry = new LogEntry(level, source, message, exception);

        lock (_lock)
        {
            _logs.Add(entry);

            // Remove old entries if we exceed the maximum
            while (_logs.Count > MaxLogEntries)
            {
                _logs.RemoveAt(0);
            }
        }

        // Also write to console for debugging
        var consoleMessage = $"[{entry.Timestamp:HH:mm:ss}] [{entry.Level}] [{entry.Source}] {entry.Message}";
        if (exception != null)
        {
            consoleMessage += $"\n{exception}";
        }
        Console.WriteLine(consoleMessage);

        // Raise event
        LogAdded?.Invoke(this, entry);
    }

    public void LogInfo(string source, string message)
    {
        Log(LogLevel.Info, source, message);
    }

    public void LogWarning(string source, string message)
    {
        Log(LogLevel.Warning, source, message);
    }

    public void LogError(string source, string message, Exception? exception = null)
    {
        Log(LogLevel.Error, source, message, exception);
    }

    public IEnumerable<LogEntry> GetLogs()
    {
        lock (_lock)
        {
            return [.. _logs];
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _logs.Clear();
        }
    }
}
