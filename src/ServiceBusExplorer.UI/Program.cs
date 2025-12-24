using System;
using Avalonia;
using Avalonia.ReactiveUI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ServiceBusExplorer.Core;
using ServiceBusExplorer.Infrastructure;
using ServiceBusExplorer.Infrastructure.Models;

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

        // Factory for INamespaceProvider (needs auth context)
        services.AddTransient<Func<ServiceBusAuthContext, INamespaceProvider>>(sp =>
            authContext => new AzureNamespaceProvider(authContext));

        // ViewModels
        services.AddTransient<ConnectDialogViewModel>();
        services.AddTransient<Func<ConnectDialogViewModel>>(sp =>
            sp.GetRequiredService<ConnectDialogViewModel>);
        services.AddTransient<LogViewModel>();
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<MessageService>();
        services.AddTransient<Func<ServiceBusAuthContext, IMessagePeekProvider>>(sp =>
            authContext => new AzureMessagePeekProvider(authContext));
        services.AddTransient<Func<ServiceBusAuthContext, IMessageSendProvider>>(sp =>
            authContext => new AzureMessageSendProvider(authContext));
        services.AddTransient<Func<ServiceBusAuthContext, IMessageDeleteProvider>>(sp =>
            authContext => new AzureMessageDeleteProvider(authContext));
        services.AddTransient<Func<ServiceBusAuthContext, IMessagePurgeProvider>>(sp =>
            authContext => new AzureMessagePurgeProvider(authContext));
        services.AddTransient<Func<ServiceBusAuthContext, IMessageResubmitProvider>>(sp =>
            authContext => new AzureMessageResubmitProvider(authContext));
        services.AddTransient<Func<ServiceBusAuthContext, MessageListViewModel>>(sp =>
            authContext => new MessageListViewModel(
                sp.GetRequiredService<MessageService>(),
                authContext,
                sp.GetRequiredService<Func<ServiceBusAuthContext, string, SendMessageDialogViewModel>>(),
                sp.GetRequiredService<ILogService>(),
                sp.GetRequiredService<Func<ServiceBusAuthContext, INamespaceProvider>>()));
        services.AddTransient<Func<ServiceBusAuthContext, string, SendMessageDialogViewModel>>(sp =>
            (authContext, entityPath) => new SendMessageDialogViewModel(
                sp.GetRequiredService<Func<ServiceBusAuthContext, IMessageSendProvider>>(),
                authContext,
                entityPath));
    }

    private static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .UseReactiveUI();   // Remove if not using ReactiveUI
}
