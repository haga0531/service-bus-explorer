namespace ServiceBusExplorer.Core.Models;

public record NamespaceEntity(string Name, NamespaceEntity.EntityType Type)
{
    public enum EntityType { Queue, Topic, Subscription }
}
