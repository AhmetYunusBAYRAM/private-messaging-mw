using PRIVATE.MESSAGING.Core.Entities;

namespace PRIVATE.MESSAGING.Core.Interfaces;

public interface IUserService
{
    Task<bool> UpdateProfilePictureAsync(string nickname, string base64Image);
    Task<object?> GetProfileAsync(string targetNickname, string? callerNickname);
    Task<IEnumerable<object>> GetContactsAsync(string myNickname, string? query);
    Task<IEnumerable<object>> GetInboxAsync(string nickname);
    Task<bool> BlockUserAsync(string myNickname, string targetNickname);
    Task<bool> UnblockUserAsync(string myNickname, string targetNickname);
    Task<IEnumerable<string>> GetBlockedUsersAsync(string myNickname);
}
