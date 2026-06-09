using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace PRIVATE.MESSAGING.MW.Models.Attributes;

public class User
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    public string Email { get; set; } = string.Empty;
    public string Nickname { get; set; } = string.Empty;
    public string PublicKey { get; set; } = string.Empty;
    public string EncryptedPrivateKey { get; set; } = string.Empty;
    public string ProfilePictureBase64 { get; set; } = string.Empty;
    
    public string? Otp { get; set; }
    public DateTime? OtpExpiry { get; set; }

    public List<BlockedUserInfo> BlockedUsers { get; set; } = new();

    public DateTime LastSeen { get; set; }
    public bool IsOnline { get; set; } = false;

    public List<DeviceLog> DeviceLogs { get; set; } = new();
}

public class DeviceLog
{
    public string IpAddress { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public DateTime LastActiveAt { get; set; }
}

public class BlockedUserInfo
{
    public string Nickname { get; set; } = string.Empty;
    public DateTime BlockedAt { get; set; }
}