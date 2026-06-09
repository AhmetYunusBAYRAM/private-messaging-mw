using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace PRIVATE.MESSAGING.Core.Entities;

public class ChatMessage
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }
    
    public string SenderNickname { get; set; } = string.Empty;
    public string ReceiverNickname { get; set; } = string.Empty;
    
    public string? ReplyToMessageId { get; set; }
    
    public string SenderEncryptedSymKey { get; set; } = string.Empty;
    public string ReceiverEncryptedSymKey { get; set; } = string.Empty;
    
    public string EncryptedPayload { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    public bool IsDeleted { get; set; } = false;
    
    public bool IsRead { get; set; } = false;
    public DateTime? ReadAt { get; set; }
    
    public Dictionary<string, string> Reactions { get; set; } = new();

    public List<string> DeletedFor { get; set; } = new();
}
