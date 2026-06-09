using System;

namespace PRIVATE.MESSAGING.Core.Entities;

public class RefreshTokenInfo
{
    public string Token { get; set; } = string.Empty;
    public DateTime Expiry { get; set; }
    public DateTime Created { get; set; } = DateTime.UtcNow;
    public bool IsRevoked { get; set; } = false;
    public string? ReplacedByToken { get; set; }

    public bool IsExpired => DateTime.UtcNow >= Expiry;
    public bool IsActive => !IsRevoked && !IsExpired;
}
