using Microsoft.Extensions.Caching.Distributed;
using MongoDB.Driver;
using PRIVATE.MESSAGING.Core.Entities;
using PRIVATE.MESSAGING.Core.Entities.Attributes;
using PRIVATE.MESSAGING.Core.Interfaces;
using PRIVATE.MESSAGING.DTOs.Responses;
using System.Text.Json;

namespace PRIVATE.MESSAGING.Services;

public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly IDistributedCache _cache;

    public UserService(IUserRepository userRepository, IDistributedCache cache)
    {
        _userRepository = userRepository;
        _cache = cache;
    }

    public async Task<bool> UpdateProfilePictureAsync(string nickname, string base64Image)
    {
        var result = await _userRepository.UpdateProfilePictureAsync(nickname, base64Image);
        
        if (result)
        {
            await _cache.RemoveAsync($"profile_{nickname}");
        }
        
        return result;
    }

    public async Task<object?> GetProfileAsync(string targetNickname, string? callerNickname)
    {
        User? targetUser = null;
        var cacheKey = $"profile_{targetNickname}";
        var cachedProfile = await _cache.GetStringAsync(cacheKey);

        if (!string.IsNullOrEmpty(cachedProfile))
        {
            targetUser = JsonSerializer.Deserialize<User>(cachedProfile);
        }

        if (targetUser == null)
        {
            targetUser = await _userRepository.GetByNicknameAsync(targetNickname);
            if (targetUser == null) return null;

            var cacheOptions = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30) };
            await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(targetUser), cacheOptions);
        }

        if (callerNickname != null && targetUser.BlockedUsers != null && targetUser.BlockedUsers.Any(b => b.Nickname == callerNickname))
        {
            return new { 
                nickname = targetUser.Nickname, 
                identityPublicKey = targetUser.IdentityPublicKey, 
                profilePictureBase64 = "data:image/gif;base64,R0lGODlhAQABAAD/ACwAAAAAAQABAAACADs=",
                lastSeen = (DateTime?)null,
                isOnline = false,
                hasBlockedYou = true
            };
        }

        return new { 
            nickname = targetUser.Nickname, 
            identityPublicKey = targetUser.IdentityPublicKey, 
            profilePictureBase64 = targetUser.ProfilePictureBase64,
            lastSeen = targetUser.LastSeen,
            isOnline = targetUser.IsOnline,
            hasBlockedYou = false
        };
    }

    public async Task<PagedResponse<object>> GetContactsAsync(string myNickname, string? query, string? cursor, int limit)
    {
        var searchResult = await _userRepository.SearchContactsAsync(myNickname, query ?? "", cursor, limit);
        var users = searchResult.Users;
        var nextCursor = searchResult.NextCursor;
        
        var items = users.Select(u => new 
        {
            nickname = u.Nickname,
            profilePictureBase64 = u.BlockedUsers != null && u.BlockedUsers.Any(b => b.Nickname == myNickname) ? "data:image/gif;base64,R0lGODlhAQABAAD/ACwAAAAAAQABAAACADs=" : u.ProfilePictureBase64,
            lastSeen = u.BlockedUsers != null && u.BlockedUsers.Any(b => b.Nickname == myNickname) ? (DateTime?)null : u.LastSeen
        }).Cast<object>().ToList();

        return new PagedResponse<object>
        {
            Items = items,
            TotalCount = (int)searchResult.TotalCount,
            NextCursor = nextCursor
        };
    }

    public async Task<IEnumerable<object>> GetInboxAsync(string nickname)
    {
        var allMessages = await _userRepository.GetInboxMessagesAsync(nickname);
        
        var inbox = allMessages
            .GroupBy(m => m.SenderNickname == nickname ? m.ReceiverNickname : m.SenderNickname)
            .Select(g => new 
            {
                ContactNickname = g.Key,
                LastMessage = g.First(),
                UnreadCount = g.Count(m => m.ReceiverNickname == nickname && !m.IsRead)
            }).ToList();

        var contacts = inbox.Select(i => i.ContactNickname).ToList();
        var profiles = await _userRepository.GetUsersByNicknamesAsync(contacts);

        var myUser = await _userRepository.GetByNicknameAsync(nickname);

        var result = new List<object>();
        foreach(var item in inbox)
        {
            var p = profiles.FirstOrDefault(x => x.Nickname == item.ContactNickname);
            var isBlockedByThem = p != null && p.BlockedUsers != null && p.BlockedUsers.Any(b => b.Nickname == nickname);
            var isBlockedByMe = myUser != null && myUser.BlockedUsers != null && myUser.BlockedUsers.Any(b => b.Nickname == item.ContactNickname);
            var hidePic = isBlockedByThem || isBlockedByMe;

            result.Add(new {
                contactNickname = item.ContactNickname,
                lastMessage = item.LastMessage,
                unreadCount = item.UnreadCount,
                profilePictureBase64 = hidePic ? "data:image/gif;base64,R0lGODlhAQABAAD/ACwAAAAAAQABAAACADs=" : p?.ProfilePictureBase64
            });
        }

        return result;
    }

    public async Task<bool> BlockUserAsync(string myNickname, string targetNickname)
    {
        if (myNickname == targetNickname) return false;

        var myUser = await _userRepository.GetByNicknameAsync(myNickname);
        var targetUser = await _userRepository.GetByNicknameAsync(targetNickname);
        if (myUser == null || targetUser == null) return false;

        if (myUser.BlockedUsers != null && myUser.BlockedUsers.Any(b => b.Nickname == targetNickname)) return true;

        var blocked = new BlockedUserInfo { Nickname = targetNickname, BlockedAt = DateTime.UtcNow };
        await _userRepository.AddBlockedUserAsync(myNickname, blocked);
        await _cache.RemoveAsync($"profile_{myNickname}");
        return true;
    }

    public async Task<bool> UnblockUserAsync(string myNickname, string targetNickname)
    {
        await _userRepository.RemoveBlockedUserAsync(myNickname, targetNickname);
        await _cache.RemoveAsync($"profile_{myNickname}");
        return true;
    }

    public async Task<IEnumerable<string>> GetBlockedUsersAsync(string myNickname)
    {
        var user = await _userRepository.GetByNicknameAsync(myNickname);
        return user?.BlockedUsers?.Select(b => b.Nickname) ?? Array.Empty<string>();
    }

    public async Task<IEnumerable<object>> GetDeviceLogsAsync(string nickname)
    {
        var user = await _userRepository.GetByNicknameAsync(nickname);
        if (user == null || user.DeviceLogs == null) return new List<object>();

        return user.DeviceLogs.OrderByDescending(d => d.LastActiveAt).Select(d => new
        {
            deviceId = d.DeviceName,
            ipAddress = d.IpAddress,
            lastActive = d.LastActiveAt
        });
    }
}
