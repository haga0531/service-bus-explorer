#nullable disable
using System.ComponentModel;
using FluentAssertions;
using ServiceBusExplorer.UI.Models;

namespace ServiceBusExplorer.Tests.UI.Models;

[TestFixture]
public class MessageViewModelTests
{
    [Test]
    public void Constructor_ShouldSetPropertiesCorrectly()
    {
        // Arrange
        var messageId = "msg123";
        var label = "test-label";
        var contentType = "application/json";
        var enqueuedTime = DateTimeOffset.Now;
        var body = "test body";
        var isDeadLetter = true;

        // Act
        var sut = new MessageViewModel(messageId, label, contentType, enqueuedTime, body, isDeadLetter);

        // Assert
        sut.MessageId.Should().Be(messageId);
        sut.Label.Should().Be(label);
        sut.ContentType.Should().Be(contentType);
        sut.EnqueuedTime.Should().Be(enqueuedTime);
        sut.Body.Should().Be(body);
        sut.IsDeadLetter.Should().Be(isDeadLetter);
        sut.IsSelected.Should().BeFalse();
    }

    [Test]
    public void Status_ShouldReturnCorrectValue_BasedOnIsDeadLetter()
    {
        // Arrange & Act
        var deadLetterMessage = new MessageViewModel("id", "label", "type", DateTimeOffset.Now, "body", true);
        var activeMessage = new MessageViewModel("id", "label", "type", DateTimeOffset.Now, "body", false);

        // Assert
        deadLetterMessage.Status.Should().Be("Dead Letter");
        activeMessage.Status.Should().Be("Active");
    }

    [Test]
    public void StatusColor_ShouldReturnCorrectValue_BasedOnIsDeadLetter()
    {
        // Arrange & Act
        var deadLetterMessage = new MessageViewModel("id", "label", "type", DateTimeOffset.Now, "body", true);
        var activeMessage = new MessageViewModel("id", "label", "type", DateTimeOffset.Now, "body", false);

        // Assert
        deadLetterMessage.StatusColor.Should().Be("Red");
        activeMessage.StatusColor.Should().Be("Green");
    }

    [Test]
    public void IsSelected_WhenChanged_ShouldRaisePropertyChangedEvent()
    {
        // Arrange
        var sut = new MessageViewModel("id", "label", "type", DateTimeOffset.Now, "body", false);
        var eventRaised = false;
        string propertyName = null;

        sut.PropertyChanged += (sender, e) =>
        {
            eventRaised = true;
            propertyName = e.PropertyName;
        };

        // Act
        sut.IsSelected = true;

        // Assert
        eventRaised.Should().BeTrue();
        propertyName.Should().Be(nameof(MessageViewModel.IsSelected));
        sut.IsSelected.Should().BeTrue();
    }

    [Test]
    public void IsSelected_WhenSetToSameValue_ShouldNotRaisePropertyChangedEvent()
    {
        // Arrange
        var sut = new MessageViewModel("id", "label", "type", DateTimeOffset.Now, "body", false);
        sut.IsSelected = true; // Set initial value

        var eventRaised = false;
        sut.PropertyChanged += (sender, e) => eventRaised = true;

        // Act
        sut.IsSelected = true; // Set same value

        // Assert
        eventRaised.Should().BeFalse();
    }

    [Test]
    public void PropertyChanged_ShouldBeRaisedWithCorrectSenderAndPropertyName()
    {
        // Arrange
        var sut = new MessageViewModel("id", "label", "type", DateTimeOffset.Now, "body", false);
        object eventSender = null;
        PropertyChangedEventArgs eventArgs = null;

        sut.PropertyChanged += (sender, e) =>
        {
            eventSender = sender;
            eventArgs = e;
        };

        // Act
        sut.IsSelected = true;

        // Assert
        eventSender.Should().Be(sut);
        eventArgs.Should().NotBeNull();
        eventArgs.PropertyName.Should().Be(nameof(MessageViewModel.IsSelected));
    }
}

