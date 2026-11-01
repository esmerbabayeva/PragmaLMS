using Pragma.Domain.Common;

namespace Pragma.Domain.Entities;

/// <summary>
/// Fərdi dərsin izlənmə vəziyyəti və son baxılan video mövqeyi.
/// 90% izlənmə qaydası serverdə bu model üzərindən yoxlanılır.
/// </summary>
public class LessonProgress : BaseEntity
{
    public Guid StudentId { get; set; }

    public Guid LessonId { get; set; }
    public Lesson? Lesson { get; set; }

    public bool IsCompleted { get; set; } = false;

    /// <summary>
    /// Tələbənin videonu ən son tərk etdiyi saniyə (dərsə qayıtdıqda buradan başlayır).
    /// </summary>
    public int LastPositionSeconds { get; set; } = 0;

    /// <summary>
    /// Tələbənin bu dərsdə irəlilədiyi ən uzaq saniyə (avto-tamamlama üçün).
    /// </summary>
    public int MaxPositionSeconds { get; set; } = 0;

    public DateTime? CompletedAtUtc { get; set; }
}
