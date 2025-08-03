namespace ServiceBusExplorer.Infrastructure;

public interface IMessageSendProvider : IAsyncDisposable
{
    Task SendMessageAsync(
        string queueOrTopic,
        string? subscription,
        string messageBody,
        Dictionary<string, object>? properties = null,
        string? contentType = null,
        string? label = null,
        CancellationToken ct = default);
        
    Task SendMessagesAsync(
        string queueOrTopic,
        string? subscription,
        IEnumerable<ServiceBusMessage> messages,
        CancellationToken ct = default);
}

public record ServiceBusMessage(
    string Body,
    Dictionary<string, object>? Properties = null,
    string? ContentType = null,
    string? Label = null); 
