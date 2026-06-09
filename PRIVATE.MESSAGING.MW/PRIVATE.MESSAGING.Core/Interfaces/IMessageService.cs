using PRIVATE.MESSAGING.Core.Entities;

using PRIVATE.MESSAGING.DTOs.Responses;

namespace PRIVATE.MESSAGING.Core.Interfaces;

public interface IMessageService
{
    Task<PagedResponse<ChatMessage>> GetHistoryAsync(string myNickname, string contactNickname, int page, int limit);
    Task ClearHistoryAsync(string myNickname, string contactNickname);
}
