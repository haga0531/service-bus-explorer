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

                // Complete all messages to remove them
                foreach (var message in messages)
                {
                    await receiver.CompleteMessageAsync(message, cancellationToken);
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

    public async ValueTask DisposeAsync() => await _client.DisposeAsync();
}
