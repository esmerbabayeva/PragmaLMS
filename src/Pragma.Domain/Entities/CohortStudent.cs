using Pragma.Domain.Common;
using Pragma.Domain.Enums;

namespace Pragma.Domain.Entities;

public class CohortStudent : BaseEntity
{
    public Guid CohortId { get; set; }
    public Cohort? Cohort { get; set; }
    public Guid StudentId { get; set; }
    public CohortStudentStatus Status { get; set; } = CohortStudentStatus.Active;
    public DateTime EnrolledAt { get; set; } = DateTime.UtcNow;
    public int TotalAbsences { get; set; }
    public double FinalGradePercent { get; set; }

    public ICollection<AttendanceRecord> AttendanceRecords { get; set; } = new List<AttendanceRecord>();
    public ICollection<StudentAssessment> Assessments { get; set; } = new List<StudentAssessment>();
}
