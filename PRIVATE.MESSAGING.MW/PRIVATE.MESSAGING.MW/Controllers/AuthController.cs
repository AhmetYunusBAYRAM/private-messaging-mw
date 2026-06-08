using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using PRIVATE.MESSAGING.MW.Models.Attributes;
using PRIVATE.MESSAGING.MW.Models.Requests;

namespace PRIVATE.MESSAGING.MW.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IMongoCollection<User> _users;

    public AuthController(IMongoDatabase database)
    {
        _users = database.GetCollection<User>("Users");
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var existingUser = await _users.Find(u => u.Nickname == request.Nickname).FirstOrDefaultAsync();
        if (existingUser != null)
        {
            existingUser.PublicKey = request.PublicKey;
            existingUser.LastSeen = DateTime.UtcNow;
            await _users.ReplaceOneAsync(u => u.Id == existingUser.Id, existingUser);
            return Ok(existingUser);
        }

        var user = new User
        {
            Nickname = request.Nickname,
            PublicKey = request.PublicKey,
            LastSeen = DateTime.UtcNow
        };

        await _users.InsertOneAsync(user);
        return Ok(user);
    }

    [HttpGet("publickey/{nickname}")]
    public async Task<IActionResult> GetPublicKey(string nickname)
    {
        var user = await _users.Find(u => u.Nickname == nickname).FirstOrDefaultAsync();
        if (user == null)
            return NotFound();

        return Ok(new { publicKey = user.PublicKey });
    }
}