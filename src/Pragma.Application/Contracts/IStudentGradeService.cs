using Pragma.Application.Common;
using Pragma.Application.DTOs.Student;

namespace Pragma.Application.Contracts;

public interface IStudentGradeService
{
    Task<Result<IReadOnlyCollection<StudentEnrolledCohortDto>>> GetMyEnrolledCohortsAsync(
        Guid currentStudentId,
        CancellationToken ct = default);

    Task<Result<StudentCohortGradeDetailsDto>> GetMyCohortGradeDetailsAsync(
        Guid cohortId,
        Guid currentStudentId,
        CancellationToken ct = default);
}
