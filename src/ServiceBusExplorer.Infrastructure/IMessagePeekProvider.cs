using ServiceBusExplorer.Infrastructure.Models;

namespace ServiceBusExplorer.Infrastructure;

public interface IMessagePeekProvider : IAsyncDisposable
{
    Task<IReadOnlyList<ServiceBusReceivedMessageDto>> PeekAsync(
        string queueOrTopic,
        string? subscription,
        int maxMessages = 50,
        CancellationToken ct = default);

    Task<IReadOnlyList<ServiceBusReceivedMessageDto>> PeekDeadLetterAsync(
        string queueOrTopic,
        string? subscription,
        int maxMessages = 50,
        CancellationToken ct = default);

    Task<PagedResult<ServiceBusReceivedMessageDto>> PeekPagedAsync(
        string queueOrTopic,
        string? subscription,
        int pageNumber,
        int pageSize = 50,
        CancellationToken ct = default);

    Task<PagedResult<ServiceBusReceivedMessageDto>> PeekDeadLetterPagedAsync(
        string queueOrTopic,
        string? subscription,
        int pageNumber,
        int pageSize = 50,
        CancellationToken ct = default);

    Task<(int activeCount, int deadLetterCount)> GetMessageCountsAsync(
        string queueOrTopic,
        string? subscription,
        CancellationToken ct = default);

    /// <summary>
    ///     Gets the runtime properties for a queue or subscription using Management API.
    ///     This is much more efficient than peeking messages.
    /// </summary>
    Task<(long totalCount, long activeCount, long deadLetterCount, long scheduledCount)> GetRuntimePropertiesAsync(
        string queueOrTopic,
        string? subscription,
        CancellationToken ct = default);
}

public record ServiceBusReceivedMessageDto(
    string MessageId,
    string Label,
    string ContentType,
    DateTimeOffset EnqueuedTime,
    string Body,
    bool IsDeadLetter = false);
