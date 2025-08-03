# Service Bus Explorer Cross-Platform

A modern, cross-platform Azure Service Bus management tool built with .NET 8 and Avalonia UI.

<img width="1728" height="1084" alt="スクリーンショット 2025-08-03 21 44 19" src="https://github.com/user-attachments/assets/3eb3ada0-c27c-417b-a143-0610687b2dcb" />

## Features

- **Cross-Platform**: Works on Windows, macOS, and Linux
- **Modern UI**: Built with Avalonia UI for a native experience on all platforms
- **Comprehensive Management**: 
  - Browse Service Bus namespaces, queues, topics, and subscriptions
  - View, send, edit, and delete messages
  - Peek active and dead-letter messages
  - Bulk operations (purge, resubmit)
  - Real-time message counts
- **Developer Friendly**:
  - Message content editing with syntax highlighting
  - Import/Export messages
  - Detailed message properties and metadata
  - Application logging with filtering

## Getting Started

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Azure Service Bus namespace with connection string

### Installation

#### Option 1: Homebrew (macOS/Linux) 🍺 **[Recommended]**

```bash
# Install with Homebrew
brew tap haga0531/service-bus-explorer
brew install service-bus-explorer

# Launch
service-bus-explorer
```

#### Option 2: Download Pre-built Binaries

Download the latest release from the [Releases](https://github.com/haga0531/service-bus-explorer/releases) page.

**macOS:**
```bash
# Download and extract
tar -xzf ServiceBusExplorer-osx-x64.tar.gz    # Intel Mac
# or
tar -xzf ServiceBusExplorer-osx-arm64.tar.gz  # Apple Silicon

# The archive contains "Service Bus Explorer.app"
# Simply drag it to your Applications folder!
```

**Windows:**
```powershell
# Extract the zip file and run
ServiceBusExplorer.UI.exe
```

**Linux:**
```bash
# Extract and run
tar -xzf ServiceBusExplorer-linux-x64.tar.gz
cd ServiceBusExplorer-linux-x64
chmod +x ServiceBusExplorer.UI
./ServiceBusExplorer.UI
```

#### Option 3: Build from Source

```bash
# Clone the repository
git clone https://github.com/haga0531/service-bus-explorer.git
cd service-bus-explorer-crossplat

# Build and run
dotnet run --project src/ServiceBusExplorer.UI
```

### Usage

1. **Connect to Service Bus**:
   - Click the "Connect" button
   - Enter your Service Bus connection string
   - The namespace tree will populate with your queues and topics

2. **Browse Messages**:
   - Select a queue or subscription from the tree
   - Messages will load automatically
   - Use the toolbar to peek more messages or switch between active/dead-letter

3. **Message Operations**:
   - **Send**: Right-click on a queue/topic and select "Send Message"
   - **Edit**: Double-click a message to view/edit its content
   - **Delete**: Select messages and press Delete or use the context menu
   - **Resubmit**: Send dead-letter messages back to the main queue

## Architecture

The application follows a clean architecture pattern with clear separation of concerns:

```
├── ServiceBusExplorer.Core/          # Business logic and domain models
├── ServiceBusExplorer.Infrastructure/ # Azure Service Bus integration
├── ServiceBusExplorer.UI/            # Avalonia UI application
└── ServiceBusExplorer.Tests/         # Unit tests
```

### Technology Stack

- **UI Framework**: [Avalonia UI](https://avaloniaui.net/) 11.0
- **MVVM**: [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet)
- **Azure SDK**: [Azure.Messaging.ServiceBus](https://github.com/Azure/azure-sdk-for-net)
- **Testing**: NUnit, FluentAssertions, Moq
- **CI/CD**: GitHub Actions


## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Security

For security concerns, please see our [Security Policy](SECURITY.md).

## Acknowledgments

- Inspired by the original [Service Bus Explorer](https://github.com/paolosalvatori/ServiceBusExplorer)
- Built with [Avalonia UI](https://avaloniaui.net/)
- Icons from [Avalonia UI Icons](https://avaloniaui.github.io/icons.html)
