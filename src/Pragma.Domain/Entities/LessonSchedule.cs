using Pragma.Domain.Common;

namespace Pragma.Domain.Entities;

public class LessonSchedule : BaseEntity
{
    public Guid CohortId { get; set; }
    public Cohort? Cohort { get; set; }
    public Guid? LessonId { get; set; }
    public Lesson? Lesson { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string RoomOrMeetingUrl { get; set; } = string.Empty;
    public bool IsLiveOnline { get; set; } = true;

    public ICollection<AttendanceRecord> AttendanceRecords { get; set; } = new List<AttendanceRecord>();
}
