using PRIVATE.MESSAGING.Core.Entities;

namespace PRIVATE.MESSAGING.Core.Interfaces;

public interface IChatService
{
    void UserConnected(string nickname, string deviceId, string connectionId, string ephemeralPublicKey);
    bool UserDisconnected(string nickname, string deviceId);
    Dictionary<string, (string ConnectionId, string EphemeralPublicKey)> GetActiveConnections(string nickname);

    Task<ChatMessage> SendPrivateMessageAsync(string senderNickname, string to, string senderSymKey, Dictionary<string, string> ephemeralSymKeys, string signature, string payload, string? replyToMessageId);
    Task<(string FinalEmoji, string SenderNickname, string ReceiverNickname)> AddReactionAsync(string myNickname, string messageId, string emoji);
    Task<ChatMessage> DeleteMessageAsync(string myNickname, string messageId);
    Task<long> MarkMessagesAsReadAsync(string myNickname, string targetNickname);
    Task UpdateOnlineStatusAsync(string nickname, bool isOnline);
    Task<IEnumerable<ChatMessage>> SyncMissedMessagesAsync(string myNickname, string lastMessageId);
}
