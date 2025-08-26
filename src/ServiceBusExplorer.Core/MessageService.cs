using ServiceBusExplorer.Infrastructure;
using ServiceBusExplorer.Infrastructure.Models;

namespace ServiceBusExplorer.Core;

/// <summary>
///     Facade over <see cref="IMessagePeekProvider"/> that hides provider
///     instantiation details from UI layers.  Given a connection string and an
///     entity path (queue or topic[/subscription]), it returns the latest
///     messages via <c>PeekAsync</c>.
/// </summary>
public sealed class MessageService(
    Func<string, IMessagePeekProvider> providerFactory,
    Func<string, IMessageDeleteProvider>? deleteProviderFactory = null,
    Func<string, IMessagePurgeProvider>? purgeProviderFactory = null,
    Func<string, IMessageResubmitProvider>? resubmitProviderFactory = null)
{
    private readonly Func<string, IMessagePeekProvider> _providerFactory = providerFactory;
    private readonly Func<string, IMessageDeleteProvider> _deleteProviderFactory = deleteProviderFactory ?? (cs => new AzureMessageDeleteProvider(cs));
    private readonly Func<string, IMessagePurgeProvider> _purgeProviderFactory = purgeProviderFactory ?? (cs => new AzureMessagePurgeProvider(cs));
    private readonly Func<string, IMessageResubmitProvider> _resubmitProviderFactory = resubmitProviderFactory ?? (cs => new AzureMessageResubmitProvider(cs));

    /// <summary>
    ///     Peeks up to <paramref name="count"/> messages from the specified
    ///     entity.
    /// </summary>
    /// <param name="connectionString">Namespace connection string.</param>
    /// <param name="queueOrTopic">Queue name or Topic name.</param>
    /// <param name="subscription">
    ///     Subscription name when peeking a topic, otherwise <c>null</c>.
    /// </param>
    /// <param name="count">Maximum messages to peek.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<IReadOnlyList<ServiceBusReceivedMessageDto>> PeekAsync(
        string connectionString,
        string queueOrTopic,
        string? subscription = null,
        int count = 50,
        CancellationToken ct = default)
    {
        await using var provider = _providerFactory(connectionString);
        return await provider.PeekAsync(queueOrTopic, subscription, count, ct);
    }

    /// <summary>
    ///     Peeks up to <paramref name="count"/> messages from the dead-letter
    ///     sub-queue of the specified entity.
    /// </summary>
    public async Task<IReadOnlyList<ServiceBusReceivedMessageDto>> PeekDeadLetterAsync(
        string connectionString,
        string queueOrTopic,
        string? subscription = null,
        int count = 50,
        CancellationToken ct = default)
    {
        await using var provider = _providerFactory(connectionString);
        return await provider.PeekDeadLetterAsync(queueOrTopic, subscription, count, ct);
    }

    /// <summary>
    ///     Peeks messages from both active and dead-letter queues.
    /// </summary>
    public async Task<IReadOnlyList<ServiceBusReceivedMessageDto>> PeekAllAsync(
        string connectionString,
        string queueOrTopic,
        string? subscription = null,
        int count = 50,
        CancellationToken ct = default)
    {
        await using var provider = _providerFactory(connectionString);

        var activeTask = provider.PeekAsync(queueOrTopic, subscription, count, ct);
        var deadLetterTask = provider.PeekDeadLetterAsync(queueOrTopic, subscription, count, ct);

        await Task.WhenAll(activeTask, deadLetterTask);

        var allMessages = new List<ServiceBusReceivedMessageDto>();
        allMessages.AddRange(await activeTask);
        allMessages.AddRange(await deadLetterTask);

        return allMessages;
    }

    /// <summary>
    ///     Retrieves paged messages from the specified queue or topic.
    /// </summary>
    public async Task<PagedResult<ServiceBusReceivedMessageDto>> GetPagedMessagesAsync(
        string connectionString,
        string queueOrTopic,
        string? subscription,
        int pageNumber,
        int pageSize = 50,
        bool activeOnly = false,
        bool deadLetterOnly = false,
        CancellationToken ct = default)
    {
        await using var provider = _providerFactory(connectionString);

        // Handle different filter cases
        if (deadLetterOnly)
        {
            // Only dead letter messages
            return await provider.PeekDeadLetterPagedAsync(queueOrTopic, subscription, pageNumber, pageSize, ct);
        }
        else if (activeOnly)
        {
            // Only active messages
            return await provider.PeekPagedAsync(queueOrTopic, subscription, pageNumber, pageSize, ct);
        }
        else
        {
            // Combined view (All) - need to implement proper pagination
            // For now, we'll handle page 1 specially
            if (pageNumber == 1)
            {
                // Get counts first
                var (activeCount, deadLetterCount) = await provider.GetMessageCountsAsync(queueOrTopic, subscription, ct);
                var totalCount = activeCount + deadLetterCount;

                // Get both active and dead letter messages
                var allMessages = new List<ServiceBusReceivedMessageDto>();

                // Get active messages first
                var activeResult = await provider.PeekPagedAsync(queueOrTopic, subscription, 1, pageSize, ct);
                allMessages.AddRange(activeResult.Items);

                // If we have room, get dead letter messages
                if (allMessages.Count < pageSize && deadLetterCount > 0)
                {
                    var remainingSize = pageSize - allMessages.Count;
                    var deadLetterResult = await provider.PeekDeadLetterPagedAsync(queueOrTopic, subscription, 1, remainingSize, ct);
                    allMessages.AddRange(deadLetterResult.Items);
                }

                return new PagedResult<ServiceBusReceivedMessageDto>
                {
                    Items = allMessages,
                    TotalCount = totalCount,
                    PageNumber = 1,
                    PageSize = pageSize
                };
            }
            else
            {
                // For other pages in combined view, calculate which messages to show
                var (activeCount, deadLetterCount) = await provider.GetMessageCountsAsync(queueOrTopic, subscription, ct);
                var totalCount = activeCount + deadLetterCount;
                var skipCount = (pageNumber - 1) * pageSize;

                var allMessages = new List<ServiceBusReceivedMessageDto>();

                if (skipCount < activeCount)
                {
                    // We're still in active messages
                    var activePageNumber = (skipCount / pageSize) + 1;
                    var activeResult = await provider.PeekPagedAsync(queueOrTopic, subscription, activePageNumber, pageSize, ct);

                    // Adjust for partial page
                    var startIndex = skipCount % pageSize;
                    var itemsToTake = Math.Min(pageSize, activeResult.Items.Count - startIndex);
                    if (startIndex < activeResult.Items.Count)
                    {
                        allMessages.AddRange(activeResult.Items.Skip(startIndex).Take(itemsToTake));
                    }

                    // If we need more items and have dead letters
                    if (allMessages.Count < pageSize && deadLetterCount > 0)
                    {
                        var remainingSize = pageSize - allMessages.Count;
                        var deadLetterResult = await provider.PeekDeadLetterPagedAsync(queueOrTopic, subscription, 1, remainingSize, ct);
                        allMessages.AddRange(deadLetterResult.Items);
                    }
                }
                else
                {
                    // We're in dead letter messages
                    var deadLetterSkip = skipCount - activeCount;
                    var deadLetterPageNumber = (deadLetterSkip / pageSize) + 1;
                    var deadLetterResult = await provider.PeekDeadLetterPagedAsync(queueOrTopic, subscription, deadLetterPageNumber, pageSize, ct);

                    // Adjust for partial page
                    var startIndex = deadLetterSkip % pageSize;
                    if (startIndex < deadLetterResult.Items.Count)
                    {
                        allMessages.AddRange(deadLetterResult.Items.Skip(startIndex).Take(pageSize));
                    }
                }

                return new PagedResult<ServiceBusReceivedMessageDto>
                {
                    Items = allMessages,
                    TotalCount = totalCount,
                    PageNumber = pageNumber,
                    PageSize = pageSize
                };
            }
        }
    }

    /// <summary>
    ///     Gets the total count of messages in active and dead letter queues.
    /// </summary>
    public async Task<(int activeCount, int deadLetterCount)> GetMessageCountsAsync(
        string connectionString,
        string queueOrTopic,
        string? subscription,
        CancellationToken ct = default)
    {
        await using var provider = _providerFactory(connectionString);
        return await provider.GetMessageCountsAsync(queueOrTopic, subscription, ct);
    }

    /// <summary>
    ///     Resubmits a message to the specified queue or topic.
    /// </summary>
    /// <param name="connectionString">Namespace connection string.</param>
    /// <param name="queueOrTopic">Queue name or Topic name.</param>
    /// <param name="messageBody">Message body to send.</param>
    /// <param name="contentType">Content type of the message.</param>
    /// <param name="label">Message label/subject.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task ResubmitMessageAsync(
        string connectionString,
        string queueOrTopic,
        string messageBody,
        string? contentType = null,
        string? label = null,
        CancellationToken ct = default)
    {
        await using var sendProvider = new AzureMessageSendProvider(connectionString);
        await sendProvider.SendMessageAsync(
            queueOrTopic,
            null,
            messageBody,
            null,
            contentType,
            label,
            ct);
    }

    /// <summary>
    ///     Deletes a message from the dead letter queue.
    /// </summary>
    /// <param name="connectionString">Namespace connection string.</param>
    /// <param name="queueOrTopic">Queue name or Topic name.</param>
    /// <param name="subscription">Subscription name if it's a topic.</param>
    /// <param name="messageId">Message ID to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task DeleteActiveMessageAsync(
        string connectionString,
        string queueOrTopic,
        string? subscription,
        string messageId,
        CancellationToken ct = default)
    {
        await using var deleteProvider = _deleteProviderFactory(connectionString);
        await deleteProvider.DeleteActiveMessageAsync(
            queueOrTopic,
            subscription,
            messageId,
            ct);
    }

    public async Task DeleteDeadLetterMessageAsync(
        string connectionString,
        string queueOrTopic,
        string? subscription,
        string messageId,
        CancellationToken ct = default)
    {
        await using var deleteProvider = _deleteProviderFactory(connectionString);
        await deleteProvider.DeleteDeadLetterMessageAsync(
            queueOrTopic,
            subscription,
            messageId,
            ct);
    }

    /// <summary>
    ///     Purges messages from a queue or topic.
    /// </summary>
    /// <param name="connectionString">Namespace connection string.</param>
    /// <param name="queueOrTopic">Queue name or Topic name.</param>
    /// <param name="subscription">Subscription name if it's a topic.</param>
    /// <param name="option">Which messages to purge (All, ActiveOnly, DeadLetterOnly).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Total number of messages purged.</returns>
    public async Task<int> PurgeMessagesAsync(
        string connectionString,
        string queueOrTopic,
        string? subscription,
        PurgeOption option,
        CancellationToken ct = default)
    {
        await using var purgeProvider = _purgeProviderFactory(connectionString);

        var purgedCount = 0;

        if (option == PurgeOption.All || option == PurgeOption.ActiveOnly)
        {
            purgedCount += await purgeProvider.PurgeActiveMessagesAsync(
                queueOrTopic,
                subscription,
                ct);
        }

        if (option == PurgeOption.All || option == PurgeOption.DeadLetterOnly)
        {
            purgedCount += await purgeProvider.PurgeDeadLetterMessagesAsync(
                queueOrTopic,
                subscription,
                ct);
        }

        return purgedCount;
    }

    /// <summary>
    ///     Resubmits a dead letter message back to the active queue.
    /// </summary>
    public async Task ResubmitDeadLetterMessageAsync(
        string connectionString,
        string queueOrTopic,
        string messageId,
        string? subscription = null,
        CancellationToken ct = default)
    {
        await using var resubmitProvider = _resubmitProviderFactory(connectionString);
        await resubmitProvider.ResubmitMessageAsync(
            queueOrTopic,
            messageId,
            subscription,
            ct);
    }
}
