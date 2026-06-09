using PRIVATE.MESSAGING.Core.Entities;
using PRIVATE.MESSAGING.DTOs.Requests;
using PRIVATE.MESSAGING.DTOs.Responses;

namespace PRIVATE.MESSAGING.Core.Interfaces;

public interface IAuthService
{
    Task<(bool Success, string Message)> RegisterAsync(string email, string nickname, KeyBundleDto keys);
    Task<(bool Success, string Message)> LoginAsync(string email);
    Task<(bool Success, string Message, VerifyOtpResponseDto? Data)> VerifyOtpAsync(string email, string otp, string ipAddress, string deviceName, string deviceId);
    Task<(bool Success, string Message, string Token, string RefreshToken)> RefreshTokenAsync(string expiredToken, string refreshToken);
    Task<(bool Success, string Message)> ResetKeysAsync(string email, KeyBundleDto keys);
    Task<KeyBundleDto?> GetPublicKeyBundleAsync(string nickname);
}
