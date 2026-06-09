using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRIVATE.MESSAGING.DTOs.Requests;
using PRIVATE.MESSAGING.Core.Interfaces;
using System.Security.Claims;

namespace PRIVATE.MESSAGING.MW.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UserController : ControllerBase
{
    private readonly IUserService _userService;

    public UserController(IUserService userService)
    {
        _userService = userService;
    }

    [Authorize]
    [HttpPost("profile-picture")]
    public async Task<IActionResult> UpdateProfilePicture([FromBody] UpdateProfilePictureRequest request)
    {
        var nickname = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(nickname)) return Unauthorized();

        var success = await _userService.UpdateProfilePictureAsync(nickname, request.Base64Image);
        
        if (!success) return NotFound(new { message = "User not found or picture already set to this value." });
        return Ok(new { message = "Profile picture updated" });
    }

    [Authorize]
    [HttpGet("{nickname}/profile")]
    public async Task<IActionResult> GetProfile(string nickname)
    {
        var callerNickname = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var profile = await _userService.GetProfileAsync(nickname, callerNickname);

        if (profile == null) return NotFound();
        return Ok(profile);
    }

    [Authorize]
    [HttpGet("contacts")]
    public async Task<IActionResult> GetContacts([FromQuery] string? query = null, [FromQuery] string? cursor = null, [FromQuery] int limit = 50)
    {
        var myNickname = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(myNickname)) return Unauthorized();

        var contacts = await _userService.GetContactsAsync(myNickname, query, cursor, limit);
        return Ok(contacts);
    }

    [Authorize]
    [HttpGet("inbox")]
    public async Task<IActionResult> GetInbox()
    {
        var nickname = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(nickname)) return Unauthorized();

        var inbox = await _userService.GetInboxAsync(nickname);
        return Ok(inbox);
    }

    [Authorize]
    [HttpPost("block/{targetNickname}")]
    public async Task<IActionResult> BlockUser(string targetNickname)
    {
        var myNickname = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(myNickname)) return Unauthorized();

        await _userService.BlockUserAsync(myNickname, targetNickname);
        return Ok(new { message = "User blocked successfully." });
    }

    [Authorize]
    [HttpPost("unblock/{targetNickname}")]
    public async Task<IActionResult> UnblockUser(string targetNickname)
    {
        var myNickname = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(myNickname)) return Unauthorized();

        await _userService.UnblockUserAsync(myNickname, targetNickname);
        return Ok(new { message = "User unblocked successfully." });
    }

    [Authorize]
    [HttpGet("blocked")]
    public async Task<IActionResult> GetBlockedUsers()
    {
        var myNickname = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(myNickname)) return Unauthorized();

        var blockedUsers = await _userService.GetBlockedUsersAsync(myNickname);
        return Ok(blockedUsers);
    }
}
