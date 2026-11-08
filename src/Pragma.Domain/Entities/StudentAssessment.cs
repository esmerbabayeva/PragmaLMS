using Pragma.Domain.Common;
using Pragma.Domain.Enums;

namespace Pragma.Domain.Entities;

public class StudentAssessment : BaseEntity
{
    public Guid CohortStudentId { get; set; }
    public CohortStudent? CohortStudent { get; set; }
    public AssessmentType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public double Score { get; set; }
    public double MaxScore { get; set; } = 100.0;
    public double WeightPercent { get; set; }
    public string? Feedback { get; set; }
    public Guid GradedByInstructorId { get; set; }
    public DateTime GradedAt { get; set; } = DateTime.UtcNow;
}
