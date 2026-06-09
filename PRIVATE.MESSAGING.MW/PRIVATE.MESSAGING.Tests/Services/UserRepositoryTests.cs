using MongoDB.Driver;
using Moq;
using PRIVATE.MESSAGING.Core.Entities;
using PRIVATE.MESSAGING.Core.Entities.Attributes;
using PRIVATE.MESSAGING.Services.Repositories;
using PRIVATE.MESSAGING.Tests.Helpers;
using Xunit;

namespace PRIVATE.MESSAGING.Tests.Services.Repositories;

public class UserRepositoryTests
{
    private static (UserRepository repo, Mock<IMongoCollection<User>> collection) CreateRepository(List<User> users)
    {
        var mockCollection = MongoMockHelper.CreateCollectionMock(users);
        
        mockCollection.Setup(c => c.UpdateOneAsync(
            It.IsAny<FilterDefinition<User>>(),
            It.IsAny<UpdateDefinition<User>>(),
            It.IsAny<UpdateOptions>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpdateResult.Acknowledged(1, 1, null));

        var db = MongoMockHelper.CreateDatabaseMock("Users", mockCollection);
        return (new UserRepository(db.Object), mockCollection);
    }

    [Fact]
    public async Task GetByNicknameAsync_ReturnsUser()
    {
        var users = new List<User> { new User { Nickname = "alice" } };
        var (repo, _) = CreateRepository(users);

        var result = await repo.GetByNicknameAsync("alice");

        Assert.NotNull(result);
        Assert.Equal("alice", result.Nickname);
    }

    [Fact]
    public async Task GetUsersByNicknamesAsync_ReturnsUsers()
    {
        var users = new List<User> 
        { 
            new User { Nickname = "alice" }, 
            new User { Nickname = "bob" } 
        };
        var (repo, _) = CreateRepository(users);

        var result = await repo.GetUsersByNicknamesAsync(new[] { "alice", "bob" });

        var list = result.ToList();
        Assert.Equal(2, list.Count);
    }

    [Fact]
    public async Task UpdateProfilePictureAsync_CallsUpdateOne()
    {
        var users = new List<User>();
        var (repo, collection) = CreateRepository(users);

        await repo.UpdateProfilePictureAsync("alice", "new_pic.png");

        collection.Verify(c => c.UpdateOneAsync(
            It.IsAny<FilterDefinition<User>>(),
            It.IsAny<UpdateDefinition<User>>(),
            null,
            default), Times.Once);
    }

    [Fact]
    public async Task SearchContactsAsync_ReturnsMatches()
    {
        var users = new List<User> 
        { 
            new User { Nickname = "alice" }, 
            new User { Nickname = "alison" } 
        };
        var (repo, _) = CreateRepository(users);

        var result = await repo.SearchContactsAsync("alice", "ali", null, 10);

        Assert.Equal(2, result.Users.Count);
    }

    [Fact]
    public async Task AddBlockedUserAsync_CallsUpdateOne()
    {
        var (repo, collection) = CreateRepository(new List<User>());

        await repo.AddBlockedUserAsync("alice", new BlockedUserInfo { Nickname = "bob" });

        collection.Verify(c => c.UpdateOneAsync(
            It.IsAny<FilterDefinition<User>>(),
            It.IsAny<UpdateDefinition<User>>(),
            null,
            default), Times.Once);
    }

    [Fact]
    public async Task RemoveBlockedUserAsync_CallsUpdateOne()
    {
        var (repo, collection) = CreateRepository(new List<User>());

        await repo.RemoveBlockedUserAsync("alice", "bob");

        collection.Verify(c => c.UpdateOneAsync(
            It.IsAny<FilterDefinition<User>>(),
            It.IsAny<UpdateDefinition<User>>(),
            null,
            default), Times.Once);
    }

    [Fact]
    public async Task GetInboxMessagesAsync_ReturnsMessages()
    {
        var messages = new List<ChatMessage>
        {
            new ChatMessage { ReceiverNickname = "alice" }
        };
        var msgCollection = MongoMockHelper.CreateCollectionMock(messages);
        
        var db = new Mock<IMongoDatabase>();
        var userCollection = new Mock<IMongoCollection<User>>();
        db.Setup(d => d.GetCollection<User>("Users", It.IsAny<MongoCollectionSettings>())).Returns(userCollection.Object);
        db.Setup(d => d.GetCollection<ChatMessage>("ChatMessages", It.IsAny<MongoCollectionSettings>())).Returns(msgCollection.Object);
        
        var repo = new UserRepository(db.Object);

        var result = await repo.GetInboxMessagesAsync("alice");

        Assert.Single(result);
    }
}
