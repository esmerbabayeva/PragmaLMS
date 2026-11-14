using Pragma.Application.Common;
using Pragma.Domain.Enums;

namespace Pragma.Application.Contracts;

public record StudentAcademicGradeSummary(
    Guid StudentId,
    string StudentFullName,
    string StudentEmail,
    decimal? QuizAverage,
    int QuizzesCompleted,
    int TotalQuizzes,
    decimal? MiniProjectScore,
    decimal? FinalScore,
    decimal OfficialScore,
    bool IsProvisional,
    CohortEnrollmentStatus Status,
    string? MiniProjectNotes = null,
    string? FinalNotes = null
);

public record BulkGradeStudentItemDto(
    Guid StudentId,
    decimal? MiniProjectScore,
    decimal? FinalScore,
    string? MiniProjectNotes = null,
    string? FinalNotes = null
);

public record BulkSubmitGradesDto(
    IReadOnlyCollection<BulkGradeStudentItemDto> Grades
);

public record SubmitGradeDto(
    Guid StudentId,
    GradeComponentType ComponentType,
    decimal Score,
    string? Notes = null
);

public interface IAcademicGradeService
{
    Task<Result<StudentAcademicGradeSummary>> CalculateStudentGradeAsync(
        Guid cohortId,
        Guid studentId,
        CancellationToken ct = default);

    Task<Result<IReadOnlyCollection<StudentAcademicGradeSummary>>> GetCohortGradesAsync(
        Guid cohortId,
        Guid requestingUserId,
        bool isAdmin,
        CancellationToken ct = default);

    Task<Result<bool>> SubmitComponentGradeAsync(
        Guid cohortId,
        Guid studentId,
        GradeComponentType componentType,
        decimal score,
        Guid teacherId,
        bool isAdmin,
        string? notes = null,
        CancellationToken ct = default);

    Task<Result<bool>> SubmitBulkGradesAsync(
        Guid cohortId,
        BulkSubmitGradesDto dto,
        Guid teacherId,
        bool isAdmin,
        CancellationToken ct = default);

    Task<Result<bool>> FinalizeCohortGradesAsync(
        Guid cohortId,
        Guid requestingUserId,
        bool isAdmin,
        CancellationToken ct = default);
}
