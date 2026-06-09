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

    public async Task SendPrivateMessage(string to, string senderSymKey, string receiverSymKey, string payload)
    {
        var myNickname = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(myNickname)) return;

        // Check if either user blocked the other
        var db = _messages.Database; // We need to access Users collection
        var usersCol = db.GetCollection<User>("Users");
        
        var senderUser = await usersCol.Find(u => u.Nickname == myNickname).FirstOrDefaultAsync();
        var receiverUser = await usersCol.Find(u => u.Nickname == to).FirstOrDefaultAsync();

        if (senderUser == null || receiverUser == null) return;

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
            await Clients.Client(targetId).SendAsync("ReceiveMessage", myNickname, receiverSymKey, payload);
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
