namespace Pragma.Application.DTOs.Quizzes;

public record TemplateOptionDto(string Text, bool IsCorrect);

public record TemplateQuestionDto(
    string Text,
    int Points,
    int OrderIndex,
    string? Explanation,
    List<TemplateOptionDto> Options
);

public record CreateQuizTemplateDto(
    string Title,
    string? Description,
    int? DefaultTimeLimitMinutes,
    int DefaultPassingScorePercentage,
    int DefaultPointsPerQuestion,
    int DefaultMaxAttempts,
    Guid? CourseId,
    List<TemplateQuestionDto> Questions
);

public record UpdateQuizTemplateDto(
    string Title,
    string? Description,
    int? DefaultTimeLimitMinutes,
    int DefaultPassingScorePercentage,
    int DefaultPointsPerQuestion,
    int DefaultMaxAttempts,
    Guid? CourseId,
    List<TemplateQuestionDto> Questions
);

public record QuizTemplateDto(
    Guid Id,
    Guid TeacherId,
    string Title,
    string? Description,
    int? DefaultTimeLimitMinutes,
    int DefaultPassingScorePercentage,
    int DefaultPointsPerQuestion,
    int DefaultMaxAttempts,
    Guid? CourseId,
    string? CourseTitle,
    int QuestionCount,
    int TotalPoints,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    List<TemplateQuestionDto> Questions
);

public record QuizTemplateSummaryDto(
    Guid Id,
    Guid TeacherId,
    string Title,
    string? Description,
    int? DefaultTimeLimitMinutes,
    int DefaultPassingScorePercentage,
    int DefaultPointsPerQuestion,
    int DefaultMaxAttempts,
    Guid? CourseId,
    string? CourseTitle,
    int QuestionCount,
    int TotalPoints,
    int ActiveAssignmentsCount,
    DateTime CreatedAtUtc
);
