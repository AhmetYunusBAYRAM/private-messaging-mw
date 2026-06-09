using Microsoft.Extensions.Caching.Distributed;
using MongoDB.Driver;
using Moq;
using PRIVATE.MESSAGING.Core.Entities;
using PRIVATE.MESSAGING.Core.Entities.Attributes;
using PRIVATE.MESSAGING.Services;
using PRIVATE.MESSAGING.Tests.Helpers;
using Xunit;

namespace PRIVATE.MESSAGING.Tests.Services;

public class UserServiceTests
{
    private static (UserService service, Mock<IMongoCollection<User>> userCollection) CreateService(
        List<User> users,
        List<Core.Entities.ChatMessage>? messages = null)
    {
        messages ??= new List<Core.Entities.ChatMessage>();

        var userCollection = MongoMockHelper.CreateCollectionMock(users);
        var msgCollection = MongoMockHelper.CreateCollectionMock(messages);

        userCollection.Setup(c => c.UpdateOneAsync(
            It.IsAny<FilterDefinition<User>>(),
            It.IsAny<UpdateDefinition<User>>(),
            It.IsAny<UpdateOptions>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpdateResult.Acknowledged(1, 1, null));

        var db = new Mock<IMongoDatabase>();
        db.Setup(d => d.GetCollection<User>("Users", It.IsAny<MongoCollectionSettings>()))
          .Returns(userCollection.Object);
        db.Setup(d => d.GetCollection<Core.Entities.ChatMessage>("ChatMessages", It.IsAny<MongoCollectionSettings>()))
          .Returns(msgCollection.Object);

        var cacheMock = new Mock<IDistributedCache>();

        return (new UserService(db.Object, cacheMock.Object), userCollection);
    }

    [Fact]
    public async Task GetProfileAsync_WhenUserNotFound_ReturnsNull()
    {
        var (service, _) = CreateService(new List<User>());

        var result = await service.GetProfileAsync("ghost", "caller");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetProfileAsync_WhenCallerIsBlocked_ReturnsRedactedProfile()
    {
        var targetUser = new User
        {
            Nickname = "alice",
            PublicKey = "alice-pk",
            BlockedUsers = new List<BlockedUserInfo>
            {
                new() { Nickname = "bob", BlockedAt = DateTime.UtcNow }
            }
        };
        var (service, _) = CreateService(new List<User> { targetUser });

        var result = await service.GetProfileAsync("alice", "bob");

        Assert.NotNull(result);
        var json = System.Text.Json.JsonSerializer.Serialize(result);
        Assert.Contains("isOnline", json);
        Assert.Contains("false", json);
    }

    [Fact]
    public async Task UpdateProfilePictureAsync_WhenUserMatched_ReturnsTrue()
    {
        var user = new User { Nickname = "alice" };
        var (service, _) = CreateService(new List<User> { user });

        var result = await service.UpdateProfilePictureAsync("alice", "data:image/png;base64,abc123");

        Assert.True(result);
    }

    [Fact]
    public async Task GetContactsAsync_WithQuery_ReturnsFilteredResults()
    {
        var users = new List<User>
        {
            new() { Nickname = "alice123" },
            new() { Nickname = "bob456" },
            new() { Nickname = "alicex" }
        };
        var (service, _) = CreateService(users);

        var result = await service.GetContactsAsync("requester", "alice", null, 50);

        var list = result.Items.ToList();
        Assert.All(list, u =>
        {
            var json = System.Text.Json.JsonSerializer.Serialize(u);
            Assert.DoesNotContain("requester", json);
        });
    }

    [Fact]
    public async Task BlockUserAsync_CallsUpdateOne()
    {
        var user = new User { Nickname = "alice", BlockedUsers = new List<BlockedUserInfo>() };
        var (service, collection) = CreateService(new List<User> { user });

        await service.BlockUserAsync("alice", "bob");

        collection.Verify(c => c.UpdateOneAsync(
            It.IsAny<FilterDefinition<User>>(),
            It.IsAny<UpdateDefinition<User>>(),
            It.IsAny<UpdateOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UnblockUserAsync_CallsUpdateOne()
    {
        var user = new User
        {
            Nickname = "alice",
            BlockedUsers = new List<BlockedUserInfo>
            {
                new() { Nickname = "bob", BlockedAt = DateTime.UtcNow }
            }
        };
        var (service, collection) = CreateService(new List<User> { user });

        await service.UnblockUserAsync("alice", "bob");

        collection.Verify(c => c.UpdateOneAsync(
            It.IsAny<FilterDefinition<User>>(),
            It.IsAny<UpdateDefinition<User>>(),
            It.IsAny<UpdateOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetBlockedUsersAsync_WhenNoBlockedUsers_ReturnsEmpty()
    {
        var user = new User { Nickname = "alice", BlockedUsers = new List<BlockedUserInfo>() };
        var (service, _) = CreateService(new List<User> { user });

        var result = await service.GetBlockedUsersAsync("alice");

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetBlockedUsersAsync_WhenUserNotFound_ReturnsEmpty()
    {
        var (service, _) = CreateService(new List<User>());

        var result = await service.GetBlockedUsersAsync("ghost");

        Assert.Empty(result);
    }
}
