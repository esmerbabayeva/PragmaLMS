namespace Pragma.Domain.Common;

/// <summary>
/// Bütün domen entity-ləri üçün təməl abstrakt sinif.
/// Vahid identifikasiya, audit tarixləri və soft-delete dəstəyi təmin edir.
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();

    public DateTime CreatedAtUtc { get; protected set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; protected set; }

    public bool IsDeleted { get; protected set; } = false;

    public byte[]? RowVersion { get; set; }

    public void MarkUpdated()
    {
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void MarkDeleted()
    {
        IsDeleted = true;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Restore()
    {
        IsDeleted = false;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
