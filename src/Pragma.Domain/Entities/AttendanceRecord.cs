using Pragma.Domain.Common;
using Pragma.Domain.Enums;

namespace Pragma.Domain.Entities;

/// <summary>
/// Kohort dərsi üzrə tələbənin fərdi davamiyyət qeydi.
/// </summary>
public class AttendanceRecord : BaseEntity
{
    public Guid CohortId { get; set; }
    public Cohort? Cohort { get; set; }

    public Guid StudentId { get; set; }

    /// <summary>
    /// Dərsin keçirildiyi gün (Təqvim günü).
    /// </summary>
    public DateOnly Date { get; set; }

    public AttendanceStatus Status { get; set; } = AttendanceStatus.Present;

    public string? Note { get; set; }

    /// <summary>
    /// Davamiyyəti yazan şəxs (Müəllim və ya Admin).
    /// </summary>
    public Guid RecordedByUserId { get; set; }
    public DateTime RecordedAtUtc { get; set; } = DateTime.UtcNow;
}
