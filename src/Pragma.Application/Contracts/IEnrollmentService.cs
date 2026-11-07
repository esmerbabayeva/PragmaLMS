using Pragma.Application.Common;
using Pragma.Application.DTOs.Enrollments;

namespace Pragma.Application.Contracts;

public interface IEnrollmentService
{
    Task<Result<EnrollmentDto>> EnrollAsync(Guid courseId, Guid studentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<EnrollmentDto>> GetMyEnrollmentsAsync(Guid studentId, CancellationToken cancellationToken = default);
    Task<bool> IsEnrolledAsync(Guid courseId, Guid studentId, CancellationToken cancellationToken = default);
}
