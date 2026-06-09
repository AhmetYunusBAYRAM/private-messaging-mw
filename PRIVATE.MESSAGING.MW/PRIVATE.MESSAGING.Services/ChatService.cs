using Microsoft.Extensions.Caching.Distributed;
using MongoDB.Driver;
using PRIVATE.MESSAGING.Core.Entities;
using PRIVATE.MESSAGING.Core.Entities.Attributes;
using PRIVATE.MESSAGING.Core.Interfaces;

namespace PRIVATE.MESSAGING.Services;

public class ChatService : IChatService
{
    private readonly IMongoCollection<ChatMessage> _messages;
    private readonly IMongoCollection<User> _users;
    private readonly IDistributedCache _cache;

    public ChatService(IMongoDatabase database, IDistributedCache cache)
    {
        _messages = database.GetCollection<ChatMessage>("ChatMessages");
        _users = database.GetCollection<User>("Users");
        _cache = cache;
    }

    public void UserConnected(string nickname, string connectionId)
    {
        var options = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24) };
        _cache.SetString($"conn_{nickname}", connectionId, options);
    }

    public bool UserDisconnected(string nickname)
    {
        try
        {
            var key = $"conn_{nickname}";
            if (_cache.GetString(key) == null) return false;
            
            _cache.Remove(key);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public string? GetConnectionId(string nickname)
    {
        return _cache.GetString($"conn_{nickname}");
    }

    public async Task<ChatMessage> SendPrivateMessageAsync(string senderNickname, string to, string senderSymKey, string receiverSymKey, string payload, string? replyToMessageId)
    {
        var senderUser = await _users.Find(u => u.Nickname == senderNickname).FirstOrDefaultAsync();
        var receiverUser = await _users.Find(u => u.Nickname == to).FirstOrDefaultAsync();

        if (senderUser == null || receiverUser == null)
            throw new InvalidOperationException("Kullanıcı bulunamadı.");

        if (senderUser.BlockedUsers != null && senderUser.BlockedUsers.Any(b => b.Nickname == to))
            throw new InvalidOperationException($"You have blocked {to}. Unblock them to send messages.");

        var blockInfo = receiverUser.BlockedUsers?.FirstOrDefault(b => b.Nickname == senderNickname);
        if (blockInfo != null)
        {
            var localTime = blockInfo.BlockedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
            throw new InvalidOperationException($"Bu kullanıcı sizi {localTime} tarihinde engelledi, ona mesaj gönderemezsiniz.");
        }

        var chatMsg = new ChatMessage
        {
            SenderNickname = senderNickname,
            ReceiverNickname = to,
            ReplyToMessageId = replyToMessageId,
            SenderEncryptedSymKey = senderSymKey,
            ReceiverEncryptedSymKey = receiverSymKey,
            EncryptedPayload = payload,
            Timestamp = DateTime.UtcNow
        };

        await _messages.InsertOneAsync(chatMsg);
        return chatMsg;
    }

    public async Task<(string FinalEmoji, string SenderNickname, string ReceiverNickname)> AddReactionAsync(string myNickname, string messageId, string emoji)
    {
        var msg = await _messages.Find(m => m.Id == messageId).FirstOrDefaultAsync();
        if (msg == null) throw new InvalidOperationException("Mesaj bulunamadı.");

        var targetNickname = msg.SenderNickname == myNickname ? msg.ReceiverNickname : msg.SenderNickname;

        var myUser = await _users.Find(u => u.Nickname == myNickname).FirstOrDefaultAsync();
        if (myUser?.BlockedUsers != null && myUser.BlockedUsers.Any(b => b.Nickname == targetNickname))
            throw new InvalidOperationException("Bu kullanıcıyı engellediğiniz için ifade bırakamazsınız.");

        var targetUser = await _users.Find(u => u.Nickname == targetNickname).FirstOrDefaultAsync();
        if (targetUser != null)
        {
            var blockInfo = targetUser.BlockedUsers?.FirstOrDefault(b => b.Nickname == myNickname);
            if (blockInfo != null)
            {
                var localTime = blockInfo.BlockedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
                throw new InvalidOperationException($"Bu kullanıcı sizi {localTime} tarihinde engelledi, ona ifade bırakamazsınız.");
            }
        }

        string finalEmoji = emoji;
        if (msg.Reactions != null && msg.Reactions.TryGetValue(myNickname, out var existingEmoji) && existingEmoji == emoji)
        {
            msg.Reactions.Remove(myNickname);
            var update = Builders<ChatMessage>.Update.Set(m => m.Reactions, msg.Reactions);
            await _messages.UpdateOneAsync(m => m.Id == messageId, update);
            finalEmoji = "";
        }
        else
        {
            if (msg.Reactions == null) msg.Reactions = new Dictionary<string, string>();
            msg.Reactions[myNickname] = emoji;
            var update = Builders<ChatMessage>.Update.Set(m => m.Reactions, msg.Reactions);
            await _messages.UpdateOneAsync(m => m.Id == messageId, update);
        }

        return (finalEmoji, msg.SenderNickname, msg.ReceiverNickname);
    }

    public async Task<ChatMessage> DeleteMessageAsync(string myNickname, string messageId)
    {
        var msg = await _messages.Find(m => m.Id == messageId).FirstOrDefaultAsync();
        if (msg == null) throw new InvalidOperationException("Mesaj bulunamadı.");

        if (msg.SenderNickname != myNickname)
            throw new InvalidOperationException("Sadece kendi gönderdiğiniz mesajları silebilirsiniz.");

        var update = Builders<ChatMessage>.Update
            .Set(m => m.IsDeleted, true)
            .Set(m => m.EncryptedPayload, "")
            .Set(m => m.SenderEncryptedSymKey, "")
            .Set(m => m.ReceiverEncryptedSymKey, "")
            .Set(m => m.Reactions, new Dictionary<string, string>());

        await _messages.UpdateOneAsync(m => m.Id == messageId, update);
        return msg;
    }

    public async Task<long> MarkMessagesAsReadAsync(string myNickname, string targetNickname)
    {
        var filter = Builders<ChatMessage>.Filter.And(
            Builders<ChatMessage>.Filter.Eq(m => m.SenderNickname, targetNickname),
            Builders<ChatMessage>.Filter.Eq(m => m.ReceiverNickname, myNickname),
            Builders<ChatMessage>.Filter.Eq(m => m.IsRead, false)
        );

        var update = Builders<ChatMessage>.Update
            .Set(m => m.IsRead, true)
            .Set(m => m.ReadAt, DateTime.UtcNow);

        var result = await _messages.UpdateManyAsync(filter, update);
        return result.ModifiedCount;
    }

    public async Task UpdateOnlineStatusAsync(string nickname, bool isOnline)
    {
        var update = Builders<User>.Update
            .Set(u => u.IsOnline, isOnline)
            .Set(u => u.LastSeen, DateTime.UtcNow);
        await _users.UpdateOneAsync(u => u.Nickname == nickname, update);
    }
}
