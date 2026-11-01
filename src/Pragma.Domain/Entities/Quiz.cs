using Pragma.Domain.Common;

namespace Pragma.Domain.Entities;

/// <summary>
/// Dərsə bağlanan test/quiz modeli.
/// </summary>
public class Quiz : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    
    /// <summary>
    /// Keçid balı faizlə (məsələn: 70%).
    /// </summary>
    public int PassingScorePercentage { get; set; } = 70;

    /// <summary>
    /// İmtahan üçün ayrılan dəqiqə limiti (boşdursa limitsizdir).
    /// </summary>
    public int? TimeLimitMinutes { get; set; }

    /// <summary>
    /// Maksimum imtahan cəhdi sayı (varsayılan: 3).
    /// </summary>
    public int MaxAttempts { get; set; } = 3;

    // Dərs əlaqəsi (1-ə 1 münasibət)
    public Guid LessonId { get; set; }
    public Lesson? Lesson { get; set; }

    // Suallar və tələbələrin imtahan cəhdləri
    public ICollection<Question> Questions { get; set; } = new List<Question>();
    public ICollection<QuizAttempt> Attempts { get; set; } = new List<QuizAttempt>();
}
