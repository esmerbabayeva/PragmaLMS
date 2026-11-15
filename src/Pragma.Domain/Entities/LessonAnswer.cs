using Pragma.Domain.Common;

namespace Pragma.Domain.Entities;

public class LessonAnswer : BaseEntity
{
    public Guid QuestionId { get; set; }
    public LessonQuestion? Question { get; set; }

    public Guid AuthorId { get; set; }
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Cavab təlimçi və ya müəllim tərəfindən verildikdə true olur (fərqləndirici nişan üçün).
    /// </summary>
    public bool IsInstructorAnswer { get; set; } = false;
}
