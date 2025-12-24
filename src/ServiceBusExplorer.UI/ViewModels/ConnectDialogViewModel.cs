using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Azure;
using Azure.Identity;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ServiceBusExplorer.Core;
using ServiceBusExplorer.Core.Models;
using ServiceBusExplorer.Infrastructure;
using ServiceBusExplorer.Infrastructure.Models;

namespace ServiceBusExplorer.UI;

public partial class ConnectDialogViewModel : ObservableObject
{
    // Tab selection
    [ObservableProperty] private int selectedAuthTabIndex = 0;

    // Connection String tab
    [ObservableProperty] private string? connectionString;

    // Azure Credentials tab
    [ObservableProperty] private string? @namespace;

    // Shared state
    [ObservableProperty] private bool isConnecting = false;
    [ObservableProperty] private string? errorMessage;

    public IRelayCommand CancelCommand  { get; }
    public IRelayCommand ConnectCommand { get; }

    private readonly Func<ServiceBusAuthContext, INamespaceProvider> _providerFactory;

    public ConnectDialogViewModel(Func<ServiceBusAuthContext, INamespaceProvider> providerFactory)
    {
        _providerFactory  = providerFactory;
        CancelCommand     = new RelayCommand(() => CloseRequested?.Invoke(this, (null, null)));
        ConnectCommand    = new AsyncRelayCommand(OnConnectAsync);
    }

    public event EventHandler<(IReadOnlyList<NamespaceEntity>? entities, ServiceBusAuthContext? authContext)>? CloseRequested;

    private async Task OnConnectAsync()
    {
        try
        {
            IsConnecting = true;
            ErrorMessage = null;

            // Create appropriate auth context based on selected tab
            ServiceBusAuthContext? authContext = null;
            if (SelectedAuthTabIndex == 0)
            {
                // Connection String tab
                if (string.IsNullOrWhiteSpace(ConnectionString))
                {
                    ErrorMessage = "Please enter a connection string.";
                    return;
                }
                authContext = new ConnectionStringAuthContext(ConnectionString);
            }
            else
            {
                // Azure Credentials tab
                if (string.IsNullOrWhiteSpace(Namespace))
                {
                    ErrorMessage = "Please enter a Service Bus namespace.";
                    return;
                }

                // Validate namespace format
                if (!Namespace.EndsWith(".servicebus.windows.net", StringComparison.OrdinalIgnoreCase))
                {
                    ErrorMessage = "Namespace must be in format: myservicebus.servicebus.windows.net";
                    return;
                }

                // Create Interactive Browser Credential
                // This will open a browser window for authentication
                try
                {
                    var credential = new InteractiveBrowserCredential(new InteractiveBrowserCredentialOptions
                    {
                        TenantId = "common", // Allow any tenant (work, school, or personal accounts)
                        ClientId = "04b07795-8ddb-461a-bbee-02f9e1bf7b46", // Azure CLI client ID (well-known, safe to use)
                        RedirectUri = new Uri("http://localhost") // Standard OAuth redirect for desktop apps
                    });

                    authContext = new TokenCredentialAuthContext(Namespace, credential);
                }
                catch (Exception ex)
                {
                    ErrorMessage = $"Failed to create credential: {ex.Message}";
                    return;
                }
            }

            // Test connection and retrieve entities
            await using var provider = _providerFactory(authContext);
            var service = new NamespaceService(provider, authContext);
            var entities = await service.GetEntitiesAsync();

            // Success! Pass both entities and auth context back
            CloseRequested?.Invoke(this, (entities, authContext));
        }
        catch (AuthenticationFailedException ex)
        {
            ErrorMessage = $"Authentication failed: {ex.Message}\n\nPlease ensure you have access to the Service Bus namespace.";
            Console.WriteLine($"Authentication error: {ex}");
        }
        catch (RequestFailedException ex) when (ex.Status == 401 || ex.Status == 403)
        {
            ErrorMessage = "Access denied. You don't have permission to access this Service Bus namespace.\n\n" +
                           "Required role: Azure Service Bus Data Owner, Data Receiver, or Data Sender";
            Console.WriteLine($"Authorization error: {ex}");
        }
        catch (OperationCanceledException)
        {
            ErrorMessage = "Authentication was cancelled. Please try again.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Connection failed: {ex.Message}";
            Console.WriteLine($"Connection error: {ex}");
        }
        finally
        {
            IsConnecting = false;
        }
    }
}
