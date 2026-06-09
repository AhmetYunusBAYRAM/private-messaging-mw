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
    public string ProfilePictureBase64 { get; set; } = string.Empty;
    
    public string? Otp { get; set; }
    public DateTime? OtpExpiry { get; set; }

    public DateTime LastSeen { get; set; }
}