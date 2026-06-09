using PRIVATE.MESSAGING.Core.Entities;
using PRIVATE.MESSAGING.Core.Entities.Attributes;

namespace PRIVATE.MESSAGING.Core.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByNicknameAsync(string nickname);
    Task<List<User>> GetUsersByNicknamesAsync(IEnumerable<string> nicknames);
    Task<bool> UpdateProfilePictureAsync(string nickname, string base64Image);
    Task<(List<User> Users, string? NextCursor, long TotalCount)> SearchContactsAsync(string myNickname, string query, string? cursor, int limit);
    Task<bool> AddBlockedUserAsync(string blockerNickname, BlockedUserInfo blockedUser);
    Task<bool> RemoveBlockedUserAsync(string blockerNickname, string targetNickname);
    Task<List<ChatMessage>> GetInboxMessagesAsync(string nickname);
}
