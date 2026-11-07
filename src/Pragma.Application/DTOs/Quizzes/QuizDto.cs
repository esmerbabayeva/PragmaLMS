using Pragma.Domain.Enums;

namespace Pragma.Application.DTOs.Quizzes;

// 1. Təlimçi üçün tam Quiz DTO (IsCorrect görünür)
public record QuizDetailDto(
    Guid Id,
    Guid LessonId,
    string Title,
    string? Description,
    int PassingScorePercentage,
    int? TimeLimitMinutes,
    int MaxAttempts,
    IReadOnlyCollection<QuestionDetailDto> Questions
);

public record QuestionDetailDto(
    Guid Id,
    string Text,
    int Points,
    int OrderIndex,
    string? Explanation,
    IReadOnlyCollection<AnswerOptionDetailDto> Options
);

public record AnswerOptionDetailDto(
    Guid Id,
    string Text,
    bool IsCorrect
);

public record CreateQuizDto(
    string Title,
    string? Description,
    int PassingScorePercentage,
    int? TimeLimitMinutes,
    int MaxAttempts,
    IReadOnlyCollection<CreateQuestionDto> Questions
);

public record CreateQuestionDto(
    string Text,
    int Points,
    int OrderIndex,
    string? Explanation,
    IReadOnlyCollection<CreateAnswerOptionDto> Options
);

public record CreateAnswerOptionDto(
    string Text,
    bool IsCorrect
);

// 2. Tələbə üçün İmtahan DTO-su (TƏHLÜKƏSİZLİK: IsCorrect YOXDUR!)
public record QuizTakingDto(
    Guid Id,
    Guid LessonId,
    string Title,
    string? Description,
    int PassingScorePercentage,
    int? TimeLimitMinutes,
    int MaxAttempts,
    int RemainingAttempts,
    IReadOnlyCollection<QuestionTakingDto> Questions
);

public record QuestionTakingDto(
    Guid Id,
    string Text,
    int Points,
    int OrderIndex,
    IReadOnlyCollection<AnswerOptionTakingDto> Options
);

public record AnswerOptionTakingDto(
    Guid Id,
    string Text
);

// 3. İmtahanı təhvil vermək və Nəticə DTO-ları
public record SubmitQuizAnswerDto(
    Guid QuestionId,
    Guid? SelectedOptionId
);

public record SubmitQuizRequestDto(
    IReadOnlyCollection<SubmitQuizAnswerDto> Answers
);

public record QuizResultDto(
    Guid AttemptId,
    Guid QuizId,
    int AttemptNumber,
    int TotalQuestions,
    int CorrectAnswers,
    int EarnedPoints,
    int TotalPoints,
    double ScorePercentage,
    bool IsPassed,
    QuizAttemptStatus Status,
    DateTime CompletedAtUtc,
    IReadOnlyCollection<QuestionReviewDto> QuestionReviews
);

public record QuestionReviewDto(
    Guid QuestionId,
    string QuestionText,
    Guid? SelectedOptionId,
    Guid? CorrectOptionId,
    bool IsCorrect,
    string? Explanation
);

public record QuizAttemptSummaryDto(
    Guid Id,
    int AttemptNumber,
    double ScorePercentage,
    bool IsPassed,
    DateTime CompletedAtUtc
);
