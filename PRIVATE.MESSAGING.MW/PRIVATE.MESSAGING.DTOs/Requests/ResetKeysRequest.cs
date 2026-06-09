namespace PRIVATE.MESSAGING.DTOs.Requests;

public class ResetKeysRequest
{
    public string PublicKey { get; set; } = string.Empty;
    public string EncryptedPrivateKey { get; set; } = string.Empty;
}
