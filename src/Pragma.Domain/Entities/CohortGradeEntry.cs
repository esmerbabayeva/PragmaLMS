using Pragma.Domain.Common;
using Pragma.Domain.Enums;

namespace Pragma.Domain.Entities;

/// <summary>
/// Müəllim tərəfindən daxil edilən komponent balı (MiniProject, Final) və ya qeydə alınan akademik bal.
/// </summary>
public class CohortGradeEntry : BaseEntity
{
    public Guid CohortId { get; set; }
    public Cohort? Cohort { get; set; }

    public Guid StudentId { get; set; }

    public GradeComponentType ComponentType { get; set; }

    /// <summary>
    /// Qiymət balı (0.00 - 100.00).
    /// </summary>
    public decimal Score { get; set; }

    public Guid GradedByTeacherId { get; set; }
    public DateTime GradedAtUtc { get; set; } = DateTime.UtcNow;

    public string? Notes { get; set; }
}
