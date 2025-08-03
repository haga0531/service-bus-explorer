namespace ServiceBusExplorer.Core.Models;

public class LogEntry(LogLevel level, string source, string message, Exception? exception = null)
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public LogLevel Level { get; set; } = level;
    public string Source { get; set; } = source;
    public string Message { get; set; } = message;
    public Exception? Exception { get; set; } = exception;
}

public enum LogLevel
{
    Info,
    Warning,
    Error
}
