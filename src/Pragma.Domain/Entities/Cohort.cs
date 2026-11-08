using Pragma.Domain.Common;

namespace Pragma.Domain.Entities;

public class Cohort : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public Guid CourseId { get; set; }
    public Course? Course { get; set; }
    public Guid InstructorId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int MaxAbsenceLimit { get; set; } = 6;
    public bool IsActive { get; set; } = true;

    public ICollection<CohortStudent> Students { get; set; } = new List<CohortStudent>();
    public ICollection<LessonSchedule> Schedules { get; set; } = new List<LessonSchedule>();
}
