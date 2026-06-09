using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using Moq;
using PRIVATE.MESSAGING.Core.Entities;
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
            .AddInMemoryCollection(new Dictionary<string, string?>
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
    public async Task RegisterAsync_WhenValid_ReturnsSuccess()
    {
        var (service, collection) = CreateService(new List<User>());
        collection.Setup(c => c.InsertOneAsync(
            It.IsAny<User>(),
            It.IsAny<InsertOneOptions>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var bundle = new PRIVATE.MESSAGING.DTOs.Requests.KeyBundleDto
        {
            IdentityPublicKey = "ipk",
            EncryptedIdentityPrivateKey = "eipk",
            SignedPreKeyPublic = "spk",
            SignedPreKeySignature = "sig",
            EncryptedSignedPrePrivateKey = "esppk",
            OneTimePreKeys = new List<PRIVATE.MESSAGING.DTOs.Requests.PreKeyDto>()
        };

        var result = await service.RegisterAsync("new@test.com", "newuser", bundle);

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
    public async Task VerifyOtpAsync_WhenOtpValid_ReturnsData()
    {
        var user = new User
        {
            Email = "test@test.com",
            Nickname = "testuser",
            Otp = "123456",
            OtpExpiry = DateTime.UtcNow.AddMinutes(5),
            EncryptedIdentityPrivateKey = "eipk",
            EncryptedSignedPrePrivateKey = "esppk"
        };
        var (service, collection) = CreateService(new List<User> { user });
        collection.Setup(c => c.UpdateOneAsync(
            It.IsAny<FilterDefinition<User>>(),
            It.IsAny<UpdateDefinition<User>>(),
            It.IsAny<UpdateOptions>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpdateResult.Acknowledged(1, 1, null));

        var result = await service.VerifyOtpAsync("test@test.com", "123456", "127.0.0.1", "TestDevice", "devId");

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("testuser", result.Data!.Nickname);
        Assert.NotEmpty(result.Data.Token);
        Assert.Equal("eipk", result.Data.EncryptedIdentityPrivateKey);
    }

    [Fact]
    public async Task GetPublicKeyBundleAsync_WhenUserExists_ReturnsBundle()
    {
        var user = new User 
        { 
            Nickname = "alice", 
            IdentityPublicKey = "ipk",
            SignedPreKeyPublic = "spk",
            SignedPreKeySignature = "sig",
            OneTimePreKeys = new List<PreKeyInfo> { new PreKeyInfo { KeyId = "1", PublicKey = "pk1" } }
        };
        var (service, collection) = CreateService(new List<User> { user });

        collection.Setup(c => c.UpdateOneAsync(
            It.IsAny<FilterDefinition<User>>(),
            It.IsAny<UpdateDefinition<User>>(),
            It.IsAny<UpdateOptions>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpdateResult.Acknowledged(1, 1, null));

        var result = await service.GetPublicKeyBundleAsync("alice");

        Assert.NotNull(result);
        Assert.Equal("ipk", result.IdentityPublicKey);
    }

    [Fact]
    public async Task RefreshTokenAsync_WhenValid_ReturnsNewToken()
    {
        var rt = new RefreshTokenInfo
        {
            Token = "valid-refresh-token",
            Expiry = DateTime.UtcNow.AddDays(7)
        };
        var user = new User
        {
            Nickname = "alice",
            RefreshTokens = new List<RefreshTokenInfo> { rt }
        };
        var (service, collection) = CreateService(new List<User> { user });

        collection.Setup(c => c.UpdateOneAsync(
            It.IsAny<FilterDefinition<User>>(),
            It.IsAny<UpdateDefinition<User>>(),
            It.IsAny<UpdateOptions>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpdateResult.Acknowledged(1, 1, null));

        // Generate a valid JWT token
        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var key = System.Text.Encoding.ASCII.GetBytes("super-secret-test-key-32-chars-ok!!");
        var descriptor = new Microsoft.IdentityModel.Tokens.SecurityTokenDescriptor
        {
            Subject = new System.Security.Claims.ClaimsIdentity(new[]
            {
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, "alice"),
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Email, "alice@test.com")
            }),
            Expires = DateTime.UtcNow.AddMinutes(5),
            SigningCredentials = new Microsoft.IdentityModel.Tokens.SigningCredentials(
                new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(key),
                Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256Signature),
            Issuer = "TestIssuer",
            Audience = "TestAudience"
        };
        var token = handler.CreateToken(descriptor);
        var tokenString = handler.WriteToken(token);

        var result = await service.RefreshTokenAsync(tokenString, "valid-refresh-token");

        Assert.True(result.Success);
        Assert.NotEmpty(result.Token);
        Assert.NotEmpty(result.RefreshToken);
    }

    [Fact]
    public async Task RefreshTokenAsync_WhenInvalid_ReturnsNull()
    {
        var (service, _) = CreateService(new List<User>());
        var result = await service.RefreshTokenAsync("invalid-jwt", "invalid-token");
        Assert.False(result.Success);
    }

    [Fact]
    public async Task ResetKeysAsync_WhenUserExists_ReturnsTrue()
    {
        var user = new User { Nickname = "alice" };
        var (service, collection) = CreateService(new List<User> { user });

        collection.Setup(c => c.UpdateOneAsync(
            It.IsAny<FilterDefinition<User>>(),
            It.IsAny<UpdateDefinition<User>>(),
            It.IsAny<UpdateOptions>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpdateResult.Acknowledged(1, 1, null));

        var req = new DTOs.Requests.ResetKeysRequest
        {
            Keys = new DTOs.Requests.KeyBundleDto
            {
                IdentityPublicKey = "new-ipk",
                EncryptedIdentityPrivateKey = "new-eipk",
                SignedPreKeyPublic = "new-spk",
                EncryptedSignedPrePrivateKey = "new-esppk",
                SignedPreKeySignature = "new-sig",
                OneTimePreKeys = new List<DTOs.Requests.PreKeyDto>()
            }
        };

        var result = await service.ResetKeysAsync("alice", req.Keys);
        Assert.True(result.Success);
    }
}
