namespace PRIVATE.MESSAGING.MW.Models.Requests;

public class RegisterRequest
{
    public string Nickname { get; set; } = string.Empty;
    public string PublicKey { get; set; } = string.Empty;
}