namespace ServiceBusExplorer.Infrastructure;

public interface IMessageResubmitProvider : IAsyncDisposable
{
    /// <summary>
    /// Resubmits a message from the dead letter queue back to the active queue.
    /// </summary>
    Task ResubmitMessageAsync(
        string queueOrTopic,
        string messageId,
        string? subscription = null,
        bool deleteFromDeadLetter = true,
        CancellationToken cancellationToken = default);
}
