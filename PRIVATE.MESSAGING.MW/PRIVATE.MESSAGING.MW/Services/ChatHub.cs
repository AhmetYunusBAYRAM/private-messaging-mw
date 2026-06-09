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
        if (!string.IsNullOrEmpty(nickname))
        {
            _chatService.UserConnected(nickname, Context.ConnectionId);
            await _chatService.UpdateOnlineStatusAsync(nickname, true);
            await Clients.All.SendAsync("UserPresenceUpdate", nickname, true, DateTime.UtcNow);
        }
        await base.OnConnectedAsync();
    }

    public async Task<string?> SendPrivateMessage(string to, string senderSymKey, string receiverSymKey, string payload, string? replyToMessageId = null)
    {
        var myNickname = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(myNickname)) return null;

        try
        {
            var chatMsg = await _chatService.SendPrivateMessageAsync(myNickname, to, senderSymKey, receiverSymKey, payload, replyToMessageId);

            var targetConnectionId = _chatService.GetConnectionId(to);
            if (targetConnectionId != null)
            {
                await Clients.Client(targetConnectionId).SendAsync("ReceiveMessage", chatMsg.Id, myNickname, receiverSymKey, payload, replyToMessageId);
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

            var senderId = _chatService.GetConnectionId(senderNickname);
            if (senderId != null)
                await Clients.Client(senderId).SendAsync("ReceiveReaction", messageId, myNickname, finalEmoji);

            if (senderNickname != receiverNickname)
            {
                var receiverId = _chatService.GetConnectionId(receiverNickname);
                if (receiverId != null)
                    await Clients.Client(receiverId).SendAsync("ReceiveReaction", messageId, myNickname, finalEmoji);
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

            var senderId = _chatService.GetConnectionId(msg.SenderNickname);
            if (senderId != null)
                await Clients.Client(senderId).SendAsync("MessageDeleted", messageId);

            if (msg.SenderNickname != msg.ReceiverNickname)
            {
                var receiverId = _chatService.GetConnectionId(msg.ReceiverNickname);
                if (receiverId != null)
                    await Clients.Client(receiverId).SendAsync("MessageDeleted", messageId);
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
        if (!string.IsNullOrEmpty(nickname))
        {
            _chatService.UserDisconnected(nickname);
            await _chatService.UpdateOnlineStatusAsync(nickname, false);
            await Clients.All.SendAsync("UserPresenceUpdate", nickname, false, DateTime.UtcNow);
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
            var targetConnectionId = _chatService.GetConnectionId(targetNickname);
            if (targetConnectionId != null)
                await Clients.Client(targetConnectionId).SendAsync("MessagesRead", myNickname);
        }
    }
}
