using Pragma.Domain.Common;
using Pragma.Domain.Enums;

namespace Pragma.Domain.Entities;

/// <summary>
/// Kohort daxilində tələbə haqqında müəllim və ya admin şərhi / qeydi.
/// İki növü var: Feedback (tələbəyə görünən) və Private (yalnız müəllim və adminə görünən).
/// </summary>
public class CohortNote : BaseEntity
{
    public Guid CohortId { get; set; }
    public Cohort? Cohort { get; set; }

    public Guid StudentId { get; set; }
    public Guid AuthorTeacherId { get; set; }

    public CohortNoteType Type { get; set; } = CohortNoteType.Feedback;
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Şərhin aid olduğu qiymət komponenti (MiniProject, Final və ya Ümumi olduqda null).
    /// </summary>
    public GradeComponentType? GradeComponent { get; set; }
}
