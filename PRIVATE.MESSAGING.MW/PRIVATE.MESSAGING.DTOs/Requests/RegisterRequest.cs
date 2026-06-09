namespace PRIVATE.MESSAGING.DTOs.Requests;

public class RegisterRequest
{
    public string Email { get; set; } = string.Empty;
    public string Nickname { get; set; } = string.Empty;
    public KeyBundleDto Keys { get; set; } = new();
}