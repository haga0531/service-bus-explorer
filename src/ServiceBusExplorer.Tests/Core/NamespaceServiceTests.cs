#nullable disable
using FluentAssertions;
using Moq;
using ServiceBusExplorer.Core;
using ServiceBusExplorer.Core.Models;
using ServiceBusExplorer.Infrastructure;

namespace ServiceBusExplorer.Tests.Core;

[TestFixture]
public class NamespaceServiceTests
{
    private Mock<INamespaceProvider> _mockProvider;
    private NamespaceService _sut;
    private const string ConnectionString = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=test;SharedAccessKey=test";

    [SetUp]
    public void SetUp()
    {
        _mockProvider = new Mock<INamespaceProvider>();
        _sut = new NamespaceService(_mockProvider.Object, ConnectionString);
    }

    [Test]
    public async Task GetEntitiesAsync_ShouldReturnQueuesAndTopics()
    {
        // Arrange
        var queues = new[] { "queue1", "queue2" };
        var topics = new[] { "topic1", "topic2" };

        _mockProvider.Setup(x => x.GetQueuesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(queues);
        _mockProvider.Setup(x => x.GetTopicsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(topics);

        // Act
        var result = await _sut.GetEntitiesAsync();

        // Assert
        result.Should().HaveCount(4);
        result.Count(e => e.Type == NamespaceEntity.EntityType.Queue).Should().Be(2);
        result.Count(e => e.Type == NamespaceEntity.EntityType.Topic).Should().Be(2);
        result.Select(e => e.Name).Should().BeEquivalentTo(new[] { "queue1", "queue2", "topic1", "topic2" });
    }

    [Test]
    public async Task GetNodesAsync_ShouldReturnHierarchicalStructure_WithoutMessageCounts()
    {
        // Arrange
        var queues = new[] { "queue1", "folder/queue2" };
        var topics = new[] { "topic1" };

        _mockProvider.Setup(x => x.GetQueuesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(queues);
        _mockProvider.Setup(x => x.GetTopicsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(topics);

        // Act
        var result = await _sut.GetNodesAsync(includeMessageCounts: false);

        // Assert
        result.Should().HaveCount(2);

        var queuesNode = result.First(n => n.Name == "Queues");
        queuesNode.IsFolder.Should().BeTrue();
        queuesNode.Children.Should().HaveCount(2);

        var topicsNode = result.First(n => n.Name == "Topics");
        topicsNode.IsFolder.Should().BeTrue();
        topicsNode.Children.Should().HaveCount(1);
    }

    [Test]
    public async Task GetNodesAsync_ShouldCreateFolderStructureForNestedQueues()
    {
        // Arrange
        var queues = new[] { "folder1/folder2/queue1", "folder1/queue2" };

        _mockProvider.Setup(x => x.GetQueuesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(queues);
        _mockProvider.Setup(x => x.GetTopicsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<string>());

        // Act
        var result = await _sut.GetNodesAsync(includeMessageCounts: false);

        // Assert
        var queuesNode = result.First(n => n.Name == "Queues");
        queuesNode.Children.Should().HaveCount(1);

        var folder1 = queuesNode.Children.First();
        folder1.Name.Should().Be("folder1");
        folder1.IsFolder.Should().BeTrue();
        folder1.Children.Should().HaveCount(2);

        var folder2 = folder1.Children.FirstOrDefault(n => n.Name == "folder2");
        folder2.Should().NotBeNull();
        folder2!.IsFolder.Should().BeTrue();
        folder2.Children.Should().HaveCount(1);
        folder2.Children.First().Name.Should().Be("queue1");
    }

    [Test]
    public async Task GetNodesAsync_ShouldAddLoadingPlaceholderForTopics()
    {
        // Arrange
        var topics = new[] { "topic1" };

        _mockProvider.Setup(x => x.GetQueuesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<string>());
        _mockProvider.Setup(x => x.GetTopicsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(topics);

        // Act
        var result = await _sut.GetNodesAsync(includeMessageCounts: false);

        // Assert
        var topicsNode = result.First(n => n.Name == "Topics");
        var topic1 = topicsNode.Children.First();

        topic1.Children.Should().HaveCount(1);
        topic1.Children.First().Name.Should().Be("Loading...");
        topic1.Children.First().EntityType.Should().BeNull();
    }

    [Test]
    public async Task GetNodesAsync_ShouldCreateNodesWithoutAutoForwardingCheck()
    {
        // Arrange
        var queues = new[] { "queue1" };

        _mockProvider.Setup(x => x.GetQueuesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(queues);
        _mockProvider.Setup(x => x.GetTopicsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<string>());

        // Act
        var result = await _sut.GetNodesAsync(includeMessageCounts: true);

        // Assert
        var queuesNode = result.First(n => n.Name == "Queues");
        var queue1 = queuesNode.Children.First();

        // Auto-forwarding and message counts are not loaded in GetNodesAsync
        queue1.HasAutoForwarding.Should().BeFalse();
        queue1.MessageCountsLoaded.Should().BeFalse();
    }

    [Test]
    public async Task GetNodesAsync_ShouldHandleEmptyNamespace()
    {
        // Arrange
        _mockProvider.Setup(x => x.GetQueuesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<string>());
        _mockProvider.Setup(x => x.GetTopicsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<string>());

        // Act
        var result = await _sut.GetNodesAsync();

        // Assert
        result.Should().BeEmpty();
    }
}

