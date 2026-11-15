namespace Pragma.Application.DTOs.LessonQa;

public record LessonAnswerDto(
    Guid Id,
    Guid QuestionId,
    Guid AuthorId,
    string? AuthorName,
    string Content,
    bool IsInstructorAnswer,
    DateTime CreatedAtUtc
);

public record LessonQuestionDto(
    Guid Id,
    Guid LessonId,
    Guid StudentId,
    string? StudentName,
    string Title,
    string Content,
    int? PositionSeconds,
    bool IsResolved,
    int AnswersCount,
    DateTime CreatedAtUtc,
    IReadOnlyList<LessonAnswerDto> Answers
);

public record CreateLessonQuestionDto(
    string Title,
    string Content,
    int? PositionSeconds = null
);

public record CreateLessonAnswerDto(
    string Content
);
