# Service Bus Explorer - Copilot Instructions

**ALWAYS follow these instructions first and only fallback to additional search and context gathering if the information here is incomplete or found to be in error.**

Service Bus Explorer is a cross-platform Azure Service Bus management tool built with .NET 8 and Avalonia UI. It provides a modern desktop interface for browsing namespaces, managing queues/topics/subscriptions, and handling messages across Windows, macOS, and Linux.

## Working Effectively

### Bootstrap and Build
Run these commands in sequence to set up the development environment:

```bash
# Restore dependencies - takes ~20 seconds. NEVER CANCEL. Set timeout to 60+ seconds.
dotnet restore

# Build the solution - takes ~15 seconds. NEVER CANCEL. Set timeout to 30+ seconds.
dotnet build --configuration Release --no-restore

# Run tests - takes ~3 seconds for 42 tests. NEVER CANCEL. Set timeout to 30+ seconds.
dotnet test --configuration Release --no-build --verbosity normal
```

### Development Commands
```bash
# Run the application (fails in headless environments - this is expected)
dotnet run --project src/ServiceBusExplorer.UI

# Format code - takes ~15 seconds. ALWAYS run before committing.
dotnet format

# Verify formatting compliance
dotnet format --verify-no-changes

# Build single-file release for Linux - takes ~8 seconds
dotnet publish src/ServiceBusExplorer.UI/ServiceBusExplorer.UI.csproj --configuration Release --runtime linux-x64 --self-contained true --output ./publish/linux-x64 -p:PublishSingleFile=true -p:DebugType=None -p:DebugSymbols=false
```

### Build Times and Timeouts
- **Restore**: 18-20 seconds - NEVER CANCEL, set timeout to 60+ seconds
- **Build**: 14-15 seconds - NEVER CANCEL, set timeout to 30+ seconds  
- **Test**: 3 seconds (42 tests) - NEVER CANCEL, set timeout to 30+ seconds
- **Format**: 15 seconds - NEVER CANCEL, set timeout to 30+ seconds
- **Publish**: 8 seconds - NEVER CANCEL, set timeout to 30+ seconds

## Validation Scenarios

### After Making Changes
ALWAYS run these validation steps in order before considering changes complete:

1. **Build Validation**:
   ```bash
   dotnet build --configuration Release --no-restore
   ```
   Should complete without errors or warnings.

2. **Test Validation**:
   ```bash
   dotnet test --configuration Release --no-build --verbosity normal
   ```
   All 42 tests must pass. DO NOT modify code to make unrelated failing tests pass.

3. **Format Validation**:
   ```bash
   dotnet format --verify-no-changes
   ```
   Should complete without formatting errors. Naming convention warnings (IDE1006) are acceptable.

4. **Application Startup**:
   ```bash
   timeout 5s dotnet run --project src/ServiceBusExplorer.UI 2>&1 || echo "Expected failure"
   ```
   Should start but fail with "XOpenDisplay failed" in headless environments - this is expected.

### Manual Testing Scenarios
When the application can run with a display:

1. **Connection Flow**: Start app → Click "Connect" → Enter valid Azure Service Bus connection string → Verify namespace tree populates
2. **Message Browsing**: Select queue/subscription → Verify messages load → Test active/dead-letter toggle
3. **Message Operations**: Right-click queue → Send message → View message details → Delete messages

## CI/CD Integration
The project uses GitHub Actions with cross-platform builds. CI runs:
- `dotnet restore`
- `dotnet build --configuration Release --no-restore` 
- `dotnet test --configuration Release --no-build --verbosity normal`

ALWAYS run `dotnet format` before committing or CI will fail on formatting checks.

## Project Structure

### Key Projects
- **ServiceBusExplorer.Core** - Business logic, domain models (LogService, MessageService, NamespaceService)
- **ServiceBusExplorer.Infrastructure** - Azure Service Bus integration (all Azure*Provider classes)
- **ServiceBusExplorer.UI** - Avalonia UI application (ViewModels, Views, Converters)
- **ServiceBusExplorer.Tests** - Unit tests (NUnit, FluentAssertions, Moq)

### Important Files
- `ServiceBusExplorer.sln` - Main solution file
- `src/Directory.Build.props` - Common build properties and code analyzers
- `src/.editorconfig` - Code style and formatting rules
- `src/.globalconfig` - Code analysis rule configuration
- `.github/workflows/ci.yml` - CI pipeline
- `.github/workflows/release.yml` - Release pipeline with cross-platform publishing

### Key Directories
```
├── src/
│   ├── ServiceBusExplorer.Core/          # Business logic
│   ├── ServiceBusExplorer.Infrastructure/ # Azure integration  
│   ├── ServiceBusExplorer.UI/            # Avalonia UI app
│   └── ServiceBusExplorer.Tests/         # Unit tests
├── scripts/                              # Build and packaging scripts
├── docs/                                 # Documentation
└── .github/workflows/                    # CI/CD pipelines
```

## Common Tasks

### Adding New Features
1. Add business logic to `ServiceBusExplorer.Core`
2. Add Azure integration to `ServiceBusExplorer.Infrastructure` if needed
3. Add UI components to `ServiceBusExplorer.UI/ViewModels` and corresponding views
4. Add tests to `ServiceBusExplorer.Tests` following existing patterns
5. ALWAYS run validation steps before committing

### Code Style
- Use the existing .editorconfig settings
- Follow MVVM pattern for UI components
- Use dependency injection (Microsoft.Extensions.Hosting)
- Async methods must end with "Async" suffix
- Interface names must start with "I"

### Testing Patterns
- Use NUnit for test framework
- Use FluentAssertions for assertions
- Use Moq for mocking dependencies
- Follow AAA pattern (Arrange, Act, Assert)
- Test classes in `ServiceBusExplorer.Tests` mirror the source structure

### Troubleshooting
- **Build failures**: Check that .NET 8 SDK is installed
- **Format failures**: Run `dotnet format` to fix, then `dotnet format --verify-no-changes`
- **Test failures**: Run tests with `--verbosity normal` to see detailed output
- **UI startup issues**: Expected in headless environments; check for actual logic errors
- **Azure connection issues**: Verify connection string format and permissions

## Technology Stack
- **Framework**: .NET 8
- **UI**: Avalonia UI 11.3.2 with MVVM pattern
- **Cloud**: Azure.Messaging.ServiceBus
- **Testing**: NUnit 4.0.1, FluentAssertions 6.12.0, Moq 4.20.70
- **Build**: MSBuild with Microsoft.CodeAnalysis.NetAnalyzers

## Common Commands Reference
```bash
# Repository-wide commands (run from root)
dotnet restore                             # Install dependencies
dotnet build --configuration Release      # Build all projects  
dotnet test --verbosity normal            # Run all tests
dotnet format                              # Fix code formatting
dotnet publish src/ServiceBusExplorer.UI/ServiceBusExplorer.UI.csproj --configuration Release --runtime linux-x64 --self-contained true --output ./publish/linux-x64 -p:PublishSingleFile=true

# Project-specific commands
dotnet run --project src/ServiceBusExplorer.UI                    # Run the application
dotnet test src/ServiceBusExplorer.Tests --verbosity normal       # Run tests only
dotnet build src/ServiceBusExplorer.Core --configuration Release  # Build single project
```

Remember: ALWAYS validate your changes with the complete build/test/format sequence before committing.