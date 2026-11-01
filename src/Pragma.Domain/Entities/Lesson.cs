using Pragma.Domain.Common;
using Pragma.Domain.Enums;

namespace Pragma.Domain.Entities;

/// <summary>
/// Modul daxilindəki fərdi dərs vahidi (Video və ya Markdown).
/// </summary>
public class Lesson : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public int OrderIndex { get; set; } = 0;
    public LessonType Type { get; set; } = LessonType.Video;

    /// <summary>
    /// Markdown formatında mətn izahı və ya dərs konspekti.
    /// </summary>
    public string? Content { get; set; }

    /// <summary>
    /// YouTube və ya digər video URL linki.
    /// </summary>
    public string? VideoUrl { get; set; }

    /// <summary>
    /// Videonun saniyə ilə ümumi müddəti.
    /// </summary>
    public int DurationSeconds { get; set; } = 0;

    /// <summary>
    /// Kursa qeydiyyatdan öncə pulsuz önbaxış üçün açıq olub-olmaması.
    /// </summary>
    public bool IsPreview { get; set; } = false;

    // Modul əlaqəsi
    public Guid ModuleId { get; set; }
    public Module? Module { get; set; }

    // Bağlı quiz (hər dərsə ən çox 1 quiz bağlana bilər)
    public Quiz? Quiz { get; set; }

    // Əlavə resurslar, tərəqqi və qeydlər
    public ICollection<LessonResource> Resources { get; set; } = new List<LessonResource>();
    public ICollection<LessonProgress> Progresses { get; set; } = new List<LessonProgress>();
    public ICollection<LessonNote> Notes { get; set; } = new List<LessonNote>();
}
