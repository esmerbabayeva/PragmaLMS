using Pragma.Application.Common;
using Pragma.Domain.Enums;

namespace Pragma.Application.Contracts;

public record AttendanceEntryDto(
    Guid StudentId,
    AttendanceStatus Status,
    string? Note = null
);

public record BulkAttendanceDto(
    DateOnly Date,
    IReadOnlyCollection<AttendanceEntryDto> Entries
);

public record StudentAttendanceSummary(
    Guid StudentId,
    string FullName,
    string Email,
    int PresentCount,
    int AbsentCount,
    int ExcusedCount,
    int LateCount,
    int EffectiveAbsences,
    int MaxAbsenceLimit,
    bool HasWarning,
    CohortEnrollmentStatus Status
);

public record AttendanceRecordDetailDto(
    Guid RecordId,
    Guid StudentId,
    string StudentFullName,
    AttendanceStatus Status,
    string? Note,
    DateTime RecordedAtUtc,
    bool IsEditableByTeacher
);

public record AttendanceDaySheetDto(
    DateOnly Date,
    DayOfWeek DayOfWeek,
    IReadOnlyCollection<AttendanceRecordDetailDto> Records
);

public interface IAttendanceService
{
    Task<Result<bool>> RecordBulkAttendanceAsync(
        Guid cohortId,
        BulkAttendanceDto dto,
        Guid currentUserId,
        bool isAdmin,
        CancellationToken ct = default);

    Task<Result<IReadOnlyCollection<StudentAttendanceSummary>>> GetCohortAttendanceSummaryAsync(
        Guid cohortId,
        Guid currentUserId,
        bool isAdmin,
        CancellationToken ct = default);

    Task<Result<IReadOnlyCollection<AttendanceDaySheetDto>>> GetCohortAttendanceSheetsAsync(
        Guid cohortId,
        Guid currentUserId,
        bool isAdmin,
        CancellationToken ct = default);

    Task<Result<StudentAttendanceSummary>> GetStudentAttendanceAsync(
        Guid cohortId,
        Guid studentId,
        Guid currentUserId,
        bool isAdmin,
        CancellationToken ct = default);
}
