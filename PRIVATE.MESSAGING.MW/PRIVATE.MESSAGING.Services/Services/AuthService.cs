using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using PRIVATE.MESSAGING.Core.Entities;
using PRIVATE.MESSAGING.Core.Entities.Attributes;
using PRIVATE.MESSAGING.Core.Interfaces;
using PRIVATE.MESSAGING.DTOs.Requests;
using PRIVATE.MESSAGING.DTOs.Responses;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace PRIVATE.MESSAGING.Services;

public class AuthService : IAuthService
{
    private readonly IMongoCollection<User> _users;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _config;

    public AuthService(IMongoDatabase database, IEmailService emailService, IConfiguration config)
    {
        _users = database.GetCollection<User>("Users");
        _emailService = emailService;
        _config = config;
    }

    public async Task<(bool Success, string Message)> RegisterAsync(string email, string nickname, KeyBundleDto keys)
    {
        var existingUser = await _users.Find(u => u.Email == email || u.Nickname == nickname).FirstOrDefaultAsync();
        if (existingUser != null)
        {
            return (false, "Email or Nickname already registered");
        }

        var otp = new Random().Next(100000, 999999).ToString();
        var user = new User
        {
            Email = email,
            Nickname = nickname,
            IdentityPublicKey = keys.IdentityPublicKey,
            EncryptedIdentityPrivateKey = keys.EncryptedIdentityPrivateKey,
            SignedPreKeyPublic = keys.SignedPreKeyPublic,
            EncryptedSignedPrePrivateKey = keys.EncryptedSignedPrePrivateKey,
            SignedPreKeySignature = keys.SignedPreKeySignature,
            OneTimePreKeys = keys.OneTimePreKeys.Select(k => new PreKeyInfo { KeyId = k.KeyId, PublicKey = k.PublicKey }).ToList(),
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

        return (true, "Registration successful. Check email (or console) for OTP.");
    }

    public async Task<(bool Success, string Message)> LoginAsync(string email)
    {
        var user = await _users.Find(u => u.Email == email).FirstOrDefaultAsync();
        if (user == null)
            return (false, "User not found");

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

        return (true, "OTP generated. Check email (or console).");
    }

    public async Task<(bool Success, string Message, VerifyOtpResponseDto? Data)> VerifyOtpAsync(string email, string otp, string ipAddress, string deviceName, string deviceId)
    {
        var user = await _users.Find(u => u.Email == email).FirstOrDefaultAsync();
        if (user == null)
            return (false, "User not found", null);

        if (user.Otp != otp || user.OtpExpiry < DateTime.UtcNow)
            return (false, "Invalid or expired OTP", null);

        var refreshToken = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(64));
        var refreshTokenExpiry = DateTime.UtcNow.AddDays(30);

        var newRefreshTokenInfo = new RefreshTokenInfo
        {
            Token = refreshToken,
            Expiry = refreshTokenExpiry
        };

        var newDeviceLog = new DeviceLog
        {
            DeviceId = string.IsNullOrEmpty(deviceId) ? "Bilinmiyor" : deviceId,
            IpAddress = string.IsNullOrEmpty(ipAddress) ? "Bilinmiyor" : ipAddress,
            DeviceName = string.IsNullOrEmpty(deviceName) ? "Bilinmeyen Cihaz" : deviceName,
            LastActiveAt = DateTime.UtcNow
        };

        var update = Builders<User>.Update
            .Set(u => u.Otp, null)
            .Set(u => u.OtpExpiry, null)
            .Push(u => u.DeviceLogs, newDeviceLog)
            .Push(u => u.RefreshTokens, newRefreshTokenInfo);

        await _users.UpdateOneAsync(u => u.Id == user.Id, update);

        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(_config["Jwt:Key"]);
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[] 
            {
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.NameIdentifier, user.Nickname)
            }),
            Expires = DateTime.UtcNow.AddMinutes(15),
            Issuer = _config["Jwt:Issuer"],
            Audience = _config["Jwt:Audience"],
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };
        var token = tokenHandler.CreateToken(tokenDescriptor);
        var tokenString = tokenHandler.WriteToken(token);

        var responseData = new VerifyOtpResponseDto
        {
            Token = tokenString,
            RefreshToken = refreshToken,
            Nickname = user.Nickname,
            EncryptedIdentityPrivateKey = user.EncryptedIdentityPrivateKey,
            EncryptedSignedPrePrivateKey = user.EncryptedSignedPrePrivateKey
        };

        return (true, "OTP verified", responseData);
    }

    public async Task<(bool Success, string Message)> ResetKeysAsync(string email, KeyBundleDto keys)
    {
        var update = Builders<User>.Update
            .Set(u => u.IdentityPublicKey, keys.IdentityPublicKey)
            .Set(u => u.EncryptedIdentityPrivateKey, keys.EncryptedIdentityPrivateKey)
            .Set(u => u.SignedPreKeyPublic, keys.SignedPreKeyPublic)
            .Set(u => u.EncryptedSignedPrePrivateKey, keys.EncryptedSignedPrePrivateKey)
            .Set(u => u.SignedPreKeySignature, keys.SignedPreKeySignature)
            .Set(u => u.OneTimePreKeys, keys.OneTimePreKeys.Select(k => new PreKeyInfo { KeyId = k.KeyId, PublicKey = k.PublicKey }).ToList());

        var result = await _users.UpdateOneAsync(u => u.Email == email, update);
        
        if (result.ModifiedCount == 0) return (false, "User not found or keys already set to these values.");
        return (true, "Keys resetted successfully.");
    }

    public async Task<(bool Success, string Message, string Token, string RefreshToken)> RefreshTokenAsync(string expiredToken, string refreshToken)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(_config["Jwt:Key"]);

        var tokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = false, // Ignore expiration for refresh
            ValidateIssuerSigningKey = true,
            ValidIssuer = _config["Jwt:Issuer"],
            ValidAudience = _config["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(key)
        };

        try
        {
            var principal = tokenHandler.ValidateToken(expiredToken, tokenValidationParameters, out var securityToken);
            if (securityToken is not JwtSecurityToken jwtSecurityToken ||
                !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
            {
                return (false, "Invalid token", string.Empty, string.Empty);
            }

            var email = principal.FindFirst(ClaimTypes.Email)?.Value;
            if (string.IsNullOrEmpty(email)) return (false, "Invalid token claims", string.Empty, string.Empty);

            var user = await _users.Find(u => u.Email == email).FirstOrDefaultAsync();
            if (user == null) return (false, "User not found", string.Empty, string.Empty);

            var existingToken = user.RefreshTokens.FirstOrDefault(t => t.Token == refreshToken);

            if (existingToken == null)
            {
                return (false, "Invalid refresh token", string.Empty, string.Empty);
            }

            if (existingToken.IsRevoked)
            {
                // Token Theft Detected! Revoke all tokens for this user.
                var revokeAllUpdate = Builders<User>.Update.Set("RefreshTokens.$[].IsRevoked", true);
                await _users.UpdateOneAsync(u => u.Id == user.Id, revokeAllUpdate);
                return (false, "Token theft detected. All sessions revoked.", string.Empty, string.Empty);
            }

            if (existingToken.IsExpired)
            {
                return (false, "Expired refresh token", string.Empty, string.Empty);
            }

            // Generate new tokens
            var newRefreshToken = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(64));
            var newRefreshTokenExpiry = DateTime.UtcNow.AddDays(30);

            var newRefreshTokenInfo = new RefreshTokenInfo
            {
                Token = newRefreshToken,
                Expiry = newRefreshTokenExpiry
            };

            // Revoke the old token and add the new one
            var updateTokens = user.RefreshTokens;
            var tokenIndex = updateTokens.FindIndex(t => t.Token == refreshToken);
            updateTokens[tokenIndex].IsRevoked = true;
            updateTokens[tokenIndex].ReplacedByToken = newRefreshToken;
            updateTokens.Add(newRefreshTokenInfo);

            var update = Builders<User>.Update.Set(u => u.RefreshTokens, updateTokens);
            await _users.UpdateOneAsync(u => u.Id == user.Id, update);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.Email, user.Email),
                    new Claim(ClaimTypes.NameIdentifier, user.Nickname)
                }),
                Expires = DateTime.UtcNow.AddMinutes(15),
                Issuer = _config["Jwt:Issuer"],
                Audience = _config["Jwt:Audience"],
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };
            
            var newToken = tokenHandler.CreateToken(tokenDescriptor);
            var newTokenString = tokenHandler.WriteToken(newToken);

            return (true, "Token refreshed", newTokenString, newRefreshToken);
        }
        catch
        {
            return (false, "Invalid token", string.Empty, string.Empty);
        }
    }

    public async Task<KeyBundleDto?> GetPublicKeyBundleAsync(string nickname)
    {
        var user = await _users.Find(u => u.Nickname == nickname).FirstOrDefaultAsync();
        if (user == null) return null;

        PreKeyDto? oneTimePreKey = null;
        if (user.OneTimePreKeys != null && user.OneTimePreKeys.Count > 0)
        {
            var preKeyInfo = user.OneTimePreKeys.First();
            oneTimePreKey = new PreKeyDto { KeyId = preKeyInfo.KeyId, PublicKey = preKeyInfo.PublicKey };

            // Remove it from DB (One-Time use)
            var update = Builders<User>.Update.PullFilter(u => u.OneTimePreKeys, k => k.KeyId == preKeyInfo.KeyId);
            await _users.UpdateOneAsync(u => u.Id == user.Id, update);
        }

        var bundle = new KeyBundleDto
        {
            IdentityPublicKey = user.IdentityPublicKey,
            SignedPreKeyPublic = user.SignedPreKeyPublic,
            SignedPreKeySignature = user.SignedPreKeySignature
        };

        if (oneTimePreKey != null)
        {
            bundle.OneTimePreKeys.Add(oneTimePreKey);
        }

        return bundle;
    }
}
