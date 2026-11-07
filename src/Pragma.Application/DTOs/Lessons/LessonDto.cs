using Pragma.Domain.Enums;

namespace Pragma.Application.DTOs.Lessons;

public record LessonSummaryDto(
    Guid Id,
    Guid ModuleId,
    string Title,
    int OrderIndex,
    LessonType Type,
    int DurationSeconds,
    bool IsPreview,
    bool HasQuiz,
    Guid? QuizId
);

public record LessonDetailDto(
    Guid Id,
    Guid ModuleId,
    Guid CourseId,
    string CourseTitle,
    string Title,
    int OrderIndex,
    LessonType Type,
    string? Content,
    string? VideoUrl,
    int DurationSeconds,
    bool IsPreview,
    bool HasQuiz,
    Guid? QuizId,
    IReadOnlyCollection<LessonResourceDto> Resources
);

public record LessonResourceDto(
    Guid Id,
    string Title,
    string FileUrl,
    long FileSizeBytes,
    string? FileExtension
);

public record CreateLessonDto(
    string Title,
    int OrderIndex,
    LessonType Type,
    string? Content,
    string? VideoUrl,
    int DurationSeconds,
    bool IsPreview
);

public record UpdateLessonDto(
    string Title,
    int OrderIndex,
    LessonType Type,
    string? Content,
    string? VideoUrl,
    int DurationSeconds,
    bool IsPreview
);
