using Microsoft.Extensions.Caching.Distributed;
using MongoDB.Driver;
using Moq;
using PRIVATE.MESSAGING.Core.Entities;
using PRIVATE.MESSAGING.Core.Entities.Attributes;
using PRIVATE.MESSAGING.Core.Interfaces;
using PRIVATE.MESSAGING.Services;
using Xunit;

namespace PRIVATE.MESSAGING.Tests.Services;

public class UserServiceTests
{
    private static (UserService service, Mock<IUserRepository> repo) CreateService(
        List<User> users,
        List<ChatMessage>? messages = null)
    {
        var repo = new Mock<IUserRepository>();

        repo.Setup(r => r.GetByNicknameAsync(It.IsAny<string>()))
            .ReturnsAsync((string nick) => users.FirstOrDefault(u => u.Nickname == nick));

        repo.Setup(r => r.UpdateProfilePictureAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((string nick, string img) => users.Any(u => u.Nickname == nick));

        repo.Setup(r => r.SearchContactsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync((string myNick, string q, string? c, int limit) => 
            {
                var qUsers = users.Where(u => u.Nickname != myNick && u.Nickname.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
                return (qUsers, null, qUsers.Count);
            });

        repo.Setup(r => r.AddBlockedUserAsync(It.IsAny<string>(), It.IsAny<BlockedUserInfo>()))
            .ReturnsAsync(true);

        repo.Setup(r => r.RemoveBlockedUserAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        var cacheMock = new Mock<IDistributedCache>();
        return (new UserService(repo.Object, cacheMock.Object), repo);
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
            IdentityPublicKey = "alice-ipk",
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
        var user1 = new User { Nickname = "alice", BlockedUsers = new List<BlockedUserInfo>() };
        var user2 = new User { Nickname = "bob" };
        var (service, collection) = CreateService(new List<User> { user1, user2 });

        await service.BlockUserAsync("alice", "bob");

        collection.Verify(r => r.AddBlockedUserAsync("alice", It.Is<BlockedUserInfo>(b => b.Nickname == "bob")), Times.Once);
    }

    [Fact]
    public async Task UnblockUserAsync_CallsUpdateOne()
    {
        var user = new User
        {
            Nickname = "alice",
            BlockedUsers = new List<BlockedUserInfo> { new() { Nickname = "bob" } }
        };
        var (service, collection) = CreateService(new List<User> { user });

        await service.UnblockUserAsync("alice", "bob");

        collection.Verify(r => r.RemoveBlockedUserAsync("alice", "bob"), Times.Once);
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

    [Fact]
    public async Task GetInboxAsync_GroupsMessagesAndReturnsInbox()
    {
        var user = new User { Nickname = "alice" };
        var bob = new User { Nickname = "bob" };
        var messages = new List<ChatMessage>
        {
            new ChatMessage { Id = "1", SenderNickname = "alice", ReceiverNickname = "bob", Timestamp = DateTime.UtcNow.AddMinutes(-5), IsRead = true },
            new ChatMessage { Id = "2", SenderNickname = "bob", ReceiverNickname = "alice", Timestamp = DateTime.UtcNow, IsRead = false }
        };
        
        var (service, repo) = CreateService(new List<User> { user, bob });
        
        repo.Setup(r => r.GetInboxMessagesAsync("alice"))
            .ReturnsAsync(messages);
            
        repo.Setup(r => r.GetUsersByNicknamesAsync(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(new List<User> { bob });

        var result = await service.GetInboxAsync("alice");

        var list = result.ToList();
        Assert.Single(list);
        var json = System.Text.Json.JsonSerializer.Serialize(list[0]);
        Assert.Contains("bob", json);
        Assert.Contains("unreadCount", json);
        Assert.Contains("1", json); // 1 unread message
    }
}
