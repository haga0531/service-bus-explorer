using Azure.Messaging.ServiceBus;
using ServiceBusExplorer.Infrastructure.Models;

namespace ServiceBusExplorer.Infrastructure;

public sealed class AzureMessageDeleteProvider : IMessageDeleteProvider
{
    private readonly ServiceBusClient _client;

    public AzureMessageDeleteProvider(ServiceBusAuthContext authContext)
    {
        ArgumentNullException.ThrowIfNull(authContext);
        _client = authContext.CreateServiceBusClient();
    }

    public async Task DeleteActiveMessageAsync(
        string queueOrTopic,
        string? subscription,
        string messageId,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"[AzureMessageDeleteProvider] DeleteActiveMessageAsync called - Queue/Topic: {queueOrTopic}, Subscription: {subscription ?? "null"}, MessageId: {messageId}");

        try
        {
            await DeleteWithNonSessionReceiverAsync(queueOrTopic, subscription, messageId, SubQueue.None, cancellationToken);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("session"))
        {
            Console.WriteLine($"[AzureMessageDeleteProvider] Queue requires sessions, switching to session-based delete");
            await DeleteWithSessionReceiverAsync(queueOrTopic, subscription, messageId, cancellationToken);
        }
    }

    private async Task DeleteWithNonSessionReceiverAsync(
        string queueOrTopic,
        string? subscription,
        string messageId,
        SubQueue subQueue,
        CancellationToken cancellationToken)
    {
        var receiver = subscription is null
            ? _client.CreateReceiver(queueOrTopic, new ServiceBusReceiverOptions
                {
                    ReceiveMode = ServiceBusReceiveMode.PeekLock,
                    SubQueue = subQueue
                })
            : _client.CreateReceiver(queueOrTopic, subscription,
                new ServiceBusReceiverOptions
                {
                    ReceiveMode = ServiceBusReceiveMode.PeekLock,
                    SubQueue = subQueue
                });

        try
        {
            var maxAttempts = 10;
            var batchSize = 10;

            for (var i = 0; i < maxAttempts; i++)
            {
                var messages = await receiver.ReceiveMessagesAsync(
                    maxMessages: batchSize,
                    maxWaitTime: TimeSpan.FromSeconds(1),
                    cancellationToken: cancellationToken);

                if (!messages.Any())
                {
                    Console.WriteLine($"[AzureMessageDeleteProvider] No more messages in queue");
                    break;
                }

                Console.WriteLine($"[AzureMessageDeleteProvider] Received {messages.Count} messages in batch {i + 1}");

                foreach (var message in messages)
                {
                    Console.WriteLine($"[AzureMessageDeleteProvider] Checking message ID: {message.MessageId} (looking for: {messageId})");

                    if (message.MessageId == messageId)
                    {
                        Console.WriteLine($"[AzureMessageDeleteProvider] Found target message! Attempting to complete...");
                        await receiver.CompleteMessageAsync(message, cancellationToken);
                        Console.WriteLine($"[AzureMessageDeleteProvider] Successfully deleted message {messageId}");
                        return;
                    }
                    else
                    {
                        Console.WriteLine($"[AzureMessageDeleteProvider] Not the target message, abandoning {message.MessageId}");
                        await receiver.AbandonMessageAsync(message, cancellationToken: cancellationToken);
                    }
                }
            }

            Console.WriteLine($"[AzureMessageDeleteProvider] Message {messageId} not found after {maxAttempts} attempts");
        }
        finally
        {
            await receiver.DisposeAsync();
        }
    }

    private async Task DeleteWithSessionReceiverAsync(
        string queueOrTopic,
        string? subscription,
        string messageId,
        CancellationToken cancellationToken)
    {
        var sessionTimeout = TimeSpan.FromSeconds(3);
        var maxSessionAttempts = 5;

        for (var sessionAttempt = 0; sessionAttempt < maxSessionAttempts; sessionAttempt++)
        {
            ServiceBusSessionReceiver? sessionReceiver = null;
            try
            {
                var options = new ServiceBusSessionReceiverOptions
                {
                    ReceiveMode = ServiceBusReceiveMode.PeekLock
                };

                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(sessionTimeout);

                sessionReceiver = subscription is null
                    ? await _client.AcceptNextSessionAsync(queueOrTopic, options, timeoutCts.Token)
                    : await _client.AcceptNextSessionAsync(queueOrTopic, subscription, options, timeoutCts.Token);

                Console.WriteLine($"[AzureMessageDeleteProvider] Accepted session: {sessionReceiver.SessionId}");

                var maxAttempts = 10;
                var batchSize = 10;

                for (var i = 0; i < maxAttempts; i++)
                {
                    var messages = await sessionReceiver.ReceiveMessagesAsync(
                        maxMessages: batchSize,
                        maxWaitTime: TimeSpan.FromSeconds(1),
                        cancellationToken: cancellationToken);

                    if (!messages.Any())
                    {
                        break;
                    }

                    foreach (var message in messages)
                    {
                        if (message.MessageId == messageId)
                        {
                            Console.WriteLine($"[AzureMessageDeleteProvider] Found target message in session {sessionReceiver.SessionId}!");
                            await sessionReceiver.CompleteMessageAsync(message, cancellationToken);
                            Console.WriteLine($"[AzureMessageDeleteProvider] Successfully deleted message {messageId}");
                            return;
                        }
                        else
                        {
                            await sessionReceiver.AbandonMessageAsync(message, cancellationToken: cancellationToken);
                        }
                    }
                }
            }
            catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.ServiceTimeout)
            {
                Console.WriteLine($"[AzureMessageDeleteProvider] No more sessions available");
                break;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                Console.WriteLine($"[AzureMessageDeleteProvider] Session accept timed out");
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

        Console.WriteLine($"[AzureMessageDeleteProvider] Message {messageId} not found in any session");
    }

    public async Task DeleteDeadLetterMessageAsync(
        string queueOrTopic,
        string? subscription,
        string messageId,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"[AzureMessageDeleteProvider] DeleteDeadLetterMessageAsync called - Queue/Topic: {queueOrTopic}, Subscription: {subscription ?? "null"}, MessageId: {messageId}");

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

        try
        {
            // First, let's peek to see if messages are there
            Console.WriteLine($"[AzureMessageDeleteProvider] Peeking dead letter queue first...");
            var peekedMessages = await receiver.PeekMessagesAsync(10, cancellationToken: cancellationToken);
            Console.WriteLine($"[AzureMessageDeleteProvider] Peeked {peekedMessages.Count} messages in dead letter queue");
            foreach (var peeked in peekedMessages)
            {
                Console.WriteLine($"[AzureMessageDeleteProvider] Peeked message ID: {peeked.MessageId}");
            }

            // Receive messages and find the one with matching ID
            // We need to receive multiple messages as we can't directly receive by ID
            var maxAttempts = 10;
            var batchSize = 10;

            for (var i = 0; i < maxAttempts; i++)
            {
                var messages = await receiver.ReceiveMessagesAsync(
                    maxMessages: batchSize,
                    maxWaitTime: TimeSpan.FromSeconds(5),
                    cancellationToken: cancellationToken);

                if (!messages.Any())
                {
                    Console.WriteLine($"[AzureMessageDeleteProvider] No more messages in dead letter queue after waiting 5 seconds");
                    break;
                }

                Console.WriteLine($"[AzureMessageDeleteProvider] Received {messages.Count} messages in batch {i + 1}");

                foreach (var message in messages)
                {
                    Console.WriteLine($"[AzureMessageDeleteProvider] Checking message ID: {message.MessageId} (looking for: {messageId})");

                    if (message.MessageId == messageId)
                    {
                        Console.WriteLine($"[AzureMessageDeleteProvider] Found target message! Attempting to complete...");
                        // Found the target message, complete it to remove from dead letter
                        await receiver.CompleteMessageAsync(message, cancellationToken);
                        Console.WriteLine($"[AzureMessageDeleteProvider] Successfully deleted message {messageId} from dead letter queue");
                        return;
                    }
                    else
                    {
                        // Not the target message, abandon it so it remains in the queue
                        Console.WriteLine($"[AzureMessageDeleteProvider] Not the target message, abandoning {message.MessageId}");
                        await receiver.AbandonMessageAsync(message, cancellationToken: cancellationToken);
                    }
                }
            }

            Console.WriteLine($"[AzureMessageDeleteProvider] Message {messageId} not found in dead letter queue after {maxAttempts} attempts");
        }
        finally
        {
            await receiver.DisposeAsync();
        }
    }

    public async ValueTask DisposeAsync() => await _client.DisposeAsync();
}
