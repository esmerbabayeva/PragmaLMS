using Pragma.Domain.Enums;

namespace Pragma.Application.DTOs.Cohorts;

public record CreateCohortDto(
    string Name,
    Guid CourseId,
    Guid TeacherId,
    DateTime StartDate,
    DateTime? EndDate = null,
    double TotalHours = 80.0,
    decimal QuizWeight = 30m,
    decimal MiniProjectWeight = 30m,
    decimal FinalWeight = 40m,
    int MaxAbsenceLimit = 6,
    int LateToAbsenceRatio = 3
);

public record UpdateCohortDto(
    string Name,
    Guid TeacherId,
    DateTime StartDate,
    DateTime? EndDate,
    double TotalHours,
    decimal QuizWeight,
    decimal MiniProjectWeight,
    decimal FinalWeight,
    int MaxAbsenceLimit,
    int LateToAbsenceRatio,
    CohortStatus Status
);

public record CohortScheduleDto(
    Guid Id,
    DayOfWeek DayOfWeek,
    TimeSpan StartTime,
    TimeSpan EndTime,
    string? RoomOrMeetingUrl
);

public record CreateCohortScheduleDto(
    DayOfWeek DayOfWeek,
    TimeSpan StartTime,
    TimeSpan EndTime,
    string? RoomOrMeetingUrl = null
);

public record CohortDetailDto(
    Guid Id,
    string Name,
    Guid CourseId,
    string CourseTitle,
    Guid TeacherId,
    string TeacherFullName,
    DateTime StartDate,
    DateTime? EndDate,
    double TotalHours,
    double CompletedHours,
    decimal QuizWeight,
    decimal MiniProjectWeight,
    decimal FinalWeight,
    int MaxAbsenceLimit,
    int LateToAbsenceRatio,
    CohortStatus Status,
    int EnrolledStudentCount,
    IReadOnlyCollection<CohortScheduleDto> Schedules
);

public record CohortSummaryDto(
    Guid Id,
    string Name,
    Guid CourseId,
    string CourseTitle,
    Guid TeacherId,
    string TeacherFullName,
    CohortStatus Status,
    int EnrolledStudentCount,
    double CompletedHours,
    double TotalHours,
    DateTime StartDate
);

public record EnrollStudentInCohortDto(
    Guid StudentId
);

public record CohortStudentDto(
    Guid StudentId,
    string FullName,
    string Email,
    CohortEnrollmentStatus Status,
    DateTime EnrolledAtUtc,
    decimal? OfficialFinalScore,
    bool IsProvisional,
    bool HasAbsenceWarning,
    int EffectiveAbsences
);

public record FinishCohortResultDto(
    Guid CohortId,
    string CohortName,
    int TotalStudents,
    int CompletedStudents,
    int DroppedStudents,
    int DiplomasIssued,
    int AttendancesIssued
);
