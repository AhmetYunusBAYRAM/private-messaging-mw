namespace PRIVATE.MESSAGING.DTOs.Requests;

public class RefreshTokenRequest
{
    public string ExpiredToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
}
