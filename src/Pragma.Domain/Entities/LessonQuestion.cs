using Pragma.Domain.Common;

namespace Pragma.Domain.Entities;

public class LessonQuestion : BaseEntity
{
    public Guid LessonId { get; set; }
    public Lesson? Lesson { get; set; }

    public Guid StudentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Videonun konkret saniyəsinə istinad (ixtiyari).
    /// </summary>
    public int? PositionSeconds { get; set; }

    public bool IsResolved { get; set; } = false;

    public ICollection<LessonAnswer> Answers { get; set; } = new List<LessonAnswer>();
}
