using System.IO.Compression;
using System.Text;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using ServiceBusExplorer.Infrastructure.Models;

namespace ServiceBusExplorer.Infrastructure;

public sealed class AzureMessagePeekProvider : IMessagePeekProvider
{
    private readonly ServiceBusClient _client;
    private readonly ServiceBusAdministrationClient _adminClient;

    public AzureMessagePeekProvider(ServiceBusAuthContext authContext)
    {
        ArgumentNullException.ThrowIfNull(authContext);
        _client = authContext.CreateServiceBusClient();
        _adminClient = authContext.CreateAdminClient();
    }

    public async Task<IReadOnlyList<ServiceBusReceivedMessageDto>> PeekAsync(
        string queueOrTopic,
        string? subscription,
        int maxMessages = 50,
        CancellationToken ct = default)
    {
        var receiver = subscription is null
            ? _client.CreateReceiver(queueOrTopic, new ServiceBusReceiverOptions
                { ReceiveMode = ServiceBusReceiveMode.PeekLock })
            : _client.CreateReceiver(queueOrTopic, subscription,
                new ServiceBusReceiverOptions { ReceiveMode = ServiceBusReceiveMode.PeekLock });

        var messages = await receiver.PeekMessagesAsync(maxMessages, cancellationToken: ct);

        return [.. messages.Select(m => new ServiceBusReceivedMessageDto(
                m.MessageId,
                m.Subject ?? string.Empty,
                m.ContentType ?? string.Empty,
                m.EnqueuedTime,
                ProcessMessageBody(m.Body, m.ContentType),
                false,
                m.SessionId))];
    }

    public async Task<IReadOnlyList<ServiceBusReceivedMessageDto>> PeekDeadLetterAsync(
        string queueOrTopic,
        string? subscription,
        int maxMessages = 50,
        CancellationToken ct = default)
    {
        var receiver = subscription is null
            ? _client.CreateReceiver(queueOrTopic, new ServiceBusReceiverOptions
                {
                    ReceiveMode = ServiceBusReceiveMode.PeekLock,
                    SubQueue = SubQueue.DeadLetter
                })
            : _client.CreateReceiver(queueOrTopic, subscription,
                new ServiceBusReceiverOptions
                {
                    ReceiveMode = ServiceBusReceiveMode.PeekLock,
                    SubQueue = SubQueue.DeadLetter
                });

        var messages = await receiver.PeekMessagesAsync(maxMessages, cancellationToken: ct);

        return [.. messages.Select(m => new ServiceBusReceivedMessageDto(
                m.MessageId,
                m.Subject ?? string.Empty,
                m.ContentType ?? string.Empty,
                m.EnqueuedTime,
                ProcessMessageBody(m.Body, m.ContentType),
                true,
                m.SessionId))];
    }

    public async Task<PagedResult<ServiceBusReceivedMessageDto>> PeekPagedAsync(
        string queueOrTopic,
        string? subscription,
        int pageNumber,
        int pageSize = 50,
        CancellationToken ct = default)
    {
        Console.WriteLine($"[AzureMessagePeekProvider] PeekPagedAsync - Page: {pageNumber}, Size: {pageSize}");

        // Get total count first
        var (activeCount, _) = await GetMessageCountsAsync(queueOrTopic, subscription, ct);

        // Calculate skip amount
        var skipCount = (pageNumber - 1) * pageSize;

        // For now, we'll retrieve messages sequentially
        // In a real implementation, we'd use sequence numbers for efficient skipping
        var receiver = string.IsNullOrEmpty(subscription)
            ? _client.CreateReceiver(queueOrTopic, new ServiceBusReceiverOptions
              {
                  ReceiveMode = ServiceBusReceiveMode.PeekLock,
                  SubQueue = SubQueue.None
              })
            : _client.CreateReceiver(queueOrTopic, subscription, new ServiceBusReceiverOptions
              {
                  ReceiveMode = ServiceBusReceiveMode.PeekLock,
                  SubQueue = SubQueue.None
              });

        var allMessages = new List<ServiceBusReceivedMessage>();
        long? lastSequenceNumber = null;

        // First, let's get the actual total count if we don't have it
        if (activeCount == 0)
        {
            var (count, _) = await GetMessageCountsAsync(queueOrTopic, subscription, ct);
            activeCount = count;
        }

        var messagesToRetrieve = skipCount + pageSize;

        await using (receiver)
        {
            while (allMessages.Count < messagesToRetrieve)
            {
                var batchSize = Math.Min(250, messagesToRetrieve - allMessages.Count);
                var messages = lastSequenceNumber.HasValue
                    ? await receiver.PeekMessagesAsync(batchSize, fromSequenceNumber: lastSequenceNumber.Value + 1, cancellationToken: ct)
                    : await receiver.PeekMessagesAsync(batchSize, cancellationToken: ct);

                if (!messages.Any())
                {
                    break;
                }

                allMessages.AddRange(messages);
                lastSequenceNumber = messages.Last().SequenceNumber;
            }
        }

        // Skip to the requested page and take only the page size
        var pagedMessages = allMessages
            .Skip(skipCount)
            .Take(pageSize)
            .Select(m => new ServiceBusReceivedMessageDto(
                m.MessageId ?? string.Empty,
                m.Subject ?? string.Empty,
                m.ContentType ?? string.Empty,
                m.EnqueuedTime,
                ProcessMessageBody(m.Body, m.ContentType),
                false,
                m.SessionId))
            .ToList();

        return new PagedResult<ServiceBusReceivedMessageDto>
        {
            Items = pagedMessages,
            TotalCount = activeCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<PagedResult<ServiceBusReceivedMessageDto>> PeekDeadLetterPagedAsync(
        string queueOrTopic,
        string? subscription,
        int pageNumber,
        int pageSize = 50,
        CancellationToken ct = default)
    {
        Console.WriteLine($"[AzureMessagePeekProvider] PeekDeadLetterPagedAsync - Page: {pageNumber}, Size: {pageSize}");

        // Get total count first
        var (_, deadLetterCount) = await GetMessageCountsAsync(queueOrTopic, subscription, ct);

        // Calculate skip amount
        var skipCount = (pageNumber - 1) * pageSize;

        var receiver = string.IsNullOrEmpty(subscription)
            ? _client.CreateReceiver(queueOrTopic, new ServiceBusReceiverOptions
              {
                  ReceiveMode = ServiceBusReceiveMode.PeekLock,
                  SubQueue = SubQueue.DeadLetter
              })
            : _client.CreateReceiver(queueOrTopic, subscription, new ServiceBusReceiverOptions
              {
                  ReceiveMode = ServiceBusReceiveMode.PeekLock,
                  SubQueue = SubQueue.DeadLetter
              });

        var allMessages = new List<ServiceBusReceivedMessage>();
        long? lastSequenceNumber = null;

        // First, let's get the actual total count if we don't have it
        if (deadLetterCount == 0)
        {
            var (_, count) = await GetMessageCountsAsync(queueOrTopic, subscription, ct);
            deadLetterCount = count;
        }

        var messagesToRetrieve = skipCount + pageSize;

        await using (receiver)
        {
            while (allMessages.Count < messagesToRetrieve)
            {
                var batchSize = Math.Min(250, messagesToRetrieve - allMessages.Count);
                var messages = lastSequenceNumber.HasValue
                    ? await receiver.PeekMessagesAsync(batchSize, fromSequenceNumber: lastSequenceNumber.Value + 1, cancellationToken: ct)
                    : await receiver.PeekMessagesAsync(batchSize, cancellationToken: ct);

                if (!messages.Any())
                {
                    break;
                }

                allMessages.AddRange(messages);
                lastSequenceNumber = messages.Last().SequenceNumber;
            }
        }

        // Skip to the requested page and take only the page size
        var pagedMessages = allMessages
            .Skip(skipCount)
            .Take(pageSize)
            .Select(m => new ServiceBusReceivedMessageDto(
                m.MessageId ?? string.Empty,
                m.Subject ?? string.Empty,
                m.ContentType ?? string.Empty,
                m.EnqueuedTime,
                ProcessMessageBody(m.Body, m.ContentType),
                true,
                m.SessionId))
            .ToList();

        return new PagedResult<ServiceBusReceivedMessageDto>
        {
            Items = pagedMessages,
            TotalCount = deadLetterCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<(int activeCount, int deadLetterCount)> GetMessageCountsAsync(
        string queueOrTopic,
        string? subscription,
        CancellationToken ct = default)
    {
        Console.WriteLine($"[AzureMessagePeekProvider] Getting message counts using Management API for {queueOrTopic}/{subscription}");

        try
        {
            // Use the more efficient Management API
            var (totalCount, activeCount, deadLetterCount, scheduledCount) = await GetRuntimePropertiesAsync(queueOrTopic, subscription, ct);

            // Return as int for backward compatibility, but log if we're truncating
            if (activeCount > int.MaxValue || deadLetterCount > int.MaxValue)
            {
                Console.WriteLine($"[AzureMessagePeekProvider] Warning: Message counts exceed int.MaxValue, truncating values");
            }

            return ((int)Math.Min(activeCount, int.MaxValue), (int)Math.Min(deadLetterCount, int.MaxValue));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AzureMessagePeekProvider] Management API failed, falling back to peek method: {ex.Message}");
            // Fall back to the old method if Management API fails
            return await GetMessageCountsByPeekingAsync(queueOrTopic, subscription, ct);
        }
    }

    private async Task<(int activeCount, int deadLetterCount)> GetMessageCountsByPeekingAsync(
        string queueOrTopic,
        string? subscription,
        CancellationToken ct = default)
    {
        Console.WriteLine($"[AzureMessagePeekProvider] Falling back to counting by peeking messages");

        // You can configure this limit based on your needs
        const int maxCountLimit = 1000000; // 0 = no limit, or set to 1000000 for 1 million limit

        var activeCount = 0;
        var deadLetterCount = 0;

        // Count active messages
        var activeReceiver = string.IsNullOrEmpty(subscription)
            ? _client.CreateReceiver(queueOrTopic, new ServiceBusReceiverOptions
              {
                  ReceiveMode = ServiceBusReceiveMode.PeekLock,
                  SubQueue = SubQueue.None
              })
            : _client.CreateReceiver(queueOrTopic, subscription, new ServiceBusReceiverOptions
              {
                  ReceiveMode = ServiceBusReceiveMode.PeekLock,
                  SubQueue = SubQueue.None
              });

        await using (activeReceiver)
        {
            long? lastSequenceNumber = null;
            var iterations = 0;
            while (true)
            {
                iterations++;

                var messages = lastSequenceNumber.HasValue
                    ? await activeReceiver.PeekMessagesAsync(250, fromSequenceNumber: lastSequenceNumber.Value + 1, cancellationToken: ct)
                    : await activeReceiver.PeekMessagesAsync(250, cancellationToken: ct);

                if (!messages.Any())
                {
                    break;
                }

                activeCount += messages.Count;
                lastSequenceNumber = messages.Last().SequenceNumber;


                // Show progress every 10,000 messages
                if (activeCount % 10000 == 0 && activeCount > 0)
                {
                    Console.WriteLine($"[AzureMessagePeekProvider] Progress: Counted {activeCount:N0} active messages so far...");
                }

                // Limit counting to prevent excessive API calls
                if (maxCountLimit > 0 && activeCount >= maxCountLimit)
                {
                    Console.WriteLine($"[AzureMessagePeekProvider] Active count reached limit of {maxCountLimit:N0}, stopping count");
                    break;
                }
            }
        }

        // Count dead letter messages
        var deadLetterReceiver = string.IsNullOrEmpty(subscription)
            ? _client.CreateReceiver(queueOrTopic, new ServiceBusReceiverOptions
              {
                  ReceiveMode = ServiceBusReceiveMode.PeekLock,
                  SubQueue = SubQueue.DeadLetter
              })
            : _client.CreateReceiver(queueOrTopic, subscription, new ServiceBusReceiverOptions
              {
                  ReceiveMode = ServiceBusReceiveMode.PeekLock,
                  SubQueue = SubQueue.DeadLetter
              });

        await using (deadLetterReceiver)
        {
            long? lastSequenceNumber = null;
            while (true)
            {
                var messages = lastSequenceNumber.HasValue
                    ? await deadLetterReceiver.PeekMessagesAsync(250, fromSequenceNumber: lastSequenceNumber.Value + 1, cancellationToken: ct)
                    : await deadLetterReceiver.PeekMessagesAsync(250, cancellationToken: ct);

                if (!messages.Any())
                {
                    break;
                }

                deadLetterCount += messages.Count;
                lastSequenceNumber = messages.Last().SequenceNumber;

                // Show progress every 10,000 messages
                if (deadLetterCount % 10000 == 0 && deadLetterCount > 0)
                {
                    Console.WriteLine($"[AzureMessagePeekProvider] Progress: Counted {deadLetterCount:N0} dead letter messages so far...");
                }

                // Limit counting to prevent excessive API calls
                if (maxCountLimit > 0 && deadLetterCount >= maxCountLimit)
                {
                    Console.WriteLine($"[AzureMessagePeekProvider] Dead letter count reached limit of {maxCountLimit:N0}, stopping count");
                    break;
                }
            }
        }

        Console.WriteLine($"[AzureMessagePeekProvider] Final message counts - Active: {activeCount:N0}, Dead Letter: {deadLetterCount:N0}");
        return (activeCount, deadLetterCount);
    }

    public async Task<(long totalCount, long activeCount, long deadLetterCount, long scheduledCount)> GetRuntimePropertiesAsync(
        string queueOrTopic,
        string? subscription,
        CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrEmpty(subscription))
            {
                // Queue
                var queueProperties = await _adminClient.GetQueueRuntimePropertiesAsync(queueOrTopic, ct);
                var props = queueProperties.Value;

                Console.WriteLine($"[AzureMessagePeekProvider] Queue runtime properties - Total: {props.TotalMessageCount:N0}, Active: {props.ActiveMessageCount:N0}, DLQ: {props.DeadLetterMessageCount:N0}, Scheduled: {props.ScheduledMessageCount:N0}");

                return (
                    totalCount: props.TotalMessageCount,
                    activeCount: props.ActiveMessageCount,
                    deadLetterCount: props.DeadLetterMessageCount,
                    scheduledCount: props.ScheduledMessageCount
                );
            }
            else
            {
                // Topic/Subscription
                var subProperties = await _adminClient.GetSubscriptionRuntimePropertiesAsync(queueOrTopic, subscription, ct);
                var props = subProperties.Value;

                Console.WriteLine($"[AzureMessagePeekProvider] Subscription runtime properties - Total: {props.TotalMessageCount:N0}, Active: {props.ActiveMessageCount:N0}, DLQ: {props.DeadLetterMessageCount:N0}");

                return (
                    totalCount: props.TotalMessageCount,
                    activeCount: props.ActiveMessageCount,
                    deadLetterCount: props.DeadLetterMessageCount,
                    scheduledCount: 0 // Subscriptions don't have ScheduledMessageCount in the runtime properties
                );
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AzureMessagePeekProvider] Error getting runtime properties: {ex.Message}");
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _client.DisposeAsync();
        // ServiceBusAdministrationClient doesn't implement IAsyncDisposable in older versions
        if (_adminClient is IAsyncDisposable asyncAdmin)
        {
            await asyncAdmin.DisposeAsync();
        }
    }

    /// <summary>
    /// Content-Typeに基づいてメッセージボディを解凍・変換する
    /// </summary>
    private static string ProcessMessageBody(BinaryData body, string? contentType)
    {
        var bytes = body.ToArray();

        if (IsCompressedContentType(contentType))
        {
            try
            {
                // GZip解凍を試みる
                var decompressed = TryDecompressGZip(bytes);
                if (decompressed != null)
                    return Encoding.UTF8.GetString(decompressed);

                // ZIP解凍を試みる
                decompressed = TryDecompressZip(bytes);
                if (decompressed != null)
                    return Encoding.UTF8.GetString(decompressed);
            }
            catch
            {
                // 解凍失敗 - Base64で表示
            }

            // 解凍できない場合はBase64で表示
            return $"[Binary data - Base64 encoded]\n{Convert.ToBase64String(bytes)}";
        }

        // 非圧縮の場合、通常の文字列変換を試みる
        return body.ToString();
    }

    private static bool IsCompressedContentType(string? contentType)
    {
        if (string.IsNullOrEmpty(contentType)) return false;
        var ct = contentType.ToLowerInvariant();
        return ct.Contains("zip") || ct.Contains("gzip") || ct.Contains("deflate") || ct.Contains("compressed");
    }

    private static byte[]? TryDecompressGZip(byte[] data)
    {
        try
        {
            using var inputStream = new MemoryStream(data);
            using var gzipStream = new GZipStream(inputStream, CompressionMode.Decompress);
            using var outputStream = new MemoryStream();
            gzipStream.CopyTo(outputStream);
            return outputStream.ToArray();
        }
        catch { return null; }
    }

    private static byte[]? TryDecompressZip(byte[] data)
    {
        try
        {
            using var archive = new ZipArchive(new MemoryStream(data), ZipArchiveMode.Read);
            if (archive.Entries.Count > 0)
            {
                using var stream = archive.Entries[0].Open();
                using var outputStream = new MemoryStream();
                stream.CopyTo(outputStream);
                return outputStream.ToArray();
            }
            return null;
        }
        catch { return null; }
    }
}
