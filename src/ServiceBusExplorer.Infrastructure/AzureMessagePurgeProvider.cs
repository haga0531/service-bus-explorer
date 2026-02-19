using Azure.Messaging.ServiceBus;
using ServiceBusExplorer.Infrastructure.Models;

namespace ServiceBusExplorer.Infrastructure;

public sealed class AzureMessagePurgeProvider : IMessagePurgeProvider
{
    private readonly ServiceBusClient _client;

    public AzureMessagePurgeProvider(ServiceBusAuthContext authContext)
    {
        ArgumentNullException.ThrowIfNull(authContext);
        _client = authContext.CreateServiceBusClient();
    }

    public async Task<int> PurgeActiveMessagesAsync(
        string queueOrTopic,
        string? subscription,
        CancellationToken cancellationToken = default)
    {
        return await PurgeMessagesAsync(queueOrTopic, subscription, SubQueue.None, cancellationToken);
    }

    public async Task<int> PurgeDeadLetterMessagesAsync(
        string queueOrTopic,
        string? subscription,
        CancellationToken cancellationToken = default)
    {
        return await PurgeMessagesAsync(queueOrTopic, subscription, SubQueue.DeadLetter, cancellationToken);
    }

    private async Task<int> PurgeMessagesAsync(
        string queueOrTopic,
        string? subscription,
        SubQueue subQueue,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Try non-session receiver first
            return await PurgeWithNonSessionReceiverAsync(queueOrTopic, subscription, subQueue, cancellationToken);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("session"))
        {
            // Queue requires sessions, use session receiver
            Console.WriteLine($"[AzureMessagePurgeProvider] Queue requires sessions, switching to session-based purge");
            return await PurgeWithSessionReceiverAsync(queueOrTopic, subscription, subQueue, cancellationToken);
        }
    }

    private async Task<int> PurgeWithNonSessionReceiverAsync(
        string queueOrTopic,
        string? subscription,
        SubQueue subQueue,
        CancellationToken cancellationToken)
    {
        var receiver = subscription is null
            ? _client.CreateReceiver(queueOrTopic, new ServiceBusReceiverOptions
                {
                    ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete,
                    SubQueue = subQueue
                })
            : _client.CreateReceiver(queueOrTopic, subscription,
                new ServiceBusReceiverOptions
                {
                    ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete,
                    SubQueue = subQueue
                });

        try
        {
            var purgedCount = 0;
            var batchSize = 100;
            var maxWaitTime = TimeSpan.FromSeconds(5);

            while (!cancellationToken.IsCancellationRequested)
            {
                var messages = await receiver.ReceiveMessagesAsync(
                    maxMessages: batchSize,
                    maxWaitTime: maxWaitTime,
                    cancellationToken: cancellationToken);

                if (!messages.Any())
                {
                    Console.WriteLine($"[AzureMessagePurgeProvider] No more messages to purge from {subQueue}");
                    break;
                }

                purgedCount += messages.Count;
                Console.WriteLine($"[AzureMessagePurgeProvider] Purged {messages.Count} messages from {subQueue}. Total: {purgedCount}");
            }

            Console.WriteLine($"[AzureMessagePurgeProvider] Total messages purged from {subQueue}: {purgedCount}");
            return purgedCount;
        }
        finally
        {
            await receiver.DisposeAsync();
        }
    }

    private async Task<int> PurgeWithSessionReceiverAsync(
        string queueOrTopic,
        string? subscription,
        SubQueue subQueue,
        CancellationToken cancellationToken)
    {
        var purgedCount = 0;
        var maxWaitTime = TimeSpan.FromSeconds(5);
        var batchSize = 100;
        var sessionTimeout = TimeSpan.FromSeconds(3); // Short timeout for accepting sessions

        // Keep accepting sessions until no more are available
        while (!cancellationToken.IsCancellationRequested)
        {
            ServiceBusSessionReceiver? sessionReceiver = null;
            try
            {
                var options = new ServiceBusSessionReceiverOptions
                {
                    ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete
                };

                // Use a timeout for accepting sessions to avoid long waits
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(sessionTimeout);

                // Accept next available session
                sessionReceiver = subscription is null
                    ? await _client.AcceptNextSessionAsync(queueOrTopic, options, timeoutCts.Token)
                    : await _client.AcceptNextSessionAsync(queueOrTopic, subscription, options, timeoutCts.Token);

                Console.WriteLine($"[AzureMessagePurgeProvider] Accepted session: {sessionReceiver.SessionId}");

                // Purge all messages in this session
                while (!cancellationToken.IsCancellationRequested)
                {
                    var messages = await sessionReceiver.ReceiveMessagesAsync(
                        maxMessages: batchSize,
                        maxWaitTime: maxWaitTime,
                        cancellationToken: cancellationToken);

                    if (!messages.Any())
                    {
                        break;
                    }

                    purgedCount += messages.Count;
                    Console.WriteLine($"[AzureMessagePurgeProvider] Purged {messages.Count} messages from session {sessionReceiver.SessionId}. Total: {purgedCount}");
                }
            }
            catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.ServiceTimeout)
            {
                // No more sessions available
                Console.WriteLine($"[AzureMessagePurgeProvider] No more sessions available");
                break;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // Session accept timed out, no more sessions available
                Console.WriteLine($"[AzureMessagePurgeProvider] Session accept timed out, no more sessions available");
                break;
            }
            finally
            {
                if (sessionReceiver != null)
                {
                    await sessionReceiver.DisposeAsync();
                }
            }
        }

        Console.WriteLine($"[AzureMessagePurgeProvider] Total messages purged from {subQueue}: {purgedCount}");
        return purgedCount;
    }

    public async ValueTask DisposeAsync() => await _client.DisposeAsync();
}
