using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using PRIVATE.MESSAGING.MW.Models;
using System.Security.Claims;

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

    [Authorize]
    [HttpGet("history/{contactNickname}")]
    public async Task<IActionResult> GetHistory(string contactNickname)
    {
        var myNickname = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(myNickname)) return Unauthorized();

        // Get all messages where the logged-in user is chatting with the specified contact
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
            // SADECE BENİM SİLMEDİĞİM MESAJLARI GETİR
            Builders<ChatMessage>.Filter.Not(
                Builders<ChatMessage>.Filter.AnyEq(x => x.DeletedFor, myNickname)
            )
        );
        
        var messages = await _messages.Find(filter).SortBy(x => x.Timestamp).ToListAsync();
        return Ok(messages);
    }

    [Authorize]
    [HttpDelete("history/{contactNickname}")]
    public async Task<IActionResult> ClearHistory(string contactNickname)
    {
        var myNickname = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(myNickname)) return Unauthorized();

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

        // Mesajların DeletedFor dizisine benim adımı ekle
        var update = Builders<ChatMessage>.Update.AddToSet(x => x.DeletedFor, myNickname);
        await _messages.UpdateManyAsync(filter, update);

        // İki tarafın da sildiği mesajları fiziksel olarak silerek temizlik yap
        var cleanupFilter = Builders<ChatMessage>.Filter.And(
            Builders<ChatMessage>.Filter.AnyEq(x => x.DeletedFor, myNickname),
            Builders<ChatMessage>.Filter.AnyEq(x => x.DeletedFor, contactNickname)
        );
        await _messages.DeleteManyAsync(cleanupFilter);

        return Ok(new { message = "Sohbet başarıyla temizlendi." });
    }
}
