using Pragma.Domain.Common;

namespace Pragma.Domain.Entities;

public class AuthChallenge : BaseEntity
{
    public string EmailNormalized { get; set; } = string.Empty;
    public string CodeHash { get; set; } = string.Empty;
    public DateTime ExpiresUtc { get; set; }
    public int Attempts { get; set; }
    public DateTime? ConsumedUtc { get; set; }
    public string? RequestIpHash { get; set; }
}
