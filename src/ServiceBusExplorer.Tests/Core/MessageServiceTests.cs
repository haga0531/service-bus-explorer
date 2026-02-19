#nullable disable
using FluentAssertions;
using Moq;
using ServiceBusExplorer.Core;
using ServiceBusExplorer.Infrastructure;
using ServiceBusExplorer.Infrastructure.Models;

namespace ServiceBusExplorer.Tests.Core;

[TestFixture]
public class MessageServiceTests
{
    private Mock<IMessagePeekProvider> _mockPeekProvider;
    private Mock<IMessageDeleteProvider> _mockDeleteProvider;
    private Mock<IMessagePurgeProvider> _mockPurgeProvider;
    private Mock<IMessageResubmitProvider> _mockResubmitProvider;
    private MessageService _sut;
    private const string ConnectionString = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=test;SharedAccessKey=test";
    private ServiceBusAuthContext _authContext;

    [SetUp]
    public void SetUp()
    {
        _mockPeekProvider = new Mock<IMessagePeekProvider>();
        _mockDeleteProvider = new Mock<IMessageDeleteProvider>();
        _mockPurgeProvider = new Mock<IMessagePurgeProvider>();
        _mockResubmitProvider = new Mock<IMessageResubmitProvider>();

        _authContext = new ConnectionStringAuthContext(ConnectionString);

        _sut = new MessageService(
            _ => _mockPeekProvider.Object,
            _ => _mockDeleteProvider.Object,
            _ => _mockPurgeProvider.Object,
            _ => _mockResubmitProvider.Object);
    }

    [Test]
    public async Task GetPagedMessagesAsync_ShouldReturnActiveMessagesOnly_WhenActiveOnlyIsTrue()
    {
        // Arrange
        var messages = new List<ServiceBusReceivedMessageDto>
        {
            new("msg1", "label1", "application/json", DateTimeOffset.Now, "body1", false),
            new("msg2", "label2", "application/json", DateTimeOffset.Now, "body2", false)
        };
        
        var pagedResult = new PagedResult<ServiceBusReceivedMessageDto>
        {
            Items = messages,
            TotalCount = 2,
            PageNumber = 1,
            PageSize = 50
        };
        
        _mockPeekProvider.Setup(x => x.PeekPagedAsync("queue1", null, 1, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        // Act
        var result = await _sut.GetPagedMessagesAsync(
            _authContext, "queue1", null, 1, 50, activeOnly: true);

        // Assert
        result.Should().Be(pagedResult);
        _mockPeekProvider.Verify(x => x.PeekPagedAsync("queue1", null, 1, 50, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task GetPagedMessagesAsync_ShouldReturnDeadLetterMessagesOnly_WhenDeadLetterOnlyIsTrue()
    {
        // Arrange
        var messages = new List<ServiceBusReceivedMessageDto>
        {
            new("msg1", "label1", "application/json", DateTimeOffset.Now, "body1", true),
            new("msg2", "label2", "application/json", DateTimeOffset.Now, "body2", true)
        };
        
        var pagedResult = new PagedResult<ServiceBusReceivedMessageDto>
        {
            Items = messages,
            TotalCount = 2,
            PageNumber = 1,
            PageSize = 50
        };
        
        _mockPeekProvider.Setup(x => x.PeekDeadLetterPagedAsync("queue1", null, 1, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        // Act
        var result = await _sut.GetPagedMessagesAsync(
            _authContext, "queue1", null, 1, 50, deadLetterOnly: true);

        // Assert
        result.Should().Be(pagedResult);
        _mockPeekProvider.Verify(x => x.PeekDeadLetterPagedAsync("queue1", null, 1, 50, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task GetPagedMessagesAsync_ShouldReturnAllMessages_WhenBothFlagsAreFalse()
    {
        // Arrange
        var activeMessages = new List<ServiceBusReceivedMessageDto>
        {
            new("msg1", "label1", "application/json", DateTimeOffset.Now, "body1", false)
        };
        
        var deadLetterMessages = new List<ServiceBusReceivedMessageDto>
        {
            new("msg2", "label2", "application/json", DateTimeOffset.Now, "body2", true)
        };
        
        var activePage = new PagedResult<ServiceBusReceivedMessageDto>
        {
            Items = activeMessages,
            TotalCount = 10,
            PageNumber = 1,
            PageSize = 50
        };
        
        var deadLetterPage = new PagedResult<ServiceBusReceivedMessageDto>
        {
            Items = deadLetterMessages,
            TotalCount = 5,
            PageNumber = 1,
            PageSize = 50
        };
        
        _mockPeekProvider.Setup(x => x.GetMessageCountsAsync("queue1", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((activeCount: 10, deadLetterCount: 5));
        _mockPeekProvider.Setup(x => x.PeekPagedAsync("queue1", null, 1, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(activePage);
        _mockPeekProvider.Setup(x => x.PeekDeadLetterPagedAsync("queue1", null, 1, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(deadLetterPage);

        // Act
        var result = await _sut.GetPagedMessagesAsync(
            _authContext, "queue1", null, 1, 50, activeOnly: false, deadLetterOnly: false);

        // Assert
        result.Items.Should().HaveCount(2);
        result.TotalCount.Should().Be(15);
        result.Items.Should().Contain(m => m.MessageId == "msg1");
        result.Items.Should().Contain(m => m.MessageId == "msg2");
    }

    [Test]
    public async Task GetMessageCountsAsync_ShouldReturnCorrectCounts()
    {
        // Arrange
        _mockPeekProvider.Setup(x => x.GetMessageCountsAsync("queue1", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((100, 10));

        // Act
        var result = await _sut.GetMessageCountsAsync(_authContext, "queue1", null);

        // Assert
        result.activeCount.Should().Be(100);
        result.deadLetterCount.Should().Be(10);
    }

    [Test]
    public async Task DeleteActiveMessageAsync_ShouldCallDeleteProvider()
    {
        // Arrange
        const string messageId = "msg123";

        // Act
        await _sut.DeleteActiveMessageAsync(_authContext, "queue1", null, messageId);

        // Assert
        _mockDeleteProvider.Verify(x => x.DeleteActiveMessageAsync("queue1", null, messageId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task DeleteDeadLetterMessageAsync_ShouldCallDeleteProvider()
    {
        // Arrange
        const string messageId = "msg123";

        // Act
        await _sut.DeleteDeadLetterMessageAsync(_authContext, "queue1", null, messageId);

        // Assert
        _mockDeleteProvider.Verify(x => x.DeleteDeadLetterMessageAsync("queue1", null, messageId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task PurgeMessagesAsync_ShouldCallPurgeProviderBasedOnOption()
    {
        // Act & Assert - All messages
        await _sut.PurgeMessagesAsync(_authContext, "queue1", null, PurgeOption.All);
        _mockPurgeProvider.Verify(x => x.PurgeActiveMessagesAsync("queue1", null, It.IsAny<CancellationToken>()), Times.Once);
        _mockPurgeProvider.Verify(x => x.PurgeDeadLetterMessagesAsync("queue1", null, It.IsAny<CancellationToken>()), Times.Once);
        
        _mockPurgeProvider.Reset();
        
        // Act & Assert - Active only
        await _sut.PurgeMessagesAsync(_authContext, "queue1", null, PurgeOption.ActiveOnly);
        _mockPurgeProvider.Verify(x => x.PurgeActiveMessagesAsync("queue1", null, It.IsAny<CancellationToken>()), Times.Once);
        _mockPurgeProvider.Verify(x => x.PurgeDeadLetterMessagesAsync("queue1", null, It.IsAny<CancellationToken>()), Times.Never);
        
        _mockPurgeProvider.Reset();
        
        // Act & Assert - Dead letter only
        await _sut.PurgeMessagesAsync(_authContext, "queue1", null, PurgeOption.DeadLetterOnly);
        _mockPurgeProvider.Verify(x => x.PurgeActiveMessagesAsync("queue1", null, It.IsAny<CancellationToken>()), Times.Never);
        _mockPurgeProvider.Verify(x => x.PurgeDeadLetterMessagesAsync("queue1", null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task ResubmitDeadLetterMessageAsync_ShouldCallResubmitProvider()
    {
        // Arrange
        const string messageId = "msg123";
        const string queueName = "queue1";

        // Act
        await _sut.ResubmitDeadLetterMessageAsync(_authContext, queueName, messageId);

        // Assert
        _mockResubmitProvider.Verify(x => x.ResubmitMessageAsync(queueName, messageId, null, true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task ResubmitDeadLetterMessageAsync_WithSubscription_ShouldPassSubscriptionToProvider()
    {
        // Arrange
        const string messageId = "msg123";
        const string topicName = "topic1";
        const string subscriptionName = "sub1";

        // Act
        await _sut.ResubmitDeadLetterMessageAsync(_authContext, topicName, messageId, subscriptionName);

        // Assert
        _mockResubmitProvider.Verify(x => x.ResubmitMessageAsync(topicName, messageId, subscriptionName, true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task ResubmitDeadLetterMessageAsync_WhenNotDeleting_ShouldPassFlagToProvider()
    {
        // Arrange
        const string messageId = "msg123";
        const string queueName = "queue1";

        // Act
        await _sut.ResubmitDeadLetterMessageAsync(_authContext, queueName, messageId, deleteFromDeadLetter: false);

        // Assert
        _mockResubmitProvider.Verify(x => x.ResubmitMessageAsync(queueName, messageId, null, false, It.IsAny<CancellationToken>()), Times.Once);
    }
}
