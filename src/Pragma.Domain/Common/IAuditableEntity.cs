namespace Pragma.Domain.Common;

/// <summary>
/// Audit məlumatlarını saxlayan entity-lər üçün interfeys.
/// </summary>
public interface IAuditableEntity
{
    DateTime CreatedAtUtc { get; }
    DateTime? UpdatedAtUtc { get; }
}
