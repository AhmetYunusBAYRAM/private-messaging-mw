using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRIVATE.MESSAGING.DTOs.Requests;
using PRIVATE.MESSAGING.Core.Interfaces;
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
        var result = await _authService.RegisterAsync(request.Email, request.Nickname, request.PublicKey, request.EncryptedPrivateKey);
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
        var result = await _authService.VerifyOtpAsync(request.Email, request.Otp);
        if (!result.Success) return BadRequest(new { message = result.Message });

        return Ok(new
        {
            token = result.Token,
            refreshToken = result.RefreshToken,
            nickname = result.Nickname,
            encryptedPrivateKey = result.EncryptedPrivateKey
        });
    }

    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        var result = await _authService.RefreshTokenAsync(request.ExpiredToken, request.RefreshToken);
        if (!result.Success) return Unauthorized(new { message = result.Message });

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

        var result = await _authService.ResetKeysAsync(email, request.PublicKey, request.EncryptedPrivateKey);
        if (!result.Success) return BadRequest(new { message = result.Message });

        return Ok(new { message = result.Message });
    }

    [Authorize]
    [HttpGet("publickey/{nickname}")]
    public async Task<IActionResult> GetPublicKey(string nickname)
    {
        var publicKey = await _authService.GetPublicKeyAsync(nickname);
        if (publicKey == null) return NotFound();
        return Ok(new { publicKey });
    }
}