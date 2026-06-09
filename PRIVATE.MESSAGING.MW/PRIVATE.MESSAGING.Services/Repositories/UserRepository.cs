using MongoDB.Bson;
using MongoDB.Driver;
using PRIVATE.MESSAGING.Core.Entities;
using PRIVATE.MESSAGING.Core.Entities.Attributes;
using PRIVATE.MESSAGING.Core.Interfaces;

namespace PRIVATE.MESSAGING.Services.Repositories;

public class UserRepository : IUserRepository
{
    private readonly IMongoCollection<User> _users;
    private readonly IMongoCollection<ChatMessage> _messages;

    public UserRepository(IMongoDatabase database)
    {
        _users = database.GetCollection<User>("Users");
        _messages = database.GetCollection<ChatMessage>("ChatMessages");
    }

    public async Task<User?> GetByNicknameAsync(string nickname)
    {
        return await _users.Find(u => u.Nickname == nickname).FirstOrDefaultAsync();
    }

    public async Task<List<User>> GetUsersByNicknamesAsync(IEnumerable<string> nicknames)
    {
        return await _users.Find(u => nicknames.Contains(u.Nickname)).ToListAsync();
    }

    public async Task<bool> UpdateProfilePictureAsync(string nickname, string base64Image)
    {
        var update = Builders<User>.Update.Set(u => u.ProfilePictureBase64, base64Image);
        var result = await _users.UpdateOneAsync(u => u.Nickname == nickname, update);
        return result.MatchedCount > 0;
    }

    public async Task<(List<User> Users, string? NextCursor, long TotalCount)> SearchContactsAsync(string myNickname, string query, string? cursor, int limit)
    {
        var filterBuilder = Builders<User>.Filter;
        var filter = filterBuilder.Ne(u => u.Nickname, myNickname);

        if (!string.IsNullOrWhiteSpace(query))
        {
            var searchFilter = filterBuilder.Regex(u => u.Nickname, new MongoDB.Bson.BsonRegularExpression(query, "i"));
            filter = filterBuilder.And(filter, searchFilter);
        }

        if (!string.IsNullOrEmpty(cursor))
        {
            var cursorFilter = filterBuilder.Gt(u => u.Nickname, cursor);
            filter = filterBuilder.And(filter, cursorFilter);
        }

        var totalCount = await _users.CountDocumentsAsync(filter);

        var users = await _users.Find(filter)
            .SortBy(u => u.Nickname)
            .Limit(limit + 1)
            .ToListAsync();

        string? nextCursor = null;
        if (users.Count > limit)
        {
            nextCursor = users.Last().Nickname;
            users.RemoveAt(users.Count - 1);
        }

        return (users, nextCursor, totalCount);
    }

    public async Task<bool> AddBlockedUserAsync(string blockerNickname, BlockedUserInfo blockedUser)
    {
        var filter = Builders<User>.Filter.Eq(u => u.Nickname, blockerNickname);
        var update = Builders<User>.Update.Push(u => u.BlockedUsers, blockedUser);
        var result = await _users.UpdateOneAsync(filter, update);
        return result.ModifiedCount > 0;
    }

    public async Task<bool> RemoveBlockedUserAsync(string blockerNickname, string targetNickname)
    {
        var filter = Builders<User>.Filter.Eq(u => u.Nickname, blockerNickname);
        var update = Builders<User>.Update.PullFilter(u => u.BlockedUsers, b => b.Nickname == targetNickname);
        var result = await _users.UpdateOneAsync(filter, update);
        return result.ModifiedCount > 0;
    }

    public async Task<List<ChatMessage>> GetInboxMessagesAsync(string nickname)
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
        
        return await _messages.Find(filter).SortByDescending(x => x.Timestamp).ToListAsync();
    }
}
