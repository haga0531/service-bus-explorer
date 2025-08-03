#nullable disable
using FluentAssertions;
using ServiceBusExplorer.Core;
using ServiceBusExplorer.Core.Models;

namespace ServiceBusExplorer.Tests.Core;

[TestFixture]
public class LogServiceTests
{
    private LogService _sut;
    private TextWriter _originalOut;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        // Suppress console output for all tests in this fixture
        _originalOut = Console.Out;
        Console.SetOut(TextWriter.Null);
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        // Restore console output
        Console.SetOut(_originalOut);
    }

    [SetUp]
    public void SetUp()
    {
        _sut = new LogService();
    }

    [Test]
    public void Log_ShouldAddLogEntryWithCorrectProperties()
    {
        // Arrange
        var level = LogLevel.Info;
        var source = "TestSource";
        var message = "Test message";
        
        // Act
        _sut.Log(level, source, message);
        
        // Assert
        var logs = _sut.GetLogs();
        logs.Should().HaveCount(1);
        
        var logEntry = logs.First();
        logEntry.Level.Should().Be(level);
        logEntry.Source.Should().Be(source);
        logEntry.Message.Should().Be(message);
        logEntry.Exception.Should().BeNull();
        logEntry.Timestamp.Should().BeCloseTo(DateTime.Now, TimeSpan.FromSeconds(1));
    }

    [Test]
    public void Log_ShouldRaiseLogAddedEvent()
    {
        // Arrange
        LogEntry capturedEntry = null;
        _sut.LogAdded += (sender, entry) => capturedEntry = entry;
        
        // Act
        _sut.LogInfo("TestSource", "Test message");
        
        // Assert
        capturedEntry.Should().NotBeNull();
        capturedEntry.Level.Should().Be(LogLevel.Info);
        capturedEntry.Message.Should().Be("Test message");
    }

    [Test]
    public void LogInfo_ShouldAddInfoLevelLog()
    {
        // Arrange & Act
        _sut.LogInfo("TestSource", "Info message");
        
        // Assert
        var logs = _sut.GetLogs();
        logs.Should().HaveCount(1);
        logs.First().Level.Should().Be(LogLevel.Info);
    }

    [Test]
    public void LogWarning_ShouldAddWarningLevelLog()
    {
        // Arrange & Act
        _sut.LogWarning("TestSource", "Warning message");
        
        // Assert
        var logs = _sut.GetLogs();
        logs.Should().HaveCount(1);
        logs.First().Level.Should().Be(LogLevel.Warning);
    }

    [Test]
    public void LogError_ShouldAddErrorLevelLogWithException()
    {
        // Arrange
        var exception = new InvalidOperationException("Test exception");
        
        // Act
        _sut.LogError("TestSource", "Error message", exception);
        
        // Assert
        var logs = _sut.GetLogs();
        logs.Should().HaveCount(1);
        
        var logEntry = logs.First();
        logEntry.Level.Should().Be(LogLevel.Error);
        logEntry.Exception.Should().Be(exception);
    }

    [Test]
    public void Clear_ShouldRemoveAllLogs()
    {
        // Arrange
        _sut.LogInfo("Source1", "Message1");
        _sut.LogWarning("Source2", "Message2");
        _sut.LogError("Source3", "Message3");
        
        // Act
        _sut.Clear();
        
        // Assert
        _sut.GetLogs().Should().BeEmpty();
    }

    [Test]
    public void Log_ShouldMaintainMaxLogCount()
    {
        // Arrange
        const int maxLogs = 10000;
        const int totalLogs = 10100;
        
        // Act
        for (int i = 0; i < totalLogs; i++)
        {
            _sut.LogInfo("TestSource", $"Message {i}");
        }
        
        // Assert
        var logs = _sut.GetLogs();
        logs.Should().HaveCount(maxLogs);
        
        // The oldest logs should have been removed
        logs.First().Message.Should().Be("Message 100");
        logs.Last().Message.Should().Be("Message 10099");
    }

    [Test]
    public void GetLogs_ShouldReturnReadOnlyCollection()
    {
        // Arrange
        _sut.LogInfo("TestSource", "Message");
        
        // Act
        var logs = _sut.GetLogs();
        
        // Assert
        logs.Should().BeAssignableTo<IReadOnlyList<LogEntry>>();
    }
}

