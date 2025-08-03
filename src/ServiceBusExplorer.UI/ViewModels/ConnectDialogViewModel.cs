using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ServiceBusExplorer.Core;
using ServiceBusExplorer.Core.Models;
using ServiceBusExplorer.Infrastructure;

namespace ServiceBusExplorer.UI;

public partial class ConnectDialogViewModel : ObservableObject
{
    [ObservableProperty] private string? connectionString;
    [ObservableProperty] private bool isConnecting = false;
    [ObservableProperty] private string? errorMessage;

    public IRelayCommand CancelCommand  { get; }
    public IRelayCommand ConnectCommand { get; }

    private readonly Func<string, INamespaceProvider> _providerFactory;

    public ConnectDialogViewModel(Func<string, INamespaceProvider> providerFactory)
    {
        _providerFactory  = providerFactory;
        CancelCommand     = new RelayCommand(() => CloseRequested?.Invoke(this, null));
        ConnectCommand    = new AsyncRelayCommand(OnConnectAsync);
    }

    /*--- Dialog -> Notify result to caller ---*/
    public event EventHandler<IReadOnlyList<NamespaceEntity>?>? CloseRequested;

    private async Task OnConnectAsync()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            ErrorMessage = "Please enter a connection string.";
            return;
        }

        try
        {
            IsConnecting = true;
            ErrorMessage = null;
            
            await using var provider = _providerFactory(ConnectionString);
            var service   = new NamespaceService(provider, ConnectionString);
            var entities  = await service.GetEntitiesAsync();
            CloseRequested?.Invoke(this, entities);
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
