using MongoDB.Driver;
using PRIVATE.MESSAGING.Core.Entities;
using PRIVATE.MESSAGING.Core.Entities.Attributes;
using PRIVATE.MESSAGING.Core.Interfaces;

namespace PRIVATE.MESSAGING.Services;

public class UserService : IUserService
{
    private readonly IMongoCollection<User> _users;
    private readonly IMongoCollection<ChatMessage> _messages;

    public UserService(IMongoDatabase database)
    {
        _users = database.GetCollection<User>("Users");
        _messages = database.GetCollection<ChatMessage>("ChatMessages");
    }

    public async Task<bool> UpdateProfilePictureAsync(string nickname, string base64Image)
    {
        var update = Builders<User>.Update.Set(u => u.ProfilePictureBase64, base64Image);
        var result = await _users.UpdateOneAsync(u => u.Nickname == nickname, update);
        return result.MatchedCount > 0;
    }

    public async Task<object?> GetProfileAsync(string targetNickname, string? callerNickname)
    {
        var targetUser = await _users.Find(u => u.Nickname == targetNickname).FirstOrDefaultAsync();
        if (targetUser == null) return null;

        if (callerNickname != null && targetUser.BlockedUsers != null && targetUser.BlockedUsers.Any(b => b.Nickname == callerNickname))
        {
            return new { 
                nickname = targetUser.Nickname, 
                publicKey = targetUser.PublicKey, 
                profilePictureBase64 = "data:image/gif;base64,R0lGODlhAQABAAD/ACwAAAAAAQABAAACADs=",
                lastSeen = (DateTime?)null,
                isOnline = false
            };
        }

        return new { 
            nickname = targetUser.Nickname, 
            publicKey = targetUser.PublicKey, 
            profilePictureBase64 = targetUser.ProfilePictureBase64,
            lastSeen = targetUser.LastSeen,
            isOnline = targetUser.IsOnline
        };
    }

    public async Task<IEnumerable<object>> GetContactsAsync(string myNickname, string? query)
    {
        var filterBuilder = Builders<User>.Filter;
        var filter = filterBuilder.Ne(u => u.Nickname, myNickname);

        if (!string.IsNullOrWhiteSpace(query))
        {
            var searchFilter = filterBuilder.Regex(u => u.Nickname, new MongoDB.Bson.BsonRegularExpression(query, "i"));
            filter = filterBuilder.And(filter, searchFilter);
        }

        var users = await _users.Find(filter).Limit(7).ToListAsync();
        
        return users.Select(u => new 
        {
            nickname = u.Nickname,
            profilePictureBase64 = u.BlockedUsers != null && u.BlockedUsers.Any(b => b.Nickname == myNickname) ? "data:image/gif;base64,R0lGODlhAQABAAD/ACwAAAAAAQABAAACADs=" : u.ProfilePictureBase64,
            lastSeen = u.BlockedUsers != null && u.BlockedUsers.Any(b => b.Nickname == myNickname) ? (DateTime?)null : u.LastSeen
        });
    }

    public async Task<IEnumerable<object>> GetInboxAsync(string nickname)
    {
        var filter = Builders<ChatMessage>.Filter.And(
            Builders<ChatMessage>.Filter.Or(
                Builders<ChatMessage>.Filter.Eq(x => x.SenderNickname, nickname),
                Builders<ChatMessage>.Filter.Eq(x => x.ReceiverNickname, nickname)
            ),
            Builders<ChatMessage>.Filter.Not(
                Builders<ChatMessage>.Filter.AnyEq(x => x.DeletedFor, nickname)
            )
        );
        
        var allMessages = await _messages.Find(filter).SortByDescending(x => x.Timestamp).ToListAsync();
        
        var inbox = allMessages
            .GroupBy(m => m.SenderNickname == nickname ? m.ReceiverNickname : m.SenderNickname)
            .Select(g => new 
            {
                ContactNickname = g.Key,
                LastMessage = g.First(),
                UnreadCount = g.Count(m => m.ReceiverNickname == nickname && !m.IsRead)
            }).ToList();

        var contacts = inbox.Select(i => i.ContactNickname).ToList();
        var profiles = await _users.Find(u => contacts.Contains(u.Nickname)).ToListAsync();

        var result = new List<object>();
        foreach(var item in inbox)
        {
            var p = profiles.FirstOrDefault(x => x.Nickname == item.ContactNickname);
            var isBlockedByThem = p != null && p.BlockedUsers != null && p.BlockedUsers.Any(b => b.Nickname == nickname);

            result.Add(new {
                contactNickname = item.ContactNickname,
                lastMessage = item.LastMessage,
                unreadCount = item.UnreadCount,
                profilePictureBase64 = isBlockedByThem ? "data:image/gif;base64,R0lGODlhAQABAAD/ACwAAAAAAQABAAACADs=" : p?.ProfilePictureBase64
            });
        }

        return result;
    }

    public async Task<bool> BlockUserAsync(string myNickname, string targetNickname)
    {
        var update = Builders<User>.Update.AddToSet(u => u.BlockedUsers, new BlockedUserInfo { Nickname = targetNickname, BlockedAt = DateTime.UtcNow });
        var result = await _users.UpdateOneAsync(u => u.Nickname == myNickname, update);
        return result.ModifiedCount > 0;
    }

    public async Task<bool> UnblockUserAsync(string myNickname, string targetNickname)
    {
        var update = Builders<User>.Update.PullFilter(u => u.BlockedUsers, b => b.Nickname == targetNickname);
        var result = await _users.UpdateOneAsync(u => u.Nickname == myNickname, update);
        return result.ModifiedCount > 0;
    }

    public async Task<IEnumerable<string>> GetBlockedUsersAsync(string myNickname)
    {
        var me = await _users.Find(u => u.Nickname == myNickname).FirstOrDefaultAsync();
        if (me == null || me.BlockedUsers == null) return new List<string>();

        return me.BlockedUsers.Select(b => b.Nickname);
    }
}
