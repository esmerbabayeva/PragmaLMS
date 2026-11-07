namespace Pragma.Application.DTOs.Progress;

public record UpdateVideoPositionRequestDto(
    int CurrentPositionSeconds,
    int DurationSeconds
);

public record LessonProgressDto(
    Guid LessonId,
    bool IsCompleted,
    int LastPositionSeconds,
    int MaxPositionSeconds,
    DateTime? CompletedAtUtc
);

public record CourseProgressSummaryDto(
    Guid CourseId,
    double OverallProgressPercentage,
    int TotalLessons,
    int CompletedLessons,
    bool IsCompleted,
    List<Guid> CompletedLessonIds
);

public record ResumeCourseDto(
    Guid CourseId,
    string CourseTitle,
    Guid LessonId,
    string LessonTitle,
    Guid ModuleId,
    string ModuleTitle,
    int LastPositionSeconds,
    double OverallProgressPercentage
);
