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

    /// <summary>
    /// Mini layihə balı (0.00 - 100.00).
    /// </summary>
    public decimal? MiniProjectGrade { get; set; }

    /// <summary>
    /// Final layihə / imtahan balı (0.00 - 100.00).
    /// </summary>
    public decimal? FinalGrade { get; set; }

    /// <summary>
    /// Qayıb sayı (Absent dərslər).
    /// </summary>
    public int AbsentCount { get; set; } = 0;

    /// <summary>
    /// Gecikmə sayı (Late dərslər - hər 3 gecikmə 1 qayıb sayılır).
    /// </summary>
    public int LateCount { get; set; } = 0;

    /// <summary>
    /// Müəllimin tələbə haqqında rəyi və ya qiymətə şərhi.
    /// </summary>
    public string? TeacherFeedback { get; set; }

    /// <summary>
    /// Son qiymətləndirmə tarixi (UTC).
    /// </summary>
    public DateTime? GradedAtUtc { get; set; }
}
