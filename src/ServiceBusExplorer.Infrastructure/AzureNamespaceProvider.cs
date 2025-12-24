using Azure.Messaging.ServiceBus.Administration;
using ServiceBusExplorer.Infrastructure.Models;

namespace ServiceBusExplorer.Infrastructure;

/// <summary>
/// Concrete implementation of <see cref="INamespaceProvider"/> that talks to
/// Azure Service Bus via <see cref="ServiceBusAdministrationClient"/>.
/// </summary>
public sealed class AzureNamespaceProvider : INamespaceProvider, IAsyncDisposable
{
    private readonly ServiceBusAdministrationClient _admin;

    public AzureNamespaceProvider(ServiceBusAuthContext authContext)
    {
        ArgumentNullException.ThrowIfNull(authContext);
        _admin = authContext.CreateAdminClient();
    }

    public async Task<IEnumerable<string>> GetQueuesAsync(CancellationToken ct = default)
    {
        var names = new List<string>();
        await foreach (var queue in _admin.GetQueuesAsync(ct))
        {
            names.Add(queue.Name);
        }
        return names;
    }

    public async Task<IEnumerable<string>> GetTopicsAsync(CancellationToken ct = default)
    {
        var names = new List<string>();
        await foreach (var topic in _admin.GetTopicsAsync(ct))
        {
            names.Add(topic.Name);
        }
        return names;
    }

    public async Task<QueueProperties?> GetQueuePropertiesAsync(string queueName, CancellationToken ct = default)
    {
        try
        {
            var response = await _admin.GetQueueAsync(queueName, ct);
            return response.Value;
        }
        catch
        {
            return null;
        }
    }

    public async Task<IEnumerable<string>> GetSubscriptionsAsync(string topicName, CancellationToken ct = default)
    {
        var names = new List<string>();
        await foreach (var subscription in _admin.GetSubscriptionsAsync(topicName, ct))
        {
            names.Add(subscription.SubscriptionName);
        }
        return names;
    }

    public async Task<SubscriptionProperties?> GetSubscriptionPropertiesAsync(string topicName, string subscriptionName, CancellationToken ct = default)
    {
        try
        {
            var response = await _admin.GetSubscriptionAsync(topicName, subscriptionName, ct);
            return response.Value;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    ///     Disposes the underlying <see cref="ServiceBusAdministrationClient"/>.
    ///     Newer versions of the Azure SDK implement <see cref="IAsyncDisposable"/>,
    ///     but if you are using an older package you can fall back to <c>Dispose()</c>
    ///     by replacing this method with a synchronous implementation.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        if (_admin is IAsyncDisposable asyncClient)
        {
            return asyncClient.DisposeAsync();
        }

        if (_admin is IDisposable disposable)
        {
            disposable.Dispose();
        }

        return ValueTask.CompletedTask;
    }
}
