using Pragma.Domain.Common;
using Pragma.Domain.Enums;

namespace Pragma.Domain.Entities;

/// <summary>
/// Tələbənin Cohort qrupuna qeydiyyatı və akademik nəticələri.
/// </summary>
public class CohortEnrollment : BaseEntity
{
    public Guid CohortId { get; set; }
    public Cohort? Cohort { get; set; }

    public Guid StudentId { get; set; }

    public CohortEnrollmentStatus Status { get; set; } = CohortEnrollmentStatus.Enrolled;

    /// <summary>
    /// Rəsmi hesablanmış və yuvarlaqlaşdırılmış yekun bal (1 onluq, MidpointRounding.AwayFromZero).
    /// </summary>
    public decimal? OfficialFinalScore { get; set; }

    /// <summary>
    /// Bütün komponentlər hələ qiymətləndirilməyibsə və ya kohort bitməyibsə müvəqqəti baldır (IsProvisional=true).
    /// </summary>
    public bool IsProvisional { get; set; } = true;

    /// <summary>
    /// Qayıb sayı limitə 2 vahid qalanda (default 4-cü qayıb) avtomatik xəbərdarlıq flag-ı verilir.
    /// </summary>
    public bool HasAbsenceWarning { get; set; } = false;

    public DateTime EnrolledAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; set; }

    public DateTime? DroppedAtUtc { get; set; }
    public string? DroppedReason { get; set; }

    public string? TeacherFeedback { get; set; }
}
