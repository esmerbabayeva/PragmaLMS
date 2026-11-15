namespace Pragma.Application.DTOs.Quizzes;

public record CreateQuizAssignmentDto(
    Guid TemplateId,
    Guid? CohortId,
    Guid? CourseId,
    List<Guid>? StudentIds,
    Guid? LessonId,
    string? CustomTitle,
    string? CustomDescription,
    int? CustomTimeLimitMinutes,
    int? PassingScorePercentage,
    int? PointsPerQuestion,
    int? MaxAttempts,
    DateTime? StartDateUtc,
    DateTime? EndDateUtc
);

public record UpdateQuizAssignmentDto(
    string? CustomTitle,
    string? CustomDescription,
    int? CustomTimeLimitMinutes,
    int? PassingScorePercentage,
    int? PointsPerQuestion,
    int? MaxAttempts,
    DateTime? StartDateUtc,
    DateTime? EndDateUtc,
    bool? IsActive
);

public record QuizAssignmentDto(
    Guid Id,
    Guid TemplateId,
    string TemplateTitle,
    Guid TeacherId,
    Guid? CohortId,
    string? CohortName,
    Guid? CourseId,
    string? CourseTitle,
    Guid? LessonId,
    string? LessonTitle,
    string Title,
    string? Description,
    int? CustomTimeLimitMinutes,
    int PassingScorePercentage,
    int? PointsPerQuestion,
    int MaxAttempts,
    DateTime? StartDateUtc,
    DateTime? EndDateUtc,
    bool IsActive,
    int QuestionCount,
    int TotalPoints,
    int TotalAttemptsCount,
    DateTime CreatedAtUtc
);

public record StudentQuizAssignmentDto(
    Guid Id,
    string Title,
    string? Description,
    string? CourseTitle,
    string? CohortName,
    int? TimeLimitMinutes,
    int PassingScorePercentage,
    int QuestionCount,
    int TotalPoints,
    DateTime? StartDateUtc,
    DateTime? EndDateUtc,
    bool IsAvailable,
    bool IsExpired,
    int AttemptsUsed,
    int MaxAttempts,
    double? BestScorePercentage,
    bool IsPassed
);
