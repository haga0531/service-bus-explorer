using Azure.Messaging.ServiceBus.Administration;
using ServiceBusExplorer.Core.Models;
using ServiceBusExplorer.Infrastructure;

namespace ServiceBusExplorer.Core;

public class NamespaceService(INamespaceProvider provider, string connectionString)
{
    private readonly INamespaceProvider _provider = provider;
    private readonly string _connectionString = connectionString;

    public async Task<IReadOnlyList<NamespaceEntity>> GetEntitiesAsync(CancellationToken ct = default)
    {
        var queues = await _provider.GetQueuesAsync(ct);
        var topics = await _provider.GetTopicsAsync(ct);

        return
        [
            .. queues.Select(q => new NamespaceEntity(q, NamespaceEntity.EntityType.Queue))
,
            .. topics.Select(t => new NamespaceEntity(t, NamespaceEntity.EntityType.Topic)),
        ];
    }
    
    public async Task<IReadOnlyList<NamespaceNode>> GetNodesAsync(bool includeMessageCounts = false, CancellationToken ct = default)
    {
        _ = includeMessageCounts ? new ServiceBusAdministrationClient(_connectionString) : null;

        // Create root nodes for Queues and Topics
        var queuesNode = new NamespaceNode 
        { 
            Name = "Queues",
            FullPath = "Queues",
            EntityType = null // Folder node
        };

        var topicsNode = new NamespaceNode 
        { 
            Name = "Topics",
            FullPath = "Topics",
            EntityType = null // Folder node
        };

        // Load queues and topics in parallel for better performance
        var queuesTask = _provider.GetQueuesAsync(ct);
        var topicsTask = _provider.GetTopicsAsync(ct);
        
        await Task.WhenAll(queuesTask, topicsTask);
        
        // Process queues
        foreach (var queuePath in queuesTask.Result)
        {
            var parts = queuePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            AddNodeToParent(queuesNode, parts, queuePath, NamespaceEntity.EntityType.Queue);
        }

        // Process topics (without loading subscriptions for performance)
        foreach (var topicPath in topicsTask.Result)
        {
            var parts = topicPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var topicNode = AddNodeToParent(topicsNode, parts, topicPath, NamespaceEntity.EntityType.Topic);
            
            // Add a placeholder child to show expand arrow
            // This will be replaced with actual subscriptions when the topic is expanded
            topicNode?.Children.Add(new NamespaceNode
                {
                    Name = "Loading...",
                    FullPath = $"{topicPath}/__placeholder__",
                    Parent = topicNode,
                    EntityType = null // Placeholder node
                });
        }

        var result = new List<NamespaceNode>();
        if (queuesNode.Children.Count > 0)
        {
            result.Add(queuesNode);
        }
        if (topicsNode.Children.Count > 0)
        {
            result.Add(topicsNode);
        }

        return result;
    }

    private NamespaceNode? AddNodeToParent(NamespaceNode parent, string[] parts, string fullPath, NamespaceEntity.EntityType type)
    {
        var currentParent = parent;
        var currentPath = new List<string>();

        for (var i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            currentPath.Add(part);
            var partialPath = string.Join("/", currentPath);

            var existingChild = currentParent.Children.FirstOrDefault(c => c.Name == part);
            if (existingChild == null)
            {
                var newNode = new NamespaceNode 
                { 
                    Name = part,
                    FullPath = fullPath, // Always use the full path for entities
                    Parent = currentParent,
                    EntityType = i == parts.Length - 1 ? type : null
                };
                currentParent.Children.Add(newNode);
                currentParent = newNode;
            }
            else
            {
                currentParent = existingChild;
            }
        }
        
        // Return the final node (the entity node)
        return currentParent;
    }

    private NamespaceNode? FindNodeByPath(NamespaceNode parent, string path)
    {
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var current = parent;

        foreach (var part in parts)
        {
            current = current.Children.FirstOrDefault(c => c.Name == part);
            if (current == null)
            {
                return null;
            }
        }

        return current;
    }

}
