namespace ServiceBusExplorer.Infrastructure;

public interface IMessageDeleteProvider
{
    Task DeleteActiveMessageAsync(
        string queueOrTopic,
        string? subscription,
        string messageId,
        CancellationToken cancellationToken = default);

    Task DeleteDeadLetterMessageAsync(
        string queueOrTopic,
        string? subscription,
        string messageId,
        CancellationToken cancellationToken = default);

    ValueTask DisposeAsync();
}
