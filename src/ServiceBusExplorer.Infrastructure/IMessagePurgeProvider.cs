namespace ServiceBusExplorer.Infrastructure;

public interface IMessagePurgeProvider
{
    Task<int> PurgeActiveMessagesAsync(
        string queueOrTopic,
        string? subscription,
        CancellationToken cancellationToken = default);

    Task<int> PurgeDeadLetterMessagesAsync(
        string queueOrTopic,
        string? subscription,
        CancellationToken cancellationToken = default);

    ValueTask DisposeAsync();
}
