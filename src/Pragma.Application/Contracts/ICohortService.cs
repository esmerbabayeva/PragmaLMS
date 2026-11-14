using Pragma.Application.Common;
using Pragma.Application.DTOs.Cohorts;
using Pragma.Domain.Enums;

namespace Pragma.Application.Contracts;

public interface ICohortService
{
    Task<Result<Guid>> CreateCohortAsync(
        CreateCohortDto dto,
        Guid currentUserId,
        bool isAdmin,
        CancellationToken ct = default);

    Task<Result<CohortDetailDto>> GetCohortByIdAsync(
        Guid id,
        Guid currentUserId,
        bool isAdmin,
        CancellationToken ct = default);

    Task<Result<PagedResult<CohortSummaryDto>>> GetCohortsAsync(
        Guid? courseId,
        Guid? teacherId,
        CohortStatus? status,
        int page,
        int pageSize,
        Guid currentUserId,
        bool isAdmin,
        CancellationToken ct = default);

    Task<Result<bool>> UpdateCohortAsync(
        Guid id,
        UpdateCohortDto dto,
        Guid currentUserId,
        bool isAdmin,
        CancellationToken ct = default);

    Task<Result<bool>> DeleteCohortAsync(
        Guid id,
        Guid currentUserId,
        bool isAdmin,
        CancellationToken ct = default);

    Task<Result<Guid>> AddScheduleAsync(
        Guid cohortId,
        CreateCohortScheduleDto dto,
        Guid currentUserId,
        bool isAdmin,
        CancellationToken ct = default);

    Task<Result<bool>> DeleteScheduleAsync(
        Guid cohortId,
        Guid scheduleId,
        Guid currentUserId,
        bool isAdmin,
        CancellationToken ct = default);

    Task<Result<bool>> EnrollStudentAsync(
        Guid cohortId,
        Guid studentId,
        Guid currentUserId,
        bool isAdmin,
        CancellationToken ct = default);

    Task<Result<bool>> RemoveStudentAsync(
        Guid cohortId,
        Guid studentId,
        Guid currentUserId,
        bool isAdmin,
        CancellationToken ct = default);

    Task<Result<IReadOnlyCollection<CohortStudentDto>>> GetCohortStudentsAsync(
        Guid cohortId,
        Guid currentUserId,
        bool isAdmin,
        CancellationToken ct = default);

    Task<Result<FinishCohortResultDto>> FinishCohortAsync(
        Guid cohortId,
        Guid currentUserId,
        bool isAdmin,
        CancellationToken ct = default);
}
