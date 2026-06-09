using System.Collections.Concurrent;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using MongoDB.Driver;
using PRIVATE.MESSAGING.Core.Entities;
using PRIVATE.MESSAGING.Core.Entities.Attributes;
using System.Security.Claims;

namespace PRIVATE.MESSAGING.MW.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly IMongoCollection<ChatMessage> _messages;
    private static readonly ConcurrentDictionary<string, string> _users = new();

    public ChatHub(IMongoDatabase database)
    {
        _messages = database.GetCollection<ChatMessage>("ChatMessages");
    }

    public override async Task OnConnectedAsync()
    {
        var nickname = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(nickname))
        {
            _users[nickname] = Context.ConnectionId;
            
            var db = _messages.Database;
            var usersCol = db.GetCollection<User>("Users");
            var update = Builders<User>.Update
                .Set(u => u.IsOnline, true)
                .Set(u => u.LastSeen, DateTime.UtcNow);
            await usersCol.UpdateOneAsync(u => u.Nickname == nickname, update);

            await Clients.All.SendAsync("UserPresenceUpdate", nickname, true, DateTime.UtcNow);
        }
        await base.OnConnectedAsync();
    }

    public async Task<string> SendPrivateMessage(string to, string senderSymKey, string receiverSymKey, string payload, string? replyToMessageId = null)
    {
        var myNickname = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(myNickname)) return null;

        var db = _messages.Database;
        var usersCol = db.GetCollection<User>("Users");
        
        var senderUser = await usersCol.Find(u => u.Nickname == myNickname).FirstOrDefaultAsync();
        var receiverUser = await usersCol.Find(u => u.Nickname == to).FirstOrDefaultAsync();

        if (senderUser == null || receiverUser == null) return null;

        if (senderUser.BlockedUsers != null && senderUser.BlockedUsers.Any(b => b.Nickname == to))
        {
            throw new HubException($"You have blocked {to}. Unblock them to send messages.");
        }

        var blockInfo = receiverUser.BlockedUsers?.FirstOrDefault(b => b.Nickname == myNickname);
        if (blockInfo != null)
        {
            var localTime = blockInfo.BlockedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
            throw new HubException($"Bu kullanıcı sizi {localTime} tarihinde engelledi, ona mesaj gönderemezsiniz.");
        }

        var chatMsg = new ChatMessage
        {
            SenderNickname = myNickname,
            ReceiverNickname = to,
            ReplyToMessageId = replyToMessageId,
            SenderEncryptedSymKey = senderSymKey,
            ReceiverEncryptedSymKey = receiverSymKey,
            EncryptedPayload = payload,
            Timestamp = DateTime.UtcNow
        };
        
        await _messages.InsertOneAsync(chatMsg);

        if (_users.TryGetValue(to, out var targetId))
        {
            await Clients.Client(targetId).SendAsync("ReceiveMessage", chatMsg.Id, myNickname, receiverSymKey, payload, replyToMessageId);
        }

        return chatMsg.Id;
    }

    public async Task AddReaction(string messageId, string emoji)
    {
        var myNickname = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(myNickname)) return;

        var msg = await _messages.Find(m => m.Id == messageId).FirstOrDefaultAsync();
        if (msg == null) return;

        var targetNickname = msg.SenderNickname == myNickname ? msg.ReceiverNickname : msg.SenderNickname;

        var db = _messages.Database;
        var usersCol = db.GetCollection<User>("Users");
        
        var myUser = await usersCol.Find(u => u.Nickname == myNickname).FirstOrDefaultAsync();
        if (myUser != null && myUser.BlockedUsers != null && myUser.BlockedUsers.Any(b => b.Nickname == targetNickname))
        {
            throw new HubException("Bu kullanıcıyı engellediğiniz için ifade bırakamazsınız.");
        }

        var targetUser = await usersCol.Find(u => u.Nickname == targetNickname).FirstOrDefaultAsync();
        if (targetUser != null)
        {
            var blockInfo = targetUser.BlockedUsers?.FirstOrDefault(b => b.Nickname == myNickname);
            if (blockInfo != null)
            {
                var localTime = blockInfo.BlockedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
                throw new HubException($"Bu kullanıcı sizi {localTime} tarihinde engelledi, ona ifade bırakamazsınız.");
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

        if (_users.TryGetValue(msg.SenderNickname, out var senderId))
        {
            await Clients.Client(senderId).SendAsync("ReceiveReaction", messageId, myNickname, finalEmoji);
        }
        
        if (msg.SenderNickname != msg.ReceiverNickname && _users.TryGetValue(msg.ReceiverNickname, out var receiverId))
        {
            await Clients.Client(receiverId).SendAsync("ReceiveReaction", messageId, myNickname, finalEmoji);
        }
    }

    public async Task DeleteMessage(string messageId)
    {
        var myNickname = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(myNickname)) return;

        var msg = await _messages.Find(m => m.Id == messageId).FirstOrDefaultAsync();
        if (msg == null) return;

        if (msg.SenderNickname != myNickname)
        {
            throw new HubException("Sadece kendi gönderdiğiniz mesajları silebilirsiniz.");
        }

        var update = Builders<ChatMessage>.Update
            .Set(m => m.IsDeleted, true)
            .Set(m => m.EncryptedPayload, "")
            .Set(m => m.SenderEncryptedSymKey, "")
            .Set(m => m.ReceiverEncryptedSymKey, "")
            .Set(m => m.Reactions, new Dictionary<string, string>());

        await _messages.UpdateOneAsync(m => m.Id == messageId, update);

        if (_users.TryGetValue(msg.SenderNickname, out var senderId))
        {
            await Clients.Client(senderId).SendAsync("MessageDeleted", messageId);
        }
        
        if (msg.SenderNickname != msg.ReceiverNickname && _users.TryGetValue(msg.ReceiverNickname, out var receiverId))
        {
            await Clients.Client(receiverId).SendAsync("MessageDeleted", messageId);
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var nickname = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(nickname))
        {
            _users.TryRemove(nickname, out _);
            
            var db = _messages.Database;
            var usersCol = db.GetCollection<User>("Users");
            var update = Builders<User>.Update
                .Set(u => u.IsOnline, false)
                .Set(u => u.LastSeen, DateTime.UtcNow);
            await usersCol.UpdateOneAsync(u => u.Nickname == nickname, update);

            await Clients.All.SendAsync("UserPresenceUpdate", nickname, false, DateTime.UtcNow);
        }
        await base.OnDisconnectedAsync(exception);
    }

    public async Task MarkMessagesAsRead(string targetNickname)
    {
        var myNickname = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(myNickname)) return;

        var filter = Builders<ChatMessage>.Filter.And(
            Builders<ChatMessage>.Filter.Eq(m => m.SenderNickname, targetNickname),
            Builders<ChatMessage>.Filter.Eq(m => m.ReceiverNickname, myNickname),
            Builders<ChatMessage>.Filter.Eq(m => m.IsRead, false)
        );

        var update = Builders<ChatMessage>.Update
            .Set(m => m.IsRead, true)
            .Set(m => m.ReadAt, DateTime.UtcNow);

        var result = await _messages.UpdateManyAsync(filter, update);

        if (result.ModifiedCount > 0)
        {
            if (_users.TryGetValue(targetNickname, out var targetId))
            {
                await Clients.Client(targetId).SendAsync("MessagesRead", myNickname);
            }
        }
    }
}
