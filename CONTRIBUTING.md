# Contributing to Service Bus Explorer

First off, thank you for considering contributing to Service Bus Explorer! It's people like you that make Service Bus Explorer such a great tool.

## Code of Conduct

This project and everyone participating in it is governed by the [Code of Conduct](CODE_OF_CONDUCT.md). By participating, you are expected to uphold this code.

## How Can I Contribute?

### Reporting Bugs

Before creating bug reports, please check existing issues to avoid duplicates. When you create a bug report, include as many details as possible:

- **Use a clear and descriptive title**
- **Describe the exact steps to reproduce the problem**
- **Provide specific examples**
- **Describe the behavior you observed and expected**
- **Include screenshots if possible**
- **Include your environment details** (OS, .NET version, etc.)

### Suggesting Enhancements

Enhancement suggestions are tracked as GitHub issues. When creating an enhancement suggestion:

- **Use a clear and descriptive title**
- **Provide a detailed description of the suggested enhancement**
- **Provide specific examples to demonstrate the steps**
- **Describe the current behavior and expected behavior**
- **Explain why this enhancement would be useful**

### Pull Requests

1. **Fork the repository** and create your branch from `main`
2. **Follow the coding standards** (see below)
3. **Write or update tests** as needed
4. **Ensure all tests pass** by running `dotnet test`
5. **Update documentation** if you're changing functionality
6. **Write a good commit message**

## Development Process

### Getting Started

```bash
# Clone your fork
git clone https://github.com/yourusername/service-bus-explorer-crossplat.git
cd service-bus-explorer-crossplat

# Add upstream remote
git remote add upstream https://github.com/originalowner/service-bus-explorer-crossplat.git

# Create a feature branch
git checkout -b feature/your-feature-name
```

### Building the Project

```bash
# Restore dependencies
dotnet restore

# Build the solution
dotnet build

# Run tests
dotnet test

# Run the application
dotnet run --project src/ServiceBusExplorer.UI
```

### Coding Standards

- **C# Coding Conventions**: Follow the [.NET coding conventions](https://docs.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- **Naming**: Use meaningful and descriptive names
- **Comments**: Write XML documentation for public APIs
- **SOLID Principles**: Follow SOLID principles where applicable
- **Async/Await**: Use async/await for I/O operations
- **Error Handling**: Implement proper error handling and logging

### Project Structure

```
├── ServiceBusExplorer.Core/          # Business logic (no UI dependencies)
├── ServiceBusExplorer.Infrastructure/ # External service integrations
├── ServiceBusExplorer.UI/            # UI layer (Avalonia)
└── ServiceBusExplorer.Tests/         # Unit tests
```

### Testing

- Write unit tests for new functionality
- Ensure existing tests pass
- Aim for high code coverage
- Use meaningful test names that describe what is being tested

Example test structure:
```csharp
[Test]
public void MethodName_StateUnderTest_ExpectedBehavior()
{
    // Arrange
    
    // Act
    
    // Assert
}
```

### Commit Messages

- Use the present tense ("Add feature" not "Added feature")
- Use the imperative mood ("Move cursor to..." not "Moves cursor to...")
- Limit the first line to 72 characters or less
- Reference issues and pull requests liberally after the first line

Example:
```
feat: Add message export functionality

- Add export to JSON and CSV formats
- Support bulk export operations
- Add progress reporting for large exports

Fixes #123
```

### Pull Request Process

1. **Update the README.md** with details of changes if needed
2. **Update the version numbers** if applicable
3. **The PR will be merged** once you have the sign-off of at least one maintainer

## Style Guide

### UI/UX Guidelines

- Follow Avalonia UI design patterns
- Ensure accessibility (keyboard navigation, screen readers)
- Test on all supported platforms (Windows, macOS, Linux)
- Keep the UI responsive during long operations

### Architecture Guidelines

- Keep the Core project free of UI and external dependencies
- Use dependency injection for loose coupling
- Follow the repository pattern for data access
- Implement proper separation of concerns

## Community

- Join our [Discussions](https://github.com/yourusername/service-bus-explorer-crossplat/discussions)
- Follow the project for updates
- Help others in issues

## Recognition

Contributors will be recognized in:
- The project README
- Release notes
- Special contributors file (for significant contributions)

Thank you for contributing! 🎉