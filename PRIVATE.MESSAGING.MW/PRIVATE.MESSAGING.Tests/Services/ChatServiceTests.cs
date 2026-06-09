using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Moq;
using PRIVATE.MESSAGING.Core.Entities;
using PRIVATE.MESSAGING.Core.Entities.Attributes;
using PRIVATE.MESSAGING.Services;
using PRIVATE.MESSAGING.Tests.Helpers;
using Xunit;

namespace PRIVATE.MESSAGING.Tests.Services;

public class ChatServiceTests
{
    [Fact]
    public void UserConnected_StoresConnectionId()
    {
        var service = CreateChatService(new List<User>(), new List<Core.Entities.ChatMessage>());

        service.UserConnected("alice", "dev-1", "conn-123", "ephemeral-key");

        var details = service.GetActiveConnections("alice");
        Assert.True(details.ContainsKey("dev-1"));
        Assert.Equal("conn-123", details["dev-1"].ConnectionId);
        Assert.Equal("ephemeral-key", details["dev-1"].EphemeralPublicKey);
    }

    [Fact]
    public void UserDisconnected_RemovesConnectionId()
    {
        var service = CreateChatService(new List<User>(), new List<Core.Entities.ChatMessage>());
        service.UserConnected("alice", "dev-1", "conn-123", "ephemeral-key");

        var removed = service.UserDisconnected("alice", "dev-1");

        Assert.True(removed);
        Assert.False(service.GetActiveConnections("alice").ContainsKey("dev-1"));
    }

    [Fact]
    public void UserDisconnected_WhenNotConnected_ReturnsFalse()
    {
        var service = CreateChatService(new List<User>(), new List<Core.Entities.ChatMessage>());

        var removed = service.UserDisconnected("ghost", "dev-1");

        Assert.False(removed);
    }

    [Fact]
    public void GetActiveConnections_WhenNotConnected_ReturnsEmpty()
    {
        var service = CreateChatService(new List<User>(), new List<Core.Entities.ChatMessage>());

        var result = service.GetActiveConnections("nobody");

        Assert.Empty(result);
    }

    [Fact]
    public async Task SendPrivateMessageAsync_WhenSenderBlockedReceiver_ThrowsException()
    {
        var sender = new User
        {
            Nickname = "alice",
            BlockedUsers = new List<BlockedUserInfo>
            {
                new() { Nickname = "bob", BlockedAt = DateTime.UtcNow }
            }
        };
        var receiver = new User { Nickname = "bob", BlockedUsers = new List<BlockedUserInfo>() };

        var service = CreateChatService(new List<User> { sender, receiver }, new List<Core.Entities.ChatMessage>());

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.SendPrivateMessageAsync("alice", "bob", "sk", new Dictionary<string, string>(), "sig", "payload", null));
    }

    [Fact]
    public async Task SendPrivateMessageAsync_WhenReceiverBlockedSender_ThrowsException()
    {
        var sender = new User { Nickname = "alice", BlockedUsers = new List<BlockedUserInfo>() };
        var receiver = new User
        {
            Nickname = "bob",
            BlockedUsers = new List<BlockedUserInfo>
            {
                new() { Nickname = "alice", BlockedAt = DateTime.UtcNow }
            }
        };

        var service = CreateChatService(new List<User> { sender, receiver }, new List<Core.Entities.ChatMessage>());

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.SendPrivateMessageAsync("alice", "bob", "sk", new Dictionary<string, string>(), "sig", "payload", null));
    }

    [Fact]
    public async Task SendPrivateMessageAsync_WhenReceiverNotFound_ThrowsException()
    {
        var sender = new User { Nickname = "alice", BlockedUsers = new List<BlockedUserInfo>() };

        var service = CreateChatService(new List<User> { sender }, new List<Core.Entities.ChatMessage>());

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.SendPrivateMessageAsync("alice", "ghost", "sk", new Dictionary<string, string>(), "sig", "payload", null));
    }

    [Fact]
    public async Task DeleteMessageAsync_WhenNotOwner_ThrowsException()
    {
        var message = new Core.Entities.ChatMessage
        {
            Id = "msg1",
            SenderNickname = "alice",
            ReceiverNickname = "bob",
            EncryptedPayload = "encrypted"
        };

        var service = CreateChatService(new List<User>(), new List<Core.Entities.ChatMessage> { message });

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.DeleteMessageAsync("bob", "msg1"));
    }

    [Fact]
    public async Task DeleteMessageAsync_WhenMessageNotFound_ThrowsException()
    {
        var service = CreateChatService(new List<User>(), new List<Core.Entities.ChatMessage>());

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.DeleteMessageAsync("alice", "nonexistent"));
    }

    private static ChatService CreateChatService(List<User> users, List<Core.Entities.ChatMessage> messages)
    {
        var userCollection = MongoMockHelper.CreateCollectionMock(users);
        var msgCollection = MongoMockHelper.CreateCollectionMock(messages);

        msgCollection.Setup(c => c.InsertOneAsync(
            It.IsAny<Core.Entities.ChatMessage>(),
            It.IsAny<InsertOneOptions>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        msgCollection.Setup(c => c.UpdateOneAsync(
            It.IsAny<FilterDefinition<Core.Entities.ChatMessage>>(),
            It.IsAny<UpdateDefinition<Core.Entities.ChatMessage>>(),
            It.IsAny<UpdateOptions>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpdateResult.Acknowledged(1, 1, null));

        var db = new Mock<IMongoDatabase>();
        db.Setup(d => d.GetCollection<User>("Users", It.IsAny<MongoCollectionSettings>()))
          .Returns(userCollection.Object);
        db.Setup(d => d.GetCollection<Core.Entities.ChatMessage>("ChatMessages", It.IsAny<MongoCollectionSettings>()))
          .Returns(msgCollection.Object);

        var options = Options.Create(new MemoryDistributedCacheOptions());
        var cache = new MemoryDistributedCache(options);

        return new ChatService(db.Object, cache);
    }
}
