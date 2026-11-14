using Pragma.Application.Common;
using Pragma.Application.DTOs.Enrollments;

namespace Pragma.Application.Contracts;

public interface IEnrollmentService
{
    Task<Result<EnrollmentDto>> EnrollAsync(Guid courseId, Guid studentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<EnrollmentDto>> GetMyEnrollmentsAsync(Guid studentId, string? language = null, CancellationToken cancellationToken = default);
    Task<bool> IsEnrolledAsync(Guid courseId, Guid studentId, CancellationToken cancellationToken = default);
    Task<Result<EnrollmentDto>> ManageEnrollStudentAsync(Guid courseId, Guid studentId, Guid currentUserId, bool isAdmin, CancellationToken cancellationToken = default);
    Task<Result> ManageUnenrollStudentAsync(Guid courseId, Guid studentId, Guid currentUserId, bool isAdmin, CancellationToken cancellationToken = default);
}
