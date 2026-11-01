using Pragma.Domain.Common;

namespace Pragma.Domain.Entities;

/// <summary>
/// Kurs daxilindəki tədris modulu (fəsil / bölmə).
/// SortableJS ilə sürüşdür-burax (drag-drop) sıralama dəstəkləyir.
/// </summary>
public class Module : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int OrderIndex { get; set; } = 0;

    // Kurs əlaqəsi
    public Guid CourseId { get; set; }
    public Course? Course { get; set; }

    // Dərslər
    public ICollection<Lesson> Lessons { get; set; } = new List<Lesson>();
}
