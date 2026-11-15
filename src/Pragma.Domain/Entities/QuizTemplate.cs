using Pragma.Domain.Common;

namespace Pragma.Domain.Entities;

/// <summary>
/// Müəllimin hazırlayıb sual bankında saxladığı əsas quiz şablonu.
/// Tələbəyə birbaşa təyin olunmur; QuizAssignment vasitəsilə təyin edilir.
/// </summary>
public class QuizTemplate : BaseEntity
{
    public Guid TeacherId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>
    /// Şablonun standart vaxt limiti (dəqiqə). Təyin edilərkən dəyişdirilə bilər.
    /// </summary>
    public int? DefaultTimeLimitMinutes { get; set; } = 20;

    /// <summary>
    /// Şablonun standart keçid faizi (məs: 70%).
    /// </summary>
    public int DefaultPassingScorePercentage { get; set; } = 70;

    /// <summary>
    /// Hər sual üçün standart bal (məs: 10 bal).
    /// </summary>
    public int DefaultPointsPerQuestion { get; set; } = 10;

    /// <summary>
    /// Maksimum imtahan cəhdi (varsayılan: 3).
    /// </summary>
    public int DefaultMaxAttempts { get; set; } = 3;

    /// <summary>
    /// Əlaqəli fənn və ya kurs ID-si (istəyə bağlı təsnifat).
    /// </summary>
    public Guid? CourseId { get; set; }
    public Course? Course { get; set; }

    /// <summary>
    /// Sualların və cavab variantlarının strukturlaşdırılmış JSON saxlanması.
    /// </summary>
    public string QuestionsJson { get; set; } = "[]";

    /// <summary>
    /// Bu şablondan törədilmiş aktiv təyinatlar.
    /// </summary>
    public ICollection<QuizAssignment> Assignments { get; set; } = new List<QuizAssignment>();
}
