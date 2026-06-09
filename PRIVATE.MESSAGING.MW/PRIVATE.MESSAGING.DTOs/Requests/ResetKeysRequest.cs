namespace PRIVATE.MESSAGING.DTOs.Requests;

public class ResetKeysRequest
{
    public KeyBundleDto Keys { get; set; } = new();
}
