using Pragma.Domain.Common;
using Pragma.Domain.Enums;

namespace Pragma.Domain.Entities;

public class AttendanceRecord : BaseEntity
{
    public Guid LessonScheduleId { get; set; }
    public LessonSchedule? LessonSchedule { get; set; }
    public Guid CohortStudentId { get; set; }
    public CohortStudent? CohortStudent { get; set; }
    public AttendanceStatus Status { get; set; } = AttendanceStatus.Present;
    public string? Remarks { get; set; }
    public DateTime MarkedAt { get; set; } = DateTime.UtcNow;
}
