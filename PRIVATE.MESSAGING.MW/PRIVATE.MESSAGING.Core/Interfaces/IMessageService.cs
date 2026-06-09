using PRIVATE.MESSAGING.Core.Entities;

namespace PRIVATE.MESSAGING.Core.Interfaces;

public interface IMessageService
{
    Task<IEnumerable<ChatMessage>> GetHistoryAsync(string myNickname, string contactNickname);
    Task ClearHistoryAsync(string myNickname, string contactNickname);
}
