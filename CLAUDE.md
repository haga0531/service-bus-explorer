# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Service Bus Explorer is a cross-platform Azure Service Bus management tool built with .NET 8 and Avalonia UI. It allows users to browse Service Bus namespaces, manage queues/topics/subscriptions, and perform message operations (peek, send, delete, purge, resubmit).

## Development Commands

### Building
```bash
# Restore dependencies
dotnet restore

# Build the solution
dotnet build --configuration Release --no-restore

# Run the UI application
dotnet run --project src/ServiceBusExplorer.UI
```

### Testing
```bash
# Run all tests
dotnet test --configuration Release --no-build --verbosity normal

# Run tests with coverage
dotnet test --configuration Release --no-build --verbosity normal --logger "trx;LogFileName=test-results.trx" --collect:"XPlat Code Coverage"

# Run a specific test
dotnet test --filter "FullyQualifiedName~YourTestName"
```

### Project Structure
```
src/
├── ServiceBusExplorer.Core/          # Business logic and domain models
│   ├── MessageService.cs             # Facade for message operations
│   ├── NamespaceService.cs           # Namespace hierarchy management
│   ├── LogService.cs                 # Application-wide logging
│   └── Models/                       # Domain models (NamespaceNode, NamespaceEntity, LogEntry)
├── ServiceBusExplorer.Infrastructure/ # Azure Service Bus SDK integration
│   ├── Azure*Provider.cs             # Concrete implementations for Azure operations
│   ├── I*Provider.cs                 # Provider interfaces
│   └── Models/                       # DTOs (ServiceBusReceivedMessageDto, PagedResult)
├── ServiceBusExplorer.UI/            # Avalonia UI application
│   ├── Program.cs                    # Entry point with DI configuration
│   ├── ViewModels/                   # MVVM ViewModels using CommunityToolkit.Mvvm
│   ├── *.axaml                       # View files
│   └── Converters/                   # UI value converters
└── ServiceBusExplorer.Tests/         # Unit tests (NUnit, Moq, FluentAssertions)
```

## Architecture Patterns

### Clean Architecture & Dependency Injection

The application follows clean architecture with clear separation between layers:
- **Core**: Contains business logic and domain models. No dependencies on UI or Infrastructure.
- **Infrastructure**: Implements provider interfaces defined in Core, handles Azure Service Bus SDK calls.
- **UI**: Avalonia MVVM application that depends on Core abstractions.

Dependency injection is configured in `Program.cs:34-71` using Microsoft.Extensions.DependencyInjection. Services are registered as singletons, transients, or factories depending on lifetime requirements.

### Provider Pattern

All Azure Service Bus operations are abstracted through provider interfaces:
- `INamespaceProvider`: Lists queues, topics, subscriptions, and their properties
- `IMessagePeekProvider`: Peeks messages (active and dead-letter) with pagination
- `IMessageSendProvider`: Sends messages to queues/topics
- `IMessageDeleteProvider`: Deletes individual messages
- `IMessagePurgeProvider`: Bulk deletes messages (purge operations)
- `IMessageResubmitProvider`: Resubmits dead-letter messages to active queue

Infrastructure layer provides `Azure*Provider` concrete implementations. UI layer receives provider factories via DI and creates instances as needed with connection strings.

### Facade Services

`MessageService` and `NamespaceService` act as facades that hide provider instantiation details from the UI:
- They accept provider factories in constructors
- They instantiate providers with connection strings internally
- They expose simplified async methods to ViewModels
- They handle provider disposal via `IAsyncDisposable`

### MVVM with CommunityToolkit.Mvvm

ViewModels use source generators for property and command binding:
- `[ObservableProperty]` generates property change notifications
- `IRelayCommand` and `IAsyncRelayCommand` for command bindings
- ViewModels are resolved from DI container
- Dialog ViewModels accept dependencies through factories

### Hierarchical Tree Navigation

Namespace entities (queues/topics/subscriptions) are displayed in a hierarchical tree:
- `NamespaceNode` represents tree nodes with parent/child relationships
- Topics use lazy-loading: subscriptions load when topic node expands (see `MainWindowViewModel:240-308`)
- Message counts are loaded on-demand when nodes expand to avoid performance issues
- Auto-forwarding entities are detected and skip message count loading

### Paging and Message Filtering

Messages are displayed with pagination and filtering:
- `PagedResult<T>` provides pagination metadata (page number, page size, total count)
- Three filter modes: All (active + dead-letter), Active Only, Dead-Letter Only
- "All" mode requires special handling to combine active and dead-letter messages across pages (see `MessageService:87-198`)
- Message counts are retrieved efficiently using Azure Management API (`GetRuntimePropertiesAsync`)

## Key Implementation Details

### Connection String Management

Connection strings are stored in `MainWindowViewModel._currentConnectionString` after successful connection. They are passed to service factories when creating provider instances.

### Async/Await Patterns

All Azure Service Bus operations are async:
- Use `CancellationToken` parameters for all async methods
- Always `await` when calling providers
- Use `Task.WhenAll()` for parallel operations (loading multiple entities)
- Use `await using` for provider disposal

### Error Handling and Logging

- `ILogService` provides application-wide logging (LogInfo, LogError)
- ViewModels catch exceptions and display user-friendly error messages via `ErrorMessage` properties
- Avalonia UI binds error messages to visual indicators
- Detailed exceptions are logged for debugging

### Testing with Mocks

Tests use Moq to create mock providers:
- Mock return values with `Setup().ReturnsAsync()`
- Verify method calls with `Verify()`
- Use FluentAssertions for readable assertions: `result.Should().HaveCount(5)`

## Platform-Specific Considerations

### macOS Bundling
When `RuntimeIdentifier` is `osx-x64` or `osx-arm64`:
- `Info.plist` is included as a bundle resource
- `CFBundleExecutable` is set to `ServiceBusExplorer.UI`
- Release builds create `.app` bundles for distribution

### CI/CD
GitHub Actions workflows test on ubuntu-latest, windows-latest, and macos-latest. All platforms must pass before merging.

## Avalonia UI Specifics

- Uses Avalonia 11.3.2 with Fluent theme
- Compiled XAML and compiled bindings enabled for performance
- `Avalonia.Diagnostics` package included in Debug builds only (for DevTools)
- ReactiveUI integration for reactive programming patterns
