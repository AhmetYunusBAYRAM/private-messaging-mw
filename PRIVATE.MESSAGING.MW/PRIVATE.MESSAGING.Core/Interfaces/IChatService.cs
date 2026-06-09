using PRIVATE.MESSAGING.Core.Entities;

namespace PRIVATE.MESSAGING.Core.Interfaces;

public interface IChatService
{
    void UserConnected(string nickname, string connectionId);
    bool UserDisconnected(string nickname);
    string? GetConnectionId(string nickname);

    Task<ChatMessage> SendPrivateMessageAsync(string senderNickname, string to, string senderSymKey, string receiverSymKey, string payload, string? replyToMessageId);
    Task<(string FinalEmoji, string SenderNickname, string ReceiverNickname)> AddReactionAsync(string myNickname, string messageId, string emoji);
    Task<ChatMessage> DeleteMessageAsync(string myNickname, string messageId);
    Task<long> MarkMessagesAsReadAsync(string myNickname, string targetNickname);
    Task UpdateOnlineStatusAsync(string nickname, bool isOnline);
}
