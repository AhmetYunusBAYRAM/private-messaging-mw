using PRIVATE.MESSAGING.Core.Entities;

namespace PRIVATE.MESSAGING.Core.Interfaces;

public interface IAuthService
{
    Task<(bool Success, string Message)> RegisterAsync(string email, string nickname, string publicKey, string encryptedPrivateKey);
    Task<(bool Success, string Message)> LoginAsync(string email);
    Task<(bool Success, string Message, string Token, string Nickname, string EncryptedPrivateKey)> VerifyOtpAsync(string email, string otp);
    Task<(bool Success, string Message)> ResetKeysAsync(string email, string newPublicKey, string newEncryptedPrivateKey);
    Task<string?> GetPublicKeyAsync(string nickname);
}
