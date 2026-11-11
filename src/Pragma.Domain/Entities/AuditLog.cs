using Pragma.Domain.Common;

namespace Pragma.Domain.Entities;

/// <summary>
/// Sistemdəki kritik inzibati əməliyyatların (rol dəyişməsi, bloklama, silmə, bal düzəlişi) audit qeydləri.
/// </summary>
public class AuditLog : BaseEntity
{
    public Guid? UserId { get; set; }
    public string? UserEmail { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
}
