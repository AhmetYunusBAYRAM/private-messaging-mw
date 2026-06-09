using System.Collections.Concurrent;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using MongoDB.Driver;
using PRIVATE.MESSAGING.MW.Models;
using System.Security.Claims;

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
