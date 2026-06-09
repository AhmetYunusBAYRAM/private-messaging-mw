using System.Collections.Concurrent;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using MongoDB.Driver;
using PRIVATE.MESSAGING.MW.Models;
using System.Security.Claims;
using PRIVATE.MESSAGING.MW.Models.Attributes;

namespace PRIVATE.MESSAGING.MW.Services;

[Authorize]
public class ChatHub : Hub
{
    private readonly IMongoCollection<ChatMessage> _messages;
    private static readonly ConcurrentDictionary<string, string> _users = new();

    public ChatHub(IMongoDatabase database)
    {
        _messages = database.GetCollection<ChatMessage>("ChatMessages");
    }

    public override Task OnConnectedAsync()
    {
        var nickname = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(nickname))
        {
            _users[nickname] = Context.ConnectionId;
        }
        return base.OnConnectedAsync();
    }

    public async Task<string> SendPrivateMessage(string to, string senderSymKey, string receiverSymKey, string payload)
    {
        var myNickname = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(myNickname)) return null;

        // Check if either user blocked the other
        var db = _messages.Database; // We need to access Users collection
        var usersCol = db.GetCollection<User>("Users");
        
        var senderUser = await usersCol.Find(u => u.Nickname == myNickname).FirstOrDefaultAsync();
        var receiverUser = await usersCol.Find(u => u.Nickname == to).FirstOrDefaultAsync();

        if (senderUser == null || receiverUser == null) return null;

        if (senderUser.BlockedUsers.Any(b => b.Nickname == to))
        {
            // The sender blocked the receiver, cannot send.
            throw new HubException($"You have blocked {to}. Unblock them to send messages.");
        }

        var blockInfo = receiverUser.BlockedUsers.FirstOrDefault(b => b.Nickname == myNickname);
        if (blockInfo != null)
        {
            // The receiver blocked the sender. Throw specific error with date!
            var localTime = blockInfo.BlockedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
            throw new HubException($"Bu kullanıcı sizi {localTime} tarihinde engelledi, ona mesaj gönderemezsiniz.");
        }

        var chatMsg = new ChatMessage
        {
            SenderNickname = myNickname,
            ReceiverNickname = to,
            SenderEncryptedSymKey = senderSymKey,
            ReceiverEncryptedSymKey = receiverSymKey,
            EncryptedPayload = payload,
            Timestamp = DateTime.UtcNow
        };
        
        await _messages.InsertOneAsync(chatMsg);

        if (_users.TryGetValue(to, out var targetId))
        {
            await Clients.Client(targetId).SendAsync("ReceiveMessage", chatMsg.Id, myNickname, receiverSymKey, payload);
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
        if (myUser != null && myUser.BlockedUsers.Any(b => b.Nickname == targetNickname))
        {
            throw new HubException("Bu kullanıcıyı engellediğiniz için ifade bırakamazsınız.");
        }

        var targetUser = await usersCol.Find(u => u.Nickname == targetNickname).FirstOrDefaultAsync();
        if (targetUser != null)
        {
            var blockInfo = targetUser.BlockedUsers.FirstOrDefault(b => b.Nickname == myNickname);
            if (blockInfo != null)
            {
                var localTime = blockInfo.BlockedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
                throw new HubException($"Bu kullanıcı sizi {localTime} tarihinde engelledi, ona ifade bırakamazsınız.");
            }
        }

        string finalEmoji = emoji;
        if (msg.Reactions != null && msg.Reactions.TryGetValue(myNickname, out var existingEmoji) && existingEmoji == emoji)
        {
            // Aynı emojiye tıklandıysa kaldır
            msg.Reactions.Remove(myNickname);
            var update = Builders<ChatMessage>.Update.Set(m => m.Reactions, msg.Reactions);
            await _messages.UpdateOneAsync(m => m.Id == messageId, update);
            finalEmoji = ""; // Kaldırıldığını belirtmek için
        }
        else
        {
            // Farklı emojiye tıklandıysa veya ilk kez atılıyorsa ekle/güncelle
            if (msg.Reactions == null) msg.Reactions = new Dictionary<string, string>();
            msg.Reactions[myNickname] = emoji;
            
            var update = Builders<ChatMessage>.Update.Set(m => m.Reactions, msg.Reactions);
            await _messages.UpdateOneAsync(m => m.Id == messageId, update);
        }

        // Notify both parties if they are online
        if (_users.TryGetValue(msg.SenderNickname, out var senderId))
        {
            await Clients.Client(senderId).SendAsync("ReceiveReaction", messageId, myNickname, finalEmoji);
        }
        
        if (msg.SenderNickname != msg.ReceiverNickname && _users.TryGetValue(msg.ReceiverNickname, out var receiverId))
        {
            await Clients.Client(receiverId).SendAsync("ReceiveReaction", messageId, myNickname, finalEmoji);
        }
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        var nickname = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(nickname))
        {
            _users.TryRemove(nickname, out _);
        }
        return base.OnDisconnectedAsync(exception);
    }
}
