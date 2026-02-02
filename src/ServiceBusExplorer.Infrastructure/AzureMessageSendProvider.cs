using System.IO.Compression;
using System.Text;
using Azure.Messaging.ServiceBus;
using ServiceBusExplorer.Infrastructure.Models;

namespace ServiceBusExplorer.Infrastructure;

public sealed class AzureMessageSendProvider : IMessageSendProvider
{
    private readonly ServiceBusClient _client;

    public AzureMessageSendProvider(ServiceBusAuthContext authContext)
    {
        ArgumentNullException.ThrowIfNull(authContext);
        _client = authContext.CreateServiceBusClient();
    }

    public async Task SendMessageAsync(
        string queueOrTopic,
        string? subscription,
        string messageBody,
        Dictionary<string, object>? properties = null,
        string? contentType = null,
        string? label = null,
        string? sessionId = null,
        CancellationToken ct = default)
    {
        // Note: You cannot send messages directly to a subscription
        // Messages are sent to topics, and subscriptions receive them
        if (subscription != null)
        {
            throw new ArgumentException("Cannot send messages to a subscription. Send to the topic instead.");
        }

        await using var sender = _client.CreateSender(queueOrTopic);

        // Content-Typeが圧縮形式を示す場合、メッセージボディを圧縮
        Azure.Messaging.ServiceBus.ServiceBusMessage message;
        if (IsCompressedContentType(contentType))
        {
            var compressedBody = CompressGZip(Encoding.UTF8.GetBytes(messageBody));
            message = new Azure.Messaging.ServiceBus.ServiceBusMessage(compressedBody)
            {
                ContentType = contentType,
                Subject = label,
                SessionId = sessionId
            };
        }
        else
        {
            message = new Azure.Messaging.ServiceBus.ServiceBusMessage(messageBody)
            {
                ContentType = contentType,
                Subject = label,
                SessionId = sessionId
            };
        }

        if (properties != null)
        {
            foreach (var prop in properties)
            {
                message.ApplicationProperties.Add(prop.Key, prop.Value);
            }
        }

        await sender.SendMessageAsync(message, ct);
    }

    public async Task SendMessagesAsync(
        string queueOrTopic,
        string? subscription,
        IEnumerable<ServiceBusMessage> messages,
        CancellationToken ct = default)
    {
        if (subscription != null)
        {
            throw new ArgumentException("Cannot send messages to a subscription. Send to the topic instead.");
        }

        await using var sender = _client.CreateSender(queueOrTopic);

        var batch = await sender.CreateMessageBatchAsync(ct);

        foreach (var msg in messages)
        {
            // Content-Typeが圧縮形式を示す場合、メッセージボディを圧縮
            Azure.Messaging.ServiceBus.ServiceBusMessage serviceBusMessage;
            if (IsCompressedContentType(msg.ContentType))
            {
                var compressedBody = CompressGZip(Encoding.UTF8.GetBytes(msg.Body));
                serviceBusMessage = new Azure.Messaging.ServiceBus.ServiceBusMessage(compressedBody)
                {
                    ContentType = msg.ContentType,
                    Subject = msg.Label
                };
            }
            else
            {
                serviceBusMessage = new Azure.Messaging.ServiceBus.ServiceBusMessage(msg.Body)
                {
                    ContentType = msg.ContentType,
                    Subject = msg.Label
                };
            }

            if (msg.Properties != null)
            {
                foreach (var prop in msg.Properties)
                {
                    serviceBusMessage.ApplicationProperties.Add(prop.Key, prop.Value);
                }
            }

            if (!batch.TryAddMessage(serviceBusMessage))
            {
                // If the batch is full, send it and create a new one
                await sender.SendMessagesAsync(batch, ct);
                batch = await sender.CreateMessageBatchAsync(ct);

                if (!batch.TryAddMessage(serviceBusMessage))
                {
                    throw new InvalidOperationException($"Message is too large to fit in a batch.");
                }
            }
        }

        // Send any remaining messages
        if (batch.Count > 0)
        {
            await sender.SendMessagesAsync(batch, ct);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _client.DisposeAsync();
    }

    private static bool IsCompressedContentType(string? contentType)
    {
        if (string.IsNullOrEmpty(contentType)) return false;
        var ct = contentType.ToLowerInvariant();
        return ct.Contains("zip") || ct.Contains("gzip") || ct.Contains("deflate") || ct.Contains("compressed");
    }

    private static byte[] CompressGZip(byte[] data)
    {
        using var outputStream = new MemoryStream();
        using (var gzipStream = new GZipStream(outputStream, CompressionMode.Compress, leaveOpen: true))
        {
            gzipStream.Write(data, 0, data.Length);
        }
        return outputStream.ToArray();
    }
}
