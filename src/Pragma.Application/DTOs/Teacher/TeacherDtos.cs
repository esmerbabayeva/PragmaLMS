namespace Pragma.Application.DTOs.Teacher;

/// <summary>
/// Müəllimə təyin olunmuş qrup (və ya kurs) haqqında ümumi məlumat.
/// Dərs saatları: neçə saat keçilib, neçə saat qalıb.
/// </summary>
public record AssignedGroupSummaryDto(
    Guid Id,
    string Title,
    string CategoryName,
    int TotalStudents,
    int TotalLessons,
    double TotalHours,
    double CompletedHours,
    double RemainingHours,
    double AverageGrade,
    Guid? CohortId = null,
    Guid? CourseId = null,
    DateTime? FinalDeadlineUtc = null,
    string? FinalDeadlineLocal = null,
    bool FinalDeadlineIsEndOfDay = false,
    string? Status = null
);

/// <summary>
/// Qrupdakı tələbənin cari akademik göstəriciləri, balları və davamiyyəti.
/// </summary>
public record GroupStudentDto(
    Guid StudentId,
    string FullName,
    string Email,
    DateTime EnrolledAtUtc,
    double ProgressPercentage,
    double QuizAverage,
    decimal? MiniProjectGrade,
    decimal? FinalGrade,
    int AbsentCount,
    int LateCount,
    int TotalAbsences,
    decimal? FinalScore,
    bool IsProvisional,
    string? TeacherFeedback,
    string Status,
    string? FinalSubmissionUrl = null,
    DateTime? FinalSubmittedAtUtc = null
);

/// <summary>
/// Tələbəyə bal vermək və ya davamiyyət qeyd etmək üçün sorğu modeli.
/// </summary>
public record AssignGradeDto(
    Guid GroupId,
    Guid StudentId,
    decimal? MiniProjectGrade,
    decimal? FinalGrade,
    int? AbsentCount,
    int? LateCount,
    string? TeacherFeedback
);

/// <summary>
/// Müəllimin qrup/kurs daxilindəki kvizlər siyahısı.
/// </summary>
public record TeacherQuizSummaryDto(
    Guid Id,
    Guid LessonId,
    string LessonTitle,
    string Title,
    string? Description,
    int PassingScorePercentage,
    int? TimeLimitMinutes,
    int MaxAttempts,
    int TotalQuestions
);
