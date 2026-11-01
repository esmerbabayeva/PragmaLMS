using Pragma.Domain.Common;

namespace Pragma.Domain.Entities;

/// <summary>
/// Tələbənin kursa qeydiyyatı və ümumi kurs tərəqqisi modeli.
/// </summary>
public class Enrollment : BaseEntity
{
    public Guid StudentId { get; set; }

    public Guid CourseId { get; set; }
    public Course? Course { get; set; }

    public DateTime EnrolledAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Kursun tamamlanma faizi (0.0 - 100.0).
    /// Bütün dərslər bitdikdə 100% olur.
    /// </summary>
    public double ProgressPercentage { get; set; } = 0.0;

    public bool IsCompleted { get; set; } = false;
    public DateTime? CompletedAtUtc { get; set; }
}
