using Pragma.Domain.Common;

namespace Pragma.Domain.Entities;

public class RefreshToken : BaseEntity
{
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public Guid FamilyId { get; set; }
    public DateTime ExpiresUtc { get; set; }
    public DateTime? RevokedUtc { get; set; }
    public string? ReplacedByTokenHash { get; set; }
    public string? CreatedByIp { get; set; }
    public string? UserAgent { get; set; }
}
