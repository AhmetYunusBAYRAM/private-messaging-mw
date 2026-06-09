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

    [Authorize]
    [HttpGet("{nickname}/profile")]
    public async Task<IActionResult> GetProfile(string nickname)
    {
        var targetUser = await _users.Find(u => u.Nickname == nickname).FirstOrDefaultAsync();
        if (targetUser == null) return NotFound();

        var callerNickname = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        // Hide sensitive profile data if target user has blocked the caller
        if (callerNickname != null && targetUser.BlockedUsers.Any(b => b.Nickname == callerNickname))
        {
            return Ok(new { 
                nickname = targetUser.Nickname, 
                publicKey = targetUser.PublicKey, 
                profilePictureBase64 = "data:image/gif;base64,R0lGODlhAQABAAD/ACwAAAAAAQABAAACADs=",
                lastSeen = (DateTime?)null,
                isOnline = false
            });
        }

        return Ok(new { 
            nickname = targetUser.Nickname, 
            publicKey = targetUser.PublicKey, 
            profilePictureBase64 = targetUser.ProfilePictureBase64,
            lastSeen = targetUser.LastSeen,
            isOnline = targetUser.IsOnline
        });
    }

    [Authorize]
    [HttpGet("contacts")]
    public async Task<IActionResult> GetContacts()
    {
        var myNickname = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(myNickname)) return Unauthorized();

        var currentUser = await _users.Find(u => u.Nickname == myNickname).FirstOrDefaultAsync();
        if (currentUser == null) return Unauthorized();

        // Get all other users
        var users = await _users.Find(u => u.Nickname != myNickname).ToListAsync();
        
        var result = users.Select(u => new 
        {
            nickname = u.Nickname,
            profilePictureBase64 = u.BlockedUsers.Any(b => b.Nickname == myNickname) ? "data:image/gif;base64,R0lGODlhAQABAAD/ACwAAAAAAQABAAACADs=" : u.ProfilePictureBase64,
            lastSeen = u.BlockedUsers.Any(b => b.Nickname == myNickname) ? (DateTime?)null : u.LastSeen
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
                LastMessage = g.First(), // Since it's sorted descending, First() is the latest message
                UnreadCount = g.Count(m => m.ReceiverNickname == nickname && !m.IsRead)
            }).ToList();

        // Attach profile pictures of the contacts
        var contactNicknames = inbox.Select(i => i.ContactNickname).ToList();
        var contacts = await _users.Find(u => contactNicknames.Contains(u.Nickname)).ToListAsync();

        var result = inbox.Select(i => 
        {
            var contactUser = contacts.FirstOrDefault(c => c.Nickname == i.ContactNickname);
            bool iAmBlocked = contactUser != null && contactUser.BlockedUsers.Any(b => b.Nickname == nickname);

            return new 
            {
                ContactNickname = i.ContactNickname,
                ContactProfilePicture = iAmBlocked ? "data:image/gif;base64,R0lGODlhAQABAAD/ACwAAAAAAQABAAACADs=" : contactUser?.ProfilePictureBase64,
                LastMessage = i.LastMessage,
                UnreadCount = i.UnreadCount
            };
        });

        return Ok(result);
    }

    [Authorize]
    [HttpGet("sessions")]
    public async Task<IActionResult> GetSessions()
    {
        var nickname = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(nickname)) return Unauthorized();

        var user = await _users.Find(u => u.Nickname == nickname).FirstOrDefaultAsync();
        if (user == null) return Unauthorized();

        return Ok(user.DeviceLogs ?? new List<DeviceLog>());
    }

    [Authorize]
    [HttpPost("block/{targetNickname}")]
    public async Task<IActionResult> BlockUser(string targetNickname)
    {
        var myNickname = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(myNickname)) return Unauthorized();

        var user = await _users.Find(u => u.Nickname == myNickname).FirstOrDefaultAsync();
        if (user == null) return Unauthorized();

        if (!user.BlockedUsers.Any(b => b.Nickname == targetNickname))
        {
            var blockedInfo = new BlockedUserInfo { Nickname = targetNickname, BlockedAt = DateTime.UtcNow };
            var update = Builders<User>.Update.Push(u => u.BlockedUsers, blockedInfo);
            await _users.UpdateOneAsync(u => u.Nickname == myNickname, update);
            return Ok(new { message = $"{targetNickname} has been blocked." });
        }

        return Ok(new { message = "User already blocked." });
    }

    [Authorize]
    [HttpPost("unblock/{targetNickname}")]
    public async Task<IActionResult> UnblockUser(string targetNickname)
    {
        var myNickname = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(myNickname)) return Unauthorized();

        // Pull where BlockedUserInfo.Nickname == targetNickname
        var update = Builders<User>.Update.PullFilter(u => u.BlockedUsers, b => b.Nickname == targetNickname);
        var result = await _users.UpdateOneAsync(u => u.Nickname == myNickname, update);

        if (result.ModifiedCount == 0) return Ok(new { message = "User was not blocked or not found." });
        
        return Ok(new { message = $"{targetNickname} has been unblocked." });
    }

    [Authorize]
    [HttpGet("blocked")]
    public async Task<IActionResult> GetBlockedUsers()
    {
        var myNickname = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(myNickname)) return Unauthorized();

        var user = await _users.Find(u => u.Nickname == myNickname).FirstOrDefaultAsync();
        if (user == null) return Unauthorized();

        // Sadece nicknameleri listele ki frontend ayni kalsin
        return Ok(user.BlockedUsers.Select(b => b.Nickname).ToList());
    }
}
