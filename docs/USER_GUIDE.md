# Service Bus Explorer User Guide

## 📋 Table of Contents

1. [Installation](#installation)
2. [Getting Started](#getting-started)
3. [Basic Usage](#basic-usage)
4. [Common Operations](#common-operations)
5. [Troubleshooting](#troubleshooting)
6. [Updates and Uninstallation](#updates-and-uninstallation)

## Installation

### Prerequisites
- macOS 10.15+ or Linux (Ubuntu 20.04+ recommended)
- Homebrew installed ([installation guide](https://brew.sh/))

### Installation Steps

```bash
# 1. Add Homebrew Tap (first time only)
brew tap haga0531/service-bus-explorer

# 2. Install Service Bus Explorer
brew install service-bus-explorer
```

Installation completes in a few minutes.

## Getting Started

### Launch from Command Line

```bash
service-bus-explorer
```

### Additional Options for macOS

```bash
# Add to Applications folder (first time only)
ln -s "/opt/homebrew/opt/service-bus-explorer/Service Bus Explorer.app" /Applications/

# Then launch from Launchpad or Finder
```

## Basic Usage

### 1. Connect to Service Bus

1. Launch the application
2. Click **Connect** button
3. Enter Connection String
   ```
   Endpoint=sb://your-namespace.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=your-key
   ```
4. Click **Connect**

### 2. Explore Namespace

Once connected, the tree view on the left shows:

```
📁 Your Namespace
├── 📁 Queues
│   ├── queue1
│   └── queue2
└── 📁 Topics
    ├── topic1
    │   ├── subscription1
    │   └── subscription2
    └── topic2
```

### 3. View Messages

1. Select a Queue or Subscription
2. Message list appears on the right
3. Click **Peek more messages** to fetch more
4. Toggle **Show dead letters** to view dead letter messages

## Common Operations

### Send Messages

1. Right-click on a Queue or Topic
2. Select **Send Message**
3. Enter message content
4. Set Properties (optional)
5. Click **Send**

### Delete Messages

1. Select message(s) (multi-select supported)
2. Click **Delete** button or use right-click menu
3. Confirm with **Yes**

### Resubmit Messages (Dead Letter Queue)

1. Select Dead Letter Queue messages
2. Right-click and select **Resubmit**
3. Messages are sent back to the original queue

### Bulk Delete (Purge)

⚠️ **Warning**: This operation cannot be undone

1. Right-click on Queue or Subscription
2. Select **Purge**
3. Choose deletion target:
   - Active messages only
   - Dead letter messages only
   - All messages
4. Confirm with **Purge**

### Edit and Resend Messages

1. Double-click a message
2. Edit content
3. Click **Send as New**

## Troubleshooting

### Connection Issues

#### Symptom
"Failed to connect to Service Bus namespace" error

#### Solutions
1. Verify Connection String is correct
2. Check network connectivity
3. Ensure VPN is connected if required
4. Check firewall settings

### Application Won't Start

#### macOS
```bash
# Resolve permission issues
xattr -cr "/opt/homebrew/opt/service-bus-explorer/Service Bus Explorer.app"

# Try launching again
service-bus-explorer
```

#### All Operating Systems
```bash
# Check .NET runtime
dotnet --version

# If not installed
brew install dotnet
```

### Check Logs

View error messages in the **Logs** panel at the bottom of the application.

## Updates and Uninstallation

### Update

When a new version is released:

```bash
# Update Homebrew
brew update

# Update Service Bus Explorer
brew upgrade service-bus-explorer
```

### Check Version

```bash
brew info service-bus-explorer
```

### Uninstall

```bash
# Uninstall Service Bus Explorer
brew uninstall service-bus-explorer

# Optionally remove tap
brew untap haga0531/service-bus-explorer
```

## Keyboard Shortcuts

| Action | Shortcut |
|--------|----------|
| Connect | `Cmd+K` (Mac) / `Ctrl+K` (Linux) |
| Refresh | `Cmd+R` (Mac) / `Ctrl+R` (Linux) |
| Delete Message | `Delete` |
| Select All | `Cmd+A` (Mac) / `Ctrl+A` (Linux) |
| Copy | `Cmd+C` (Mac) / `Ctrl+C` (Linux) |

## Support

### Report Issues

GitHub Issues: https://github.com/haga0531/service-bus-explorer/issues

### Internal Support

- Slack: #service-bus-support
- Email: servicebus-support@company.com

## Tips & Tricks

### 1. Manage Multiple Namespaces

Launch multiple instances in different terminal windows:

```bash
# Terminal 1
service-bus-explorer  # Production

# Terminal 2  
service-bus-explorer  # Staging
```

### 2. Message Filtering

- Use the search box in message list
- Regular expressions supported

### 3. Export Messages

1. Select messages
2. Right-click → **Export**
3. Save as JSON or CSV

### 4. Performance Tips

- Limit fetch size for queues with many messages
- Collapse unused queues
- Clear logs periodically

---

*Last updated: August 2024*