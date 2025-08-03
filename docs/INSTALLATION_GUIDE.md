# Service Bus Explorer Installation Guide

## Overview

Service Bus Explorer is a cross-platform tool for managing Azure Service Bus with ease.

## System Requirements

### macOS
- macOS 10.15 (Catalina) or later
- Apple Silicon (M1/M2) and Intel Mac supported

### Linux
- Ubuntu 20.04 or later
- Debian 11 or later
- Other major distributions

### Windows
- Windows 10 or later
- Windows Server 2019 or later

## Installation Methods

### macOS / Linux (Homebrew)

#### Prerequisites
- Homebrew installed
  ```bash
  # Install Homebrew (if not installed)
  /bin/bash -c "$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh)"
  ```

#### Installation Steps

1. **Add Tap**
   ```bash
   brew tap haga0531/service-bus-explorer
   ```

2. **Install**
   ```bash
   brew install service-bus-explorer
   ```

3. **Verify**
   ```bash
   # Check installation
   which service-bus-explorer
   
   # Check version
   brew info service-bus-explorer
   ```

### Windows

1. Download latest `ServiceBusExplorer-win-x64.zip` from [Releases](https://github.com/haga0531/service-bus-explorer/releases)
2. Extract to any folder
3. Run `ServiceBusExplorer.UI.exe`

### Other Methods

#### Build from Source

```bash
# Clone repository
git clone https://github.com/haga0531/service-bus-explorer.git
cd service-bus-explorer-crossplat

# Build and run
dotnet run --project src/ServiceBusExplorer.UI
```

## Initial Setup

### macOS

1. **Security Settings**
   
   If you see "unidentified developer" warning on first launch:
   
   ```bash
   # Grant permissions from command line
   xattr -cr "/opt/homebrew/opt/service-bus-explorer/Service Bus Explorer.app"
   ```
   
   Or:
   - Right-click the app → Select "Open"
   - Click "Open" in the warning dialog

2. **Add to Applications (Optional)**
   
   ```bash
   ln -s "/opt/homebrew/opt/service-bus-explorer/Service Bus Explorer.app" /Applications/
   ```

### Linux

Verify execution permissions:
```bash
ls -la $(which service-bus-explorer)
```

## Connection Setup

### Getting Connection String

1. Sign in to Azure Portal
2. Navigate to your Service Bus Namespace
3. Go to **Settings** → **Shared access policies**
4. Click **RootManageSharedAccessKey**
5. Copy **Primary Connection String**

### Connection Steps

1. Launch Service Bus Explorer
2. Click **Connect** button
3. Paste Connection String
4. Click **Connect**

## Updates

### Using Homebrew

```bash
# Update Homebrew
brew update

# Update Service Bus Explorer
brew upgrade service-bus-explorer
```

### Windows

1. Download latest version
2. Replace existing files

## Uninstallation

### Using Homebrew

```bash
# Uninstall
brew uninstall service-bus-explorer

# Remove tap (optional)
brew untap haga0531/service-bus-explorer
```

### Windows

Delete the installation folder

## Troubleshooting

### Application Won't Start

1. **Check .NET Runtime**
   ```bash
   dotnet --version
   ```
   
   If not installed:
   ```bash
   # macOS/Linux
   brew install dotnet
   
   # Windows
   winget install Microsoft.DotNet.Runtime.8
   ```

2. **Check Logs**
   - Application Logs panel
   - `~/Library/Logs/ServiceBusExplorer/` (macOS)
   - `~/.local/share/ServiceBusExplorer/logs/` (Linux)

### Connection Errors

1. **Network Connection**
   - Verify internet connectivity
   - Check VPN if on corporate network

2. **Firewall**
   - Ensure port 5671 (AMQPS) is open
   - Set proxy environment variables if needed

3. **Connection String**
   - Verify format is correct
   - Check permissions (Listen/Send/Manage)

## Support

### Internal Resources

- **Slack**: #service-bus-support
- **Wiki**: [Internal Wiki URL]
- **Email**: servicebus-support@company.com

### External Resources

- **GitHub Issues**: https://github.com/haga0531/service-bus-explorer/issues
- **Azure Service Bus Docs**: https://docs.microsoft.com/en-us/azure/service-bus-messaging/

## Appendix

### Environment Variables

| Variable | Description | Example |
|----------|-------------|---------|
| `HTTPS_PROXY` | HTTPS proxy server | `http://proxy.company.com:8080` |
| `NO_PROXY` | Hosts to bypass proxy | `*.servicebus.windows.net` |

### Log Levels

Set log level on startup:
```bash
export LOGGING__LOGLEVEL__DEFAULT=Debug
service-bus-explorer
```

---

*Last updated: August 2024*