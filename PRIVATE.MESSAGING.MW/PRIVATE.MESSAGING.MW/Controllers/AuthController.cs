using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using PRIVATE.MESSAGING.MW.Models.Attributes;
using PRIVATE.MESSAGING.MW.Models.Requests;
using PRIVATE.MESSAGING.MW.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace PRIVATE.MESSAGING.MW.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IMongoCollection<User> _users;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _config;

    public AuthController(IMongoDatabase database, IEmailService emailService, IConfiguration config)
    {
        _users = database.GetCollection<User>("Users");
        _emailService = emailService;
        _config = config;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var existingUser = await _users.Find(u => u.Email == request.Email || u.Nickname == request.Nickname).FirstOrDefaultAsync();
        if (existingUser != null)
        {
            return BadRequest(new { message = "Email or Nickname already registered" });
        }

        var otp = new Random().Next(100000, 999999).ToString();
        var user = new User
        {
            Email = request.Email,
            Nickname = request.Nickname,
            PublicKey = request.PublicKey,
            Otp = otp,
            OtpExpiry = DateTime.UtcNow.AddMinutes(5),
            LastSeen = DateTime.UtcNow
        };

        await _users.InsertOneAsync(user);
        
        try 
        {
            await _emailService.SendOtpEmailAsync(user.Email, otp);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SMTP HATA] E-posta gönderilemedi. OTP Kodunuz: {otp}");
            Console.WriteLine($"Hata Detayı: {ex.Message}");
        }

        return Ok(new { message = "Registration successful. Check email (or console) for OTP." });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var user = await _users.Find(u => u.Email == request.Email).FirstOrDefaultAsync();
        if (user == null)
            return NotFound(new { message = "User not found" });

        var otp = new Random().Next(100000, 999999).ToString();
        
        var update = Builders<User>.Update
            .Set(u => u.Otp, otp)
            .Set(u => u.OtpExpiry, DateTime.UtcNow.AddMinutes(5));

        await _users.UpdateOneAsync(u => u.Id == user.Id, update);

        try 
        {
            await _emailService.SendOtpEmailAsync(user.Email, otp);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SMTP HATA] E-posta gönderilemedi. OTP Kodunuz: {otp}");
            Console.WriteLine($"Hata Detayı: {ex.Message}");
        }

        return Ok(new { message = "OTP generated. Check email (or console)." });
    }

    [HttpPost("verify-otp")]
    public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest request)
    {
        var user = await _users.Find(u => u.Email == request.Email).FirstOrDefaultAsync();
        if (user == null)
            return NotFound(new { message = "User not found" });

        if (user.Otp != request.Otp || user.OtpExpiry < DateTime.UtcNow)
        {
            return Unauthorized(new { message = "Invalid or expired OTP" });
        }

        var update = Builders<User>.Update
            .Set(u => u.Otp, null)
            .Set(u => u.OtpExpiry, null)
            .Set(u => u.LastSeen, DateTime.UtcNow);

        // Update Public Key if provided (e.g. login from new device)
        if (!string.IsNullOrEmpty(request.PublicKey))
        {
            update = update.Set(u => u.PublicKey, request.PublicKey);
            user.PublicKey = request.PublicKey;
        }

        await _users.UpdateOneAsync(u => u.Id == user.Id, update);

        // Generate JWT Token
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(_config["Jwt:Key"]);
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Nickname),
                new Claim(ClaimTypes.Email, user.Email)
            }),
            Expires = DateTime.UtcNow.AddDays(7),
            Issuer = _config["Jwt:Issuer"],
            Audience = _config["Jwt:Audience"],
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };
        
        var token = tokenHandler.CreateToken(tokenDescriptor);
        var jwtString = tokenHandler.WriteToken(token);

        return Ok(new { 
            message = "Login successful", 
            token = jwtString,
            nickname = user.Nickname, 
            publicKey = user.PublicKey 
        });
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