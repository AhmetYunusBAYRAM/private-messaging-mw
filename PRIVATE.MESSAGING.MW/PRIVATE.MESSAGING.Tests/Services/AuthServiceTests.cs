using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using Moq;
using PRIVATE.MESSAGING.Core.Entities.Attributes;
using PRIVATE.MESSAGING.Core.Interfaces;
using PRIVATE.MESSAGING.Services;
using PRIVATE.MESSAGING.Tests.Helpers;
using Xunit;

namespace PRIVATE.MESSAGING.Tests.Services;

public class AuthServiceTests
{
    private readonly Mock<IEmailService> _emailServiceMock = new();
    private readonly IConfiguration _config;

    public AuthServiceTests()
    {
        _config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["Jwt:Key"] = "super-secret-test-key-32-chars-ok!!",
                ["Jwt:Issuer"] = "TestIssuer",
                ["Jwt:Audience"] = "TestAudience"
            })
            .Build();
    }

    private (AuthService service, Mock<IMongoCollection<User>> collection) CreateService(List<User> users)
    {
        var collection = MongoMockHelper.CreateCollectionMock(users);
        var db = new Mock<IMongoDatabase>();
        db.Setup(d => d.GetCollection<User>("Users", It.IsAny<MongoCollectionSettings>()))
          .Returns(collection.Object);

        var service = new AuthService(db.Object, _emailServiceMock.Object, _config);
        return (service, collection);
    }

    [Fact]
    public async Task RegisterAsync_WhenEmailAlreadyExists_ReturnsFailure()
    {
        var existingUser = new User { Email = "test@test.com", Nickname = "test" };
        var (service, _) = CreateService(new List<User> { existingUser });

        var result = await service.RegisterAsync("test@test.com", "newuser", "pk", "epk");

        Assert.False(result.Success);
        Assert.Contains("already registered", result.Message);
    }

    [Fact]
    public async Task RegisterAsync_WhenNicknameAlreadyExists_ReturnsFailure()
    {
        var existingUser = new User { Email = "other@test.com", Nickname = "takenNick" };
        var (service, _) = CreateService(new List<User> { existingUser });

        var result = await service.RegisterAsync("new@test.com", "takenNick", "pk", "epk");

        Assert.False(result.Success);
        Assert.Contains("already registered", result.Message);
    }

    [Fact]
    public async Task RegisterAsync_WhenValid_ReturnsSuccess()
    {
        var (service, collection) = CreateService(new List<User>());
        collection.Setup(c => c.InsertOneAsync(
            It.IsAny<User>(),
            It.IsAny<InsertOneOptions>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await service.RegisterAsync("new@test.com", "newuser", "pk", "epk");

        Assert.True(result.Success);
    }

    [Fact]
    public async Task LoginAsync_WhenUserNotFound_ReturnsFailure()
    {
        var (service, _) = CreateService(new List<User>());

        var result = await service.LoginAsync("notfound@test.com");

        Assert.False(result.Success);
        Assert.Equal("User not found", result.Message);
    }

    [Fact]
    public async Task VerifyOtpAsync_WhenUserNotFound_ReturnsFailure()
    {
        var (service, _) = CreateService(new List<User>());

        var result = await service.VerifyOtpAsync("notfound@test.com", "123456");

        Assert.False(result.Success);
        Assert.Equal("User not found", result.Message);
    }

    [Fact]
    public async Task VerifyOtpAsync_WhenOtpExpired_ReturnsFailure()
    {
        var user = new User
        {
            Email = "test@test.com",
            Nickname = "testuser",
            Otp = "123456",
            OtpExpiry = DateTime.UtcNow.AddMinutes(-10)
        };
        var (service, _) = CreateService(new List<User> { user });

        var result = await service.VerifyOtpAsync("test@test.com", "123456");

        Assert.False(result.Success);
        Assert.Contains("expired", result.Message);
    }

    [Fact]
    public async Task VerifyOtpAsync_WhenOtpWrong_ReturnsFailure()
    {
        var user = new User
        {
            Email = "test@test.com",
            Nickname = "testuser",
            Otp = "999999",
            OtpExpiry = DateTime.UtcNow.AddMinutes(5)
        };
        var (service, _) = CreateService(new List<User> { user });

        var result = await service.VerifyOtpAsync("test@test.com", "123456");

        Assert.False(result.Success);
        Assert.Contains("Invalid", result.Message);
    }

    [Fact]
    public async Task VerifyOtpAsync_WhenOtpValid_ReturnsTokenAndNickname()
    {
        var user = new User
        {
            Email = "test@test.com",
            Nickname = "testuser",
            Otp = "123456",
            OtpExpiry = DateTime.UtcNow.AddMinutes(5),
            EncryptedPrivateKey = "encrypted_key"
        };
        var (service, collection) = CreateService(new List<User> { user });
        collection.Setup(c => c.UpdateOneAsync(
            It.IsAny<FilterDefinition<User>>(),
            It.IsAny<UpdateDefinition<User>>(),
            It.IsAny<UpdateOptions>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpdateResult.Acknowledged(1, 1, null));

        var result = await service.VerifyOtpAsync("test@test.com", "123456");

        Assert.True(result.Success);
        Assert.Equal("testuser", result.Nickname);
        Assert.NotEmpty(result.Token);
        Assert.Equal("encrypted_key", result.EncryptedPrivateKey);
    }

    [Fact]
    public async Task GetPublicKeyAsync_WhenUserExists_ReturnsKey()
    {
        var user = new User { Nickname = "alice", PublicKey = "alice-public-key" };
        var (service, _) = CreateService(new List<User> { user });

        var result = await service.GetPublicKeyAsync("alice");

        Assert.Equal("alice-public-key", result);
    }

    [Fact]
    public async Task GetPublicKeyAsync_WhenUserNotFound_ReturnsNull()
    {
        var (service, _) = CreateService(new List<User>());

        var result = await service.GetPublicKeyAsync("ghost");

        Assert.Null(result);
    }
}
