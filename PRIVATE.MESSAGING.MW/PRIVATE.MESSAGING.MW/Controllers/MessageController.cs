using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using PRIVATE.MESSAGING.MW.Models;

namespace PRIVATE.MESSAGING.MW.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MessageController : ControllerBase
{
    private readonly IMongoCollection<ChatMessage> _messages;

    public MessageController(IMongoDatabase database)
    {
        _messages = database.GetCollection<ChatMessage>("ChatMessages");
    }

    [HttpGet("history/{nickname}")]
    public async Task<IActionResult> GetHistory(string nickname)
    {
        // Get all messages where the user is sender or receiver
        var filter = Builders<ChatMessage>.Filter.Or(
            Builders<ChatMessage>.Filter.Eq(x => x.SenderNickname, nickname),
            Builders<ChatMessage>.Filter.Eq(x => x.ReceiverNickname, nickname)
        );
        
        var messages = await _messages.Find(filter).SortBy(x => x.Timestamp).ToListAsync();
        return Ok(messages);
    }
}
