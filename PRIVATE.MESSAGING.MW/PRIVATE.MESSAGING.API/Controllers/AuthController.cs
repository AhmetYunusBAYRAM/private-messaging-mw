using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRIVATE.MESSAGING.DTOs.Requests;
using PRIVATE.MESSAGING.Core.Interfaces;
using PRIVATE.MESSAGING.DTOs.Responses;
using PRIVATE.MESSAGING.MW.Filters;
using System.Security.Claims;

namespace PRIVATE.MESSAGING.MW.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [OtpRateLimit(maxRequests: 3, windowSeconds: 60)]
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var result = await _authService.RegisterAsync(request.Email, request.Nickname, request.Keys);
        if (!result.Success) return BadRequest(new { message = result.Message });

        return Ok(new { message = result.Message });
    }

    [OtpRateLimit(maxRequests: 5, windowSeconds: 60)]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await _authService.LoginAsync(request.Email);
        if (!result.Success) return NotFound(new { message = result.Message });

        return Ok(new { message = result.Message });
    }

    [OtpRateLimit(maxRequests: 5, windowSeconds: 60)]
    [HttpPost("verify-otp")]
    public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest request)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Bilinmiyor";
        var result = await _authService.VerifyOtpAsync(request.Email, request.Otp, ip, request.DeviceName, request.DeviceId);
        if (!result.Success) return BadRequest(new { message = result.Message });

        if (result.Data is VerifyOtpResponseDto dto)
        {
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None,
                Expires = DateTime.UtcNow.AddDays(7)
            };
            Response.Cookies.Append("token", dto.Token, cookieOptions);
            Response.Cookies.Append("refreshToken", dto.RefreshToken, cookieOptions);
            
            // We can still return them for now, but frontend will stop using them from JSON.
        }

        return Ok(result.Data);
    }

    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        var result = await _authService.RefreshTokenAsync(request.ExpiredToken, request.RefreshToken);
        if (!result.Success) return Unauthorized(new { message = result.Message });

        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.None,
            Expires = DateTime.UtcNow.AddDays(7)
        };
        Response.Cookies.Append("token", result.Token, cookieOptions);
        Response.Cookies.Append("refreshToken", result.RefreshToken, cookieOptions);

        return Ok(new
        {
            token = result.Token,
            refreshToken = result.RefreshToken
        });
    }

    [Authorize]
    [HttpPost("reset-keys")]
    public async Task<IActionResult> ResetKeys([FromBody] ResetKeysRequest request)
    {
        var email = User.FindFirst(ClaimTypes.Email)?.Value;
        if (string.IsNullOrEmpty(email)) return Unauthorized();

        var result = await _authService.ResetKeysAsync(email, request.Keys);
        if (!result.Success) return BadRequest(new { message = result.Message });

        return Ok(new { message = result.Message });
    }

    [Authorize]
    [HttpGet("publickey-bundle/{nickname}")]
    public async Task<IActionResult> GetPublicKeyBundle(string nickname)
    {
        var bundle = await _authService.GetPublicKeyBundleAsync(nickname);
        if (bundle == null) return NotFound();
        return Ok(bundle);
    }
}