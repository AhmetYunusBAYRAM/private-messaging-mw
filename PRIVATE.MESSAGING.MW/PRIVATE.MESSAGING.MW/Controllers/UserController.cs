using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using PRIVATE.MESSAGING.MW.Models;
using PRIVATE.MESSAGING.MW.Models.Attributes;
using PRIVATE.MESSAGING.MW.Models.Requests;
using System.Security.Claims;

namespace PRIVATE.MESSAGING.MW.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UserController : ControllerBase
{
    private readonly IMongoCollection<User> _users;
    private readonly IMongoCollection<ChatMessage> _messages;

    public UserController(IMongoDatabase database)
    {
        _users = database.GetCollection<User>("Users");
        _messages = database.GetCollection<ChatMessage>("ChatMessages");
    }

    [Authorize]
    [HttpPost("profile-picture")]
    public async Task<IActionResult> UpdateProfilePicture([FromBody] UpdateProfilePictureRequest request)
    {
        var nickname = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(nickname)) return Unauthorized();

        var update = Builders<User>.Update.Set(u => u.ProfilePictureBase64, request.Base64Image);
        var result = await _users.UpdateOneAsync(u => u.Nickname == nickname, update);
        
        if (result.ModifiedCount == 0) return NotFound(new { message = "User not found or picture already set to this value." });
        return Ok(new { message = "Profile picture updated" });
    }

    // Public endpoint to view others' profiles
    [HttpGet("{nickname}/profile")]
    public async Task<IActionResult> GetProfile(string nickname)
    {
        var user = await _users.Find(u => u.Nickname == nickname).FirstOrDefaultAsync();
        if (user == null) return NotFound();

        return Ok(new { 
            nickname = user.Nickname, 
            publicKey = user.PublicKey, 
            profilePictureBase64 = user.ProfilePictureBase64,
            lastSeen = user.LastSeen
        });
    }

    [Authorize]
    [HttpGet("contacts")]
    public async Task<IActionResult> GetContacts()
    {
        var myNickname = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(myNickname)) return Unauthorized();

        var users = await _users.Find(u => u.Nickname != myNickname).ToListAsync();
        
        var result = users.Select(u => new 
        {
            nickname = u.Nickname,
            profilePictureBase64 = u.ProfilePictureBase64,
            lastSeen = u.LastSeen
        });

        return Ok(result);
    }

    [Authorize]
    [HttpGet("inbox")]
    public async Task<IActionResult> GetInbox()
    {
        var nickname = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(nickname)) return Unauthorized();

        var filter = Builders<ChatMessage>.Filter.Or(
            Builders<ChatMessage>.Filter.Eq(x => x.SenderNickname, nickname),
            Builders<ChatMessage>.Filter.Eq(x => x.ReceiverNickname, nickname)
        );
        
        var allMessages = await _messages.Find(filter).SortByDescending(x => x.Timestamp).ToListAsync();
        
        // Group by the "other" contact in the conversation
        var inbox = allMessages
            .GroupBy(m => m.SenderNickname == nickname ? m.ReceiverNickname : m.SenderNickname)
            .Select(g => new 
            {
                ContactNickname = g.Key,
                LastMessage = g.First() // Since it's sorted descending, First() is the latest message
            }).ToList();

        // Attach profile pictures of the contacts
        var contactNicknames = inbox.Select(i => i.ContactNickname).ToList();
        var contacts = await _users.Find(u => contactNicknames.Contains(u.Nickname)).ToListAsync();

        var result = inbox.Select(i => new 
        {
            ContactNickname = i.ContactNickname,
            ContactProfilePicture = contacts.FirstOrDefault(c => c.Nickname == i.ContactNickname)?.ProfilePictureBase64,
            LastMessage = i.LastMessage
        });

        return Ok(result);
    }
}
