using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRIVATE.MESSAGING.Core.Interfaces;
using System.Security.Claims;

namespace PRIVATE.MESSAGING.MW.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MessageController : ControllerBase
{
    private readonly IMessageService _messageService;

    public MessageController(IMessageService messageService)
    {
        _messageService = messageService;
    }

    [Authorize]
    [HttpGet("history/{contactNickname}")]
    public async Task<IActionResult> GetHistory(string contactNickname)
    {
        var myNickname = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(myNickname)) return Unauthorized();

        var messages = await _messageService.GetHistoryAsync(myNickname, contactNickname);
        return Ok(messages);
    }

    [Authorize]
    [HttpDelete("history/{contactNickname}")]
    public async Task<IActionResult> ClearHistory(string contactNickname)
    {
        var myNickname = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(myNickname)) return Unauthorized();

        await _messageService.ClearHistoryAsync(myNickname, contactNickname);
        return Ok(new { message = "Sohbet başarıyla temizlendi." });
    }
}
