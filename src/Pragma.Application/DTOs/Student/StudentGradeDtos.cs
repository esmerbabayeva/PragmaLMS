using Pragma.Application.DTOs.Cohorts;
using Pragma.Domain.Enums;

namespace Pragma.Application.DTOs.Student;

public record StudentEnrolledCohortDto(
    Guid CohortId,
    string CohortName,
    Guid CourseId,
    string CourseTitle,
    CohortStatus Status,
    DateTime EnrolledAtUtc
);

public record StudentGradeComponentDto(
    decimal? QuizAverage,
    int QuizzesCompleted,
    int TotalQuizzes,
    decimal? MiniProjectScore,
    decimal? FinalScore,
    decimal? OfficialFinalScore,
    bool IsProvisional,
    decimal QuizWeight,
    decimal MiniProjectWeight,
    decimal FinalWeight
);

public record StudentAttendanceRecordDto(
    DateOnly Date,
    DayOfWeek DayOfWeek,
    AttendanceStatus Status,
    string? Note
);

public record StudentAttendanceOverviewDto(
    int AbsentCount,
    int LateCount,
    int ExcusedCount,
    int PresentCount,
    int EffectiveAbsences,
    int MaxAbsenceLimit,
    int LateToAbsenceRatio,
    bool HasWarning,
    bool IsDropped,
    IReadOnlyCollection<StudentAttendanceRecordDto> History
);

public record StudentFeedbackNoteDto(
    Guid Id,
    string AuthorTeacherName,
    string Text,
    GradeComponentType? GradeComponent,
    DateTime CreatedAtUtc
);

public record StudentCertificateStatusDto(
    CertificateEligibilityResult EligibilityResult,
    string EligibilityReason,
    Guid? CertificateId,
    string? CertificateCode,
    string? DownloadUrl
);

public record StudentCohortGradeDetailsDto(
    Guid CohortId,
    string CohortName,
    Guid CourseId,
    string CourseTitle,
    CohortStatus CohortStatus,
    StudentGradeComponentDto Grades,
    StudentAttendanceOverviewDto Attendance,
    IReadOnlyCollection<StudentFeedbackNoteDto> FeedbackNotes,
    StudentCertificateStatusDto CertificateStatus,
    DateTime? FinalDeadlineUtc = null,
    string? FinalDeadlineLocal = null,
    bool FinalDeadlineIsEndOfDay = false,
    bool IsFinalDeadlinePassed = false,
    StudentFinalSubmissionDto? FinalSubmission = null
);
