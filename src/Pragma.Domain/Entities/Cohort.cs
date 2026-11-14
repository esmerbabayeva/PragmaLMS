using Pragma.Domain.Common;
using Pragma.Domain.Enums;

namespace Pragma.Domain.Entities;

/// <summary>
/// Kurs üzrə tədris axını / qrup (Cohort əsaslı tədris modeli).
/// </summary>
public class Cohort : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public Guid CourseId { get; set; }
    public Course? Course { get; set; }

    /// <summary>
    /// Qrupun rəsmi təyin olunmuş müəllimi (TeacherId).
    /// </summary>
    public Guid TeacherId { get; set; }

    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// Proqram üzrə cəmi planlaşdırılan saatlar (default 80 saat).
    /// </summary>
    public double TotalHours { get; set; } = 80.0;

    /// <summary>
    /// Keçilmiş tədris saatları.
    /// </summary>
    public double CompletedHours { get; set; } = 0.0;

    /// <summary>
    /// Quiz çəkisi (default: 30%).
    /// </summary>
    public decimal QuizWeight { get; set; } = 30m;

    /// <summary>
    /// Mini layihə çəkisi (default: 30%).
    /// </summary>
    public decimal MiniProjectWeight { get; set; } = 30m;

    /// <summary>
    /// Final layihə / imtahan çəkisi (default: 40%).
    /// </summary>
    public decimal FinalWeight { get; set; } = 40m;

    /// <summary>
    /// Maksimum qayıb limiti (default: 6).
    /// </summary>
    public int MaxAbsenceLimit { get; set; } = 6;

    /// <summary>
    /// Gecikmənin qayıba nisbəti: hər X gecikmə 1 qayıb sayılır (default: 3).
    /// </summary>
    public int LateToAbsenceRatio { get; set; } = 3;

    public CohortStatus Status { get; set; } = CohortStatus.Upcoming;

    // Naviqasiya
    public ICollection<CohortSchedule> Schedules { get; set; } = new List<CohortSchedule>();
    public ICollection<CohortEnrollment> Enrollments { get; set; } = new List<CohortEnrollment>();
    public ICollection<AttendanceRecord> AttendanceRecords { get; set; } = new List<AttendanceRecord>();
    public ICollection<CohortGradeEntry> GradeEntries { get; set; } = new List<CohortGradeEntry>();
    public ICollection<CohortNote> Notes { get; set; } = new List<CohortNote>();
}
