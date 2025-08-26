#nullable disable
using FluentAssertions;
using ServiceBusExplorer.Core.Models;

namespace ServiceBusExplorer.Tests.Core.Models;

[TestFixture]
public class NamespaceNodeTests
{
    [Test]
    public void IsQueue_ShouldReturnTrue_WhenEntityTypeIsQueue()
    {
        // Arrange
        var node = new NamespaceNode { EntityType = NamespaceEntity.EntityType.Queue };

        // Assert
        node.IsQueue.Should().BeTrue();
        node.IsTopic.Should().BeFalse();
        node.IsSubscription.Should().BeFalse();
        node.IsFolder.Should().BeFalse();
    }

    [Test]
    public void IsTopic_ShouldReturnTrue_WhenEntityTypeIsTopic()
    {
        // Arrange
        var node = new NamespaceNode { EntityType = NamespaceEntity.EntityType.Topic };

        // Assert
        node.IsTopic.Should().BeTrue();
        node.IsQueue.Should().BeFalse();
        node.IsSubscription.Should().BeFalse();
        node.IsFolder.Should().BeFalse();
    }

    [Test]
    public void IsSubscription_ShouldReturnTrue_WhenEntityTypeIsSubscription()
    {
        // Arrange
        var node = new NamespaceNode { EntityType = NamespaceEntity.EntityType.Subscription };

        // Assert
        node.IsSubscription.Should().BeTrue();
        node.IsQueue.Should().BeFalse();
        node.IsTopic.Should().BeFalse();
        node.IsFolder.Should().BeFalse();
    }

    [Test]
    public void IsFolder_ShouldReturnTrue_WhenEntityTypeIsNull()
    {
        // Arrange
        var node = new NamespaceNode { EntityType = null };

        // Assert
        node.IsFolder.Should().BeTrue();
        node.IsQueue.Should().BeFalse();
        node.IsTopic.Should().BeFalse();
        node.IsSubscription.Should().BeFalse();
    }

    [Test]
    public void Icon_ShouldReturnCorrectEmoji_ForEachEntityType()
    {
        // Arrange & Act & Assert
        new NamespaceNode { EntityType = null }.Icon.Should().Be("📁");
        new NamespaceNode { EntityType = NamespaceEntity.EntityType.Queue }.Icon.Should().Be("📬");
        new NamespaceNode { EntityType = NamespaceEntity.EntityType.Topic }.Icon.Should().Be("📡");
        new NamespaceNode { EntityType = NamespaceEntity.EntityType.Subscription }.Icon.Should().Be("📥");
    }

    [Test]
    public void DisplayName_ShouldIncludeMessageCounts_WhenLoaded()
    {
        // Arrange
        var node = new NamespaceNode
        {
            Name = "TestQueue",
            EntityType = NamespaceEntity.EntityType.Queue,
            MessageCountsLoaded = true,
            ActiveMessageCount = 10,
            DeadLetterMessageCount = 5
        };

        // Act & Assert
        node.DisplayName.Should().Be("TestQueue (10, 5)");
    }

    [Test]
    public void DisplayName_ShouldShowOnlyActiveCount_WhenNoDeadLetterMessages()
    {
        // Arrange
        var node = new NamespaceNode
        {
            Name = "TestQueue",
            EntityType = NamespaceEntity.EntityType.Queue,
            MessageCountsLoaded = true,
            ActiveMessageCount = 10,
            DeadLetterMessageCount = 0
        };

        // Act & Assert
        node.DisplayName.Should().Be("TestQueue (10, 0)");
    }

    [Test]
    public void DisplayName_ShouldShowOnlyName_WhenMessageCountsNotLoaded()
    {
        // Arrange
        var node = new NamespaceNode
        {
            Name = "TestQueue",
            EntityType = NamespaceEntity.EntityType.Queue,
            MessageCountsLoaded = false
        };

        // Act & Assert
        node.DisplayName.Should().Be("TestQueue");
    }

    [Test]
    public void DisplayName_ShouldShowOnlyName_ForFolders()
    {
        // Arrange
        var node = new NamespaceNode
        {
            Name = "FolderName",
            EntityType = null,
            MessageCountsLoaded = true,
            ActiveMessageCount = 10
        };

        // Act & Assert
        node.DisplayName.Should().Be("FolderName");
    }

    [Test]
    public void HasAutoForwarding_WhenChanged_ShouldRaisePropertyChangedEvent()
    {
        // Arrange
        var node = new NamespaceNode();
        var raisedProperties = new List<string>();

        node.PropertyChanged += (sender, e) =>
        {
            raisedProperties.Add(e.PropertyName);
        };

        // Act
        node.HasAutoForwarding = true;

        // Assert
        raisedProperties.Should().Contain(nameof(NamespaceNode.HasAutoForwarding));
        raisedProperties.Should().Contain(nameof(NamespaceNode.DisplayName));
    }

    [Test]
    public void ActiveMessageCount_WhenChanged_ShouldRaisePropertyChangedForDisplayName()
    {
        // Arrange
        var node = new NamespaceNode
        {
            Name = "TestQueue",
            EntityType = NamespaceEntity.EntityType.Queue,
            MessageCountsLoaded = true
        };

        var raisedProperties = new List<string>();
        node.PropertyChanged += (sender, e) => raisedProperties.Add(e.PropertyName!);

        // Act
        node.ActiveMessageCount = 100;

        // Assert
        raisedProperties.Should().Contain(nameof(NamespaceNode.ActiveMessageCount));
        raisedProperties.Should().Contain(nameof(NamespaceNode.DisplayName));
    }

    [Test]
    public void DeadLetterMessageCount_WhenChanged_ShouldRaisePropertyChangedForDisplayName()
    {
        // Arrange
        var node = new NamespaceNode
        {
            Name = "TestQueue",
            EntityType = NamespaceEntity.EntityType.Queue,
            MessageCountsLoaded = true
        };

        var raisedProperties = new List<string>();
        node.PropertyChanged += (sender, e) => raisedProperties.Add(e.PropertyName!);

        // Act
        node.DeadLetterMessageCount = 50;

        // Assert
        raisedProperties.Should().Contain(nameof(NamespaceNode.DeadLetterMessageCount));
        raisedProperties.Should().Contain(nameof(NamespaceNode.DisplayName));
    }

    [Test]
    public void MessageCountsLoaded_WhenChanged_ShouldRaisePropertyChangedForDisplayName()
    {
        // Arrange
        var node = new NamespaceNode
        {
            Name = "TestQueue",
            EntityType = NamespaceEntity.EntityType.Queue,
            ActiveMessageCount = 10,
            DeadLetterMessageCount = 5
        };

        var raisedProperties = new List<string>();
        node.PropertyChanged += (sender, e) => raisedProperties.Add(e.PropertyName!);

        // Act
        node.MessageCountsLoaded = true;

        // Assert
        raisedProperties.Should().Contain(nameof(NamespaceNode.MessageCountsLoaded));
        raisedProperties.Should().Contain(nameof(NamespaceNode.DisplayName));
    }
}

