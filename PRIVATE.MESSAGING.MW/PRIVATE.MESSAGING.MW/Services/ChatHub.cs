using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using PRIVATE.MESSAGING.Core.Interfaces;

namespace PRIVATE.MESSAGING.MW.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly IChatService _chatService;

    public ChatHub(IChatService chatService)
    {
        _chatService = chatService;
    }

    public override async Task OnConnectedAsync()
    {
        var nickname = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var ephemeralKey = Context.GetHttpContext()?.Request.Query["ephemeralKey"].ToString() ?? "";
        var deviceId = Context.GetHttpContext()?.Request.Query["deviceId"].ToString() ?? "";

        if (!string.IsNullOrEmpty(nickname) && !string.IsNullOrEmpty(deviceId))
        {
            Context.Items["deviceId"] = deviceId;
            _chatService.UserConnected(nickname, deviceId, Context.ConnectionId, ephemeralKey);
            await _chatService.UpdateOnlineStatusAsync(nickname, true);
            await Clients.All.SendAsync("UserPresenceUpdate", nickname, true, DateTime.UtcNow);
        }
        await base.OnConnectedAsync();
    }

    public async Task<string?> SendPrivateMessage(string to, string senderEncryptedKey, Dictionary<string, string> receiverEncryptedKeys, string commonEncryptedPayload, string signature, string? replyToMessageId = null)
    {
        var myNickname = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(myNickname)) return null;

        try
        {
            var chatMsg = await _chatService.SendPrivateMessageAsync(myNickname, to, senderEncryptedKey, receiverEncryptedKeys, commonEncryptedPayload, signature, replyToMessageId);

            var activeConnections = _chatService.GetActiveConnections(to);
            foreach(var kvp in activeConnections)
            {
                var deviceId = kvp.Key;
                var connId = kvp.Value.ConnectionId;
                
                if (chatMsg.ReceiverEncryptedPayloads != null && chatMsg.ReceiverEncryptedPayloads.TryGetValue(deviceId, out var receiverKey))
                {
                    await Clients.Client(connId).SendAsync("ReceiveMessage", chatMsg.Id, myNickname, $"{receiverKey}||{chatMsg.CommonEncryptedPayload}", signature, replyToMessageId);
                }
            }

            return chatMsg.Id;
        }
        catch (InvalidOperationException ex)
        {
            throw new HubException(ex.Message);
        }
    }

    public async Task AddReaction(string messageId, string emoji)
    {
        var myNickname = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(myNickname)) return;

        try
        {
            var (finalEmoji, senderNickname, receiverNickname) = await _chatService.AddReactionAsync(myNickname, messageId, emoji);

            foreach(var conn in _chatService.GetActiveConnections(senderNickname).Values)
            {
                await Clients.Client(conn.ConnectionId).SendAsync("ReceiveReaction", messageId, myNickname, finalEmoji);
            }

            if (senderNickname != receiverNickname)
            {
                foreach(var conn in _chatService.GetActiveConnections(receiverNickname).Values)
                {
                    await Clients.Client(conn.ConnectionId).SendAsync("ReceiveReaction", messageId, myNickname, finalEmoji);
                }
            }
        }
        catch (InvalidOperationException ex)
        {
            throw new HubException(ex.Message);
        }
    }

    public async Task DeleteMessage(string messageId)
    {
        var myNickname = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(myNickname)) return;

        try
        {
            var msg = await _chatService.DeleteMessageAsync(myNickname, messageId);

            foreach(var conn in _chatService.GetActiveConnections(msg.SenderNickname).Values)
            {
                await Clients.Client(conn.ConnectionId).SendAsync("MessageDeleted", messageId);
            }

            if (msg.SenderNickname != msg.ReceiverNickname)
            {
                foreach(var conn in _chatService.GetActiveConnections(msg.ReceiverNickname).Values)
                {
                    await Clients.Client(conn.ConnectionId).SendAsync("MessageDeleted", messageId);
                }
            }
        }
        catch (InvalidOperationException ex)
        {
            throw new HubException(ex.Message);
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var nickname = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(nickname) && Context.Items.TryGetValue("deviceId", out var devIdObj))
        {
            var deviceId = devIdObj?.ToString();
            if (!string.IsNullOrEmpty(deviceId))
            {
                var wasRemoved = _chatService.UserDisconnected(nickname, deviceId, Context.ConnectionId);
                
                if (wasRemoved && !_chatService.GetActiveConnections(nickname).Any())
                {
                    await _chatService.UpdateOnlineStatusAsync(nickname, false);
                    await Clients.All.SendAsync("UserPresenceUpdate", nickname, false, DateTime.UtcNow);
                }
            }
        }
        await base.OnDisconnectedAsync(exception);
    }

    public async Task MarkMessagesAsRead(string targetNickname)
    {
        var myNickname = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(myNickname)) return;

        var modifiedCount = await _chatService.MarkMessagesAsReadAsync(myNickname, targetNickname);

        if (modifiedCount > 0)
        {
            foreach(var conn in _chatService.GetActiveConnections(targetNickname).Values)
            {
                await Clients.Client(conn.ConnectionId).SendAsync("MessagesRead", myNickname);
            }
        }
    }

    public async Task SyncMessages(string lastMessageId)
    {
        var myNickname = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var deviceId = Context.Items["deviceId"] as string;

        if (string.IsNullOrEmpty(myNickname) || string.IsNullOrEmpty(lastMessageId) || string.IsNullOrEmpty(deviceId)) return;

        var missed = await _chatService.SyncMissedMessagesAsync(myNickname, lastMessageId);
        foreach (var msg in missed)
        {
            if (msg.ReceiverEncryptedPayloads != null && msg.ReceiverEncryptedPayloads.TryGetValue(deviceId, out var payload))
            {
                await Clients.Caller.SendAsync("ReceiveMessage", msg.Id, msg.SenderNickname, payload, msg.DigitalSignature, msg.ReplyToMessageId);
            }
        }
    }

    public Dictionary<string, string> GetEphemeralPublicKeys(string targetNickname)
    {
        return _chatService.GetActiveConnections(targetNickname)
                           .ToDictionary(k => k.Key, v => v.Value.EphemeralPublicKey);
    }

    public async Task SendWebRTCSignal(string to, string payload)
    {
        var myNickname = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(myNickname)) return;

        foreach(var conn in _chatService.GetActiveConnections(to).Values)
        {
            await Clients.Client(conn.ConnectionId).SendAsync("ReceiveWebRTCSignal", myNickname, payload);
        }
    }
}
