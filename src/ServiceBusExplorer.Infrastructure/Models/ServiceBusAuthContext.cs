using Azure.Core;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;

namespace ServiceBusExplorer.Infrastructure.Models;

/// <summary>
/// Abstract base class representing authentication context for Azure Service Bus.
/// Provides factory methods to create Service Bus clients with the appropriate authentication.
/// </summary>
public abstract class ServiceBusAuthContext
{
    /// <summary>
    /// Creates a ServiceBusClient with the appropriate authentication method.
    /// </summary>
    public abstract ServiceBusClient CreateServiceBusClient();

    /// <summary>
    /// Creates a ServiceBusAdministrationClient with the appropriate authentication method.
    /// </summary>
    public abstract ServiceBusAdministrationClient CreateAdminClient();
}

/// <summary>
/// Authentication context using a connection string.
/// This is the traditional authentication method for Azure Service Bus.
/// </summary>
public sealed class ConnectionStringAuthContext : ServiceBusAuthContext
{
    private string ConnectionString { get; }

    public ConnectionStringAuthContext(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ConnectionString = connectionString;
    }

    public override ServiceBusClient CreateServiceBusClient() => new(ConnectionString);

    public override ServiceBusAdministrationClient CreateAdminClient() => new(ConnectionString);
}

/// <summary>
/// Authentication context using Azure Active Directory token credentials.
/// This supports various credential types including Interactive Browser, Managed Identity, etc.
/// </summary>
public sealed class TokenCredentialAuthContext : ServiceBusAuthContext
{
    private string FullyQualifiedNamespace { get; }
    private TokenCredential Credential { get; }

    public TokenCredentialAuthContext(string fullyQualifiedNamespace, TokenCredential credential)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullyQualifiedNamespace, nameof(fullyQualifiedNamespace));
        ArgumentNullException.ThrowIfNull(credential, nameof(credential));

        FullyQualifiedNamespace = fullyQualifiedNamespace;
        Credential = credential;
    }

    public override ServiceBusClient CreateServiceBusClient() => new(FullyQualifiedNamespace, Credential);

    public override ServiceBusAdministrationClient CreateAdminClient() => new(FullyQualifiedNamespace, Credential);
}
