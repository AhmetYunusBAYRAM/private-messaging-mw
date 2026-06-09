using MongoDB.Driver;
using Moq;
using PRIVATE.MESSAGING.Core.Entities;
using PRIVATE.MESSAGING.Services;
using PRIVATE.MESSAGING.Tests.Helpers;
using Xunit;

namespace PRIVATE.MESSAGING.Tests.Services;

public class MessageServiceTests
{
    private static (MessageService service, Mock<IMongoCollection<ChatMessage>> collection) CreateService(List<ChatMessage> messages)
    {
        var msgCollection = MongoMockHelper.CreateCollectionMock(messages);
        
        msgCollection.Setup(c => c.UpdateManyAsync(
            It.IsAny<FilterDefinition<ChatMessage>>(),
            It.IsAny<UpdateDefinition<ChatMessage>>(),
            It.IsAny<UpdateOptions>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpdateResult.Acknowledged(1, 1, null));

        var db = new Mock<IMongoDatabase>();
        db.Setup(d => d.GetCollection<ChatMessage>("ChatMessages", It.IsAny<MongoCollectionSettings>()))
          .Returns(msgCollection.Object);

        return (new MessageService(db.Object), msgCollection);
    }

    [Fact]
    public async Task GetHistoryAsync_ReturnsMessagesBetweenUsers()
    {
        var messages = new List<ChatMessage>
        {
            new ChatMessage { SenderNickname = "alice", ReceiverNickname = "bob", SenderEncryptedPayload = "m1" },
            new ChatMessage { SenderNickname = "bob", ReceiverNickname = "alice", SenderEncryptedPayload = "m2" }
        };
        var (service, collection) = CreateService(messages);

        var result = await service.GetHistoryAsync("alice", "bob", null, 50);

        var list = result.Items.ToList();
        Assert.Equal(2, list.Count);
        collection.Verify(c => c.FindAsync(
            It.IsAny<FilterDefinition<ChatMessage>>(),
            It.IsAny<FindOptions<ChatMessage, ChatMessage>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ClearHistoryAsync_CallsUpdateMany()
    {
        var (service, collection) = CreateService(new List<ChatMessage>());

        await service.ClearHistoryAsync("alice", "bob");

        collection.Verify(c => c.UpdateManyAsync(
            It.IsAny<FilterDefinition<ChatMessage>>(),
            It.IsAny<UpdateDefinition<ChatMessage>>(),
            It.IsAny<UpdateOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
