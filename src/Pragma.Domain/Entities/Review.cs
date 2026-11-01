using Pragma.Domain.Common;

namespace Pragma.Domain.Entities;

/// <summary>
/// Qeydiyyatdan keçmiş tələbənin kurs üçün yazdığı rəy və reytinq ulduzu (1-5).
/// Qayda: Yalnız kursu ən azı 20% tamamlamış tələbələr rəy yaza bilər.
/// </summary>
public class Review : BaseEntity
{
    public Guid StudentId { get; set; }

    public Guid CourseId { get; set; }
    public Course? Course { get; set; }

    /// <summary>
    /// Ulduz reytinqi (1-dən 5-ə qədər).
    /// </summary>
    public int Rating { get; set; } = 5;

    public string Comment { get; set; } = string.Empty;
}
