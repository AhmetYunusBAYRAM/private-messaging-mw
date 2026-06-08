using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using MongoDB.Driver;
using PRIVATE.MESSAGING.MW.Models;

namespace PRIVATE.MESSAGING.MW.Services;

public class ChatHub : Hub
{
    private readonly IMongoCollection<ChatMessage> _messages;
    private static readonly ConcurrentDictionary<string, string> _users = new();

    public ChatHub(IMongoDatabase database)
    {
        _messages = database.GetCollection<ChatMessage>("ChatMessages");
    }

    public async Task Register(string nickname)
    {
        _users[nickname] = Context.ConnectionId;
    }

    public async Task SendPrivateMessage(string myNickname, string to, string senderSymKey, string receiverSymKey, string payload)
    {
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
        var user = _users.FirstOrDefault(x => x.Value == Context.ConnectionId).Key;
        if (user != null)
        {
            _users.TryRemove(user, out _);
        }
        return base.OnDisconnectedAsync(exception);
    }
}
