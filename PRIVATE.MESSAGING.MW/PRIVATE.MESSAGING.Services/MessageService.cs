using MongoDB.Driver;
using PRIVATE.MESSAGING.Core.Entities;
using PRIVATE.MESSAGING.Core.Interfaces;
using PRIVATE.MESSAGING.DTOs.Responses;

namespace PRIVATE.MESSAGING.Services;

public class MessageService : IMessageService
{
    private readonly IMongoCollection<ChatMessage> _messages;

    public MessageService(IMongoDatabase database)
    {
        _messages = database.GetCollection<ChatMessage>("ChatMessages");
    }

    public async Task<PagedResponse<ChatMessage>> GetHistoryAsync(string myNickname, string contactNickname, int page, int limit)
    {
        var filter = Builders<ChatMessage>.Filter.And(
            Builders<ChatMessage>.Filter.Or(
                Builders<ChatMessage>.Filter.And(
                    Builders<ChatMessage>.Filter.Eq(x => x.SenderNickname, myNickname),
                    Builders<ChatMessage>.Filter.Eq(x => x.ReceiverNickname, contactNickname)
                ),
                Builders<ChatMessage>.Filter.And(
                    Builders<ChatMessage>.Filter.Eq(x => x.SenderNickname, contactNickname),
                    Builders<ChatMessage>.Filter.Eq(x => x.ReceiverNickname, myNickname)
                )
            ),
            Builders<ChatMessage>.Filter.Not(
                Builders<ChatMessage>.Filter.AnyEq(x => x.DeletedFor, myNickname)
            )
        );
        
        var totalCount = await _messages.CountDocumentsAsync(filter);
        
        var messages = await _messages.Find(filter)
            .SortByDescending(x => x.Timestamp)
            .Skip((page - 1) * limit)
            .Limit(limit)
            .ToListAsync();
            
        // Reverse to return them in chronological order for the client
        messages.Reverse();

        return new PagedResponse<ChatMessage>
        {
            Items = messages,
            TotalCount = (int)totalCount,
            Page = page,
            Limit = limit
        };
    }

    public async Task ClearHistoryAsync(string myNickname, string contactNickname)
    {
        var filter = Builders<ChatMessage>.Filter.Or(
            Builders<ChatMessage>.Filter.And(
                Builders<ChatMessage>.Filter.Eq(x => x.SenderNickname, myNickname),
                Builders<ChatMessage>.Filter.Eq(x => x.ReceiverNickname, contactNickname)
            ),
            Builders<ChatMessage>.Filter.And(
                Builders<ChatMessage>.Filter.Eq(x => x.SenderNickname, contactNickname),
                Builders<ChatMessage>.Filter.Eq(x => x.ReceiverNickname, myNickname)
            )
        );

        var update = Builders<ChatMessage>.Update.AddToSet(x => x.DeletedFor, myNickname);
        await _messages.UpdateManyAsync(filter, update);

        var cleanupFilter = Builders<ChatMessage>.Filter.And(
            Builders<ChatMessage>.Filter.AnyEq(x => x.DeletedFor, myNickname),
            Builders<ChatMessage>.Filter.AnyEq(x => x.DeletedFor, contactNickname)
        );
        await _messages.DeleteManyAsync(cleanupFilter);
    }
}
