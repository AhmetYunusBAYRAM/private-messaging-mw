namespace PRIVATE.MESSAGING.DTOs.Responses;

public class VerifyOtpResponseDto
{
    public string Token { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public string Nickname { get; set; } = string.Empty;
    public string EncryptedIdentityPrivateKey { get; set; } = string.Empty;
    public string EncryptedSignedPrePrivateKey { get; set; } = string.Empty;
}
