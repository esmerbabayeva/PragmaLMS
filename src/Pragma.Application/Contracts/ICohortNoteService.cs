using Pragma.Application.Common;
using Pragma.Application.DTOs.Cohorts;

namespace Pragma.Application.Contracts;

public interface ICohortNoteService
{
    Task<Result<CohortNoteDto>> CreateNoteAsync(
        Guid cohortId,
        Guid studentId,
        CreateCohortNoteDto dto,
        Guid currentUserId,
        bool isAdmin,
        CancellationToken ct = default);

    Task<Result<CohortNoteDto>> UpdateNoteAsync(
        Guid cohortId,
        Guid noteId,
        UpdateCohortNoteDto dto,
        Guid currentUserId,
        bool isAdmin,
        CancellationToken ct = default);

    Task<Result<bool>> DeleteNoteAsync(
        Guid cohortId,
        Guid noteId,
        Guid currentUserId,
        bool isAdmin,
        CancellationToken ct = default);

    Task<Result<IReadOnlyCollection<CohortNoteDto>>> GetNotesForStudentAsync(
        Guid cohortId,
        Guid studentId,
        Guid currentUserId,
        bool isAdmin,
        CancellationToken ct = default);

    Task<Result<IReadOnlyCollection<CohortNoteDto>>> GetNotesForCohortAsync(
        Guid cohortId,
        Guid currentUserId,
        bool isAdmin,
        CancellationToken ct = default);
}
