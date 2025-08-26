using Azure.Messaging.ServiceBus.Administration;

namespace ServiceBusExplorer.Infrastructure;

public interface INamespaceProvider : IAsyncDisposable
{
    Task<IEnumerable<string>> GetQueuesAsync(CancellationToken ct = default);
    Task<IEnumerable<string>> GetTopicsAsync(CancellationToken ct = default);
    Task<QueueProperties?> GetQueuePropertiesAsync(string queueName, CancellationToken ct = default);
    Task<IEnumerable<string>> GetSubscriptionsAsync(string topicName, CancellationToken ct = default);
    Task<SubscriptionProperties?> GetSubscriptionPropertiesAsync(string topicName, string subscriptionName, CancellationToken ct = default);
}
