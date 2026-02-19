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
    Func<ServiceBusAuthContext, IMessagePeekProvider> providerFactory,
    Func<ServiceBusAuthContext, IMessageDeleteProvider>? deleteProviderFactory = null,
    Func<ServiceBusAuthContext, IMessagePurgeProvider>? purgeProviderFactory = null,
    Func<ServiceBusAuthContext, IMessageResubmitProvider>? resubmitProviderFactory = null)
{
    private readonly Func<ServiceBusAuthContext, IMessagePeekProvider> _providerFactory = providerFactory;
    private readonly Func<ServiceBusAuthContext, IMessageDeleteProvider> _deleteProviderFactory = deleteProviderFactory ?? (authContext => new AzureMessageDeleteProvider(authContext));
    private readonly Func<ServiceBusAuthContext, IMessagePurgeProvider> _purgeProviderFactory = purgeProviderFactory ?? (authContext => new AzureMessagePurgeProvider(authContext));
    private readonly Func<ServiceBusAuthContext, IMessageResubmitProvider> _resubmitProviderFactory = resubmitProviderFactory ?? (authContext => new AzureMessageResubmitProvider(authContext));

    /// <summary>
    ///     Peeks up to <paramref name="count"/> messages from the specified
    ///     entity.
    /// </summary>
    /// <param name="authContext">The authentication context to work in.</param>
    /// <param name="queueOrTopic">Queue name or Topic name.</param>
    /// <param name="subscription">
    ///     Subscription name when peeking a topic, otherwise <c>null</c>.
    /// </param>
    /// <param name="count">Maximum messages to peek.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<IReadOnlyList<ServiceBusReceivedMessageDto>> PeekAsync(
        ServiceBusAuthContext authContext,
        string queueOrTopic,
        string? subscription = null,
        int count            = 50,
        CancellationToken ct = default)
    {
        await using var provider = _providerFactory(authContext);
        return await provider.PeekAsync(queueOrTopic, subscription, count, ct);
    }

    /// <summary>
    ///     Peeks up to <paramref name="count"/> messages from the dead-letter
    ///     sub-queue of the specified entity.
    /// </summary>
    public async Task<IReadOnlyList<ServiceBusReceivedMessageDto>> PeekDeadLetterAsync(
        ServiceBusAuthContext authContext,
        string queueOrTopic,
        string? subscription = null,
        int count            = 50,
        CancellationToken ct = default)
    {
        await using var provider = _providerFactory(authContext);
        return await provider.PeekDeadLetterAsync(queueOrTopic, subscription, count, ct);
    }

    /// <summary>
    ///     Peeks messages from both active and dead-letter queues.
    /// </summary>
    public async Task<IReadOnlyList<ServiceBusReceivedMessageDto>> PeekAllAsync(
        ServiceBusAuthContext authContext,
        string queueOrTopic,
        string? subscription = null,
        int count            = 50,
        CancellationToken ct = default)
    {
        await using var provider = _providerFactory(authContext);

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
        ServiceBusAuthContext authContext,
        string queueOrTopic,
        string? subscription,
        int pageNumber,
        int pageSize = 50,
        bool activeOnly = false,
        bool deadLetterOnly = false,
        CancellationToken ct = default)
    {
        await using var provider = _providerFactory(authContext);

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
        ServiceBusAuthContext authContext,
        string queueOrTopic,
        string? subscription,
        CancellationToken ct = default)
    {
        await using var provider = _providerFactory(authContext);
        return await provider.GetMessageCountsAsync(queueOrTopic, subscription, ct);
    }

    /// <summary>
    ///     Resubmits a message to the specified queue or topic.
    /// </summary>
    /// <param name="authContext">The authentication context to work in.</param>
    /// <param name="queueOrTopic">Queue name or Topic name.</param>
    /// <param name="messageBody">Message body to send.</param>
    /// <param name="contentType">Content type of the message.</param>
    /// <param name="label">Message label/subject.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task ResubmitMessageAsync(
        ServiceBusAuthContext authContext,
        string queueOrTopic,
        string messageBody,
        string? contentType = null,
        string? label = null,
        CancellationToken ct = default)
    {
        await using var sendProvider = new AzureMessageSendProvider(authContext);
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
    /// <param name="authContext">The authentication context to work in.</param>
    /// <param name="queueOrTopic">Queue name or Topic name.</param>
    /// <param name="subscription">Subscription name if it's a topic.</param>
    /// <param name="messageId">Message ID to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task DeleteActiveMessageAsync(
        ServiceBusAuthContext authContext,
        string queueOrTopic,
        string? subscription,
        string messageId,
        CancellationToken ct = default)
    {
        await using var deleteProvider = _deleteProviderFactory(authContext);
        await deleteProvider.DeleteActiveMessageAsync(
            queueOrTopic,
            subscription,
            messageId,
            ct);
    }

    public async Task DeleteDeadLetterMessageAsync(
        ServiceBusAuthContext authContext,
        string queueOrTopic,
        string? subscription,
        string messageId,
        CancellationToken ct = default)
    {
        await using var deleteProvider = _deleteProviderFactory(authContext);
        await deleteProvider.DeleteDeadLetterMessageAsync(
            queueOrTopic,
            subscription,
            messageId,
            ct);
    }

    /// <summary>
    ///     Purges messages from a queue or topic.
    /// </summary>
    /// <param name="authContext">The authentication context to work in.</param>
    /// <param name="queueOrTopic">Queue name or Topic name.</param>
    /// <param name="subscription">Subscription name if it's a topic.</param>
    /// <param name="option">Which messages to purge (All, ActiveOnly, DeadLetterOnly).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Total number of messages purged.</returns>
    public async Task<int> PurgeMessagesAsync(
        ServiceBusAuthContext authContext,
        string queueOrTopic,
        string? subscription,
        PurgeOption option,
        CancellationToken ct = default)
    {
        await using var purgeProvider = _purgeProviderFactory(authContext);

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
        ServiceBusAuthContext authContext,
        string queueOrTopic,
        string messageId,
        string? subscription = null,
        bool deleteFromDeadLetter = true,
        CancellationToken ct = default)
    {
        await using var resubmitProvider = _resubmitProviderFactory(authContext);
        await resubmitProvider.ResubmitMessageAsync(
            queueOrTopic,
            messageId,
            subscription,
            deleteFromDeadLetter,
            ct);
    }
}
