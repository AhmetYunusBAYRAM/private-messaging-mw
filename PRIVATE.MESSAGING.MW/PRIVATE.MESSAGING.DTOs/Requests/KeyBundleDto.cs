namespace PRIVATE.MESSAGING.DTOs.Requests;

public class KeyBundleDto
{
    public string IdentityPublicKey { get; set; } = string.Empty;
    public string EncryptedIdentityPrivateKey { get; set; } = string.Empty;
    public string SignedPreKeyPublic { get; set; } = string.Empty;
    public string EncryptedSignedPrePrivateKey { get; set; } = string.Empty;
    public string SignedPreKeySignature { get; set; } = string.Empty;
    public List<PreKeyDto> OneTimePreKeys { get; set; } = new();
}

public class PreKeyDto
{
    public string KeyId { get; set; } = string.Empty;
    public string PublicKey { get; set; } = string.Empty;
}
