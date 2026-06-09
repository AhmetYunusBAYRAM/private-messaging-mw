namespace PRIVATE.MESSAGING.MW.Models.Requests;

public class VerifyOtpRequest
{
    public string Email { get; set; } = string.Empty;
    public string Otp { get; set; } = string.Empty;
    public string PublicKey { get; set; } = string.Empty;
}
