using System;
using Avalonia;
using Avalonia.ReactiveUI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ServiceBusExplorer.Core;
using ServiceBusExplorer.Infrastructure;

namespace ServiceBusExplorer.UI;

/// <summary>
/// Entry point: builds Generic Host and starts Avalonia.
/// </summary>
internal static class Program
{
    /// <summary>Global DI container</summary>
    public static IServiceProvider Services { get; private set; } = default!;

    [STAThread]
    public static void Main(string[] args)
    {
        // ---------- 1. Generic Host ----------
        var builder = Host.CreateApplicationBuilder(args);
        ConfigureServices(builder.Services);

        using var host = builder.Build();
        Services = host.Services;

        // ---------- 2. Avalonia --------------
        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Core
        // LogService (singleton for app-wide logging)
        services.AddSingleton<ILogService, LogService>();
        
        // NamespaceService factory (needs INamespaceProvider and connection string)
        services.AddTransient<Func<INamespaceProvider, string, NamespaceService>>(sp =>
            (provider, connectionString) => new NamespaceService(provider, connectionString));

        // Factory for INamespaceProvider (needs connection string)
        services.AddTransient<Func<string, INamespaceProvider>>(sp =>
            cs => new AzureNamespaceProvider(cs));

        // ViewModels
        services.AddTransient<ConnectDialogViewModel>();
        services.AddTransient<Func<ConnectDialogViewModel>>(sp =>
            () => sp.GetRequiredService<ConnectDialogViewModel>());
        services.AddTransient<LogViewModel>();
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<MessageService>();
        services.AddTransient<Func<string, IMessagePeekProvider>>(sp =>
            cs => new AzureMessagePeekProvider(cs));
        services.AddTransient<Func<string, IMessageSendProvider>>(sp =>
            cs => new AzureMessageSendProvider(cs));
        services.AddTransient<Func<string, MessageListViewModel>>(sp =>
            cs => new MessageListViewModel(
                sp.GetRequiredService<MessageService>(), 
                cs,
                sp.GetRequiredService<Func<string, string, SendMessageDialogViewModel>>(),
                sp.GetRequiredService<ILogService>(),
                sp.GetRequiredService<Func<string, INamespaceProvider>>()));
        services.AddTransient<Func<string, string, SendMessageDialogViewModel>>(sp =>
            (connectionString, entityPath) => new SendMessageDialogViewModel(
                sp.GetRequiredService<Func<string, IMessageSendProvider>>(),
                connectionString,
                entityPath));
    }

    private static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .UseReactiveUI();   // Remove if not using ReactiveUI
}
