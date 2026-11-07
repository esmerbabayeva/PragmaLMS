namespace Pragma.Application.DTOs.Gradebook;

public record StudentGradeSummaryDto(
    Guid StudentId,
    string FullName,
    string Email,
    DateTime EnrolledAtUtc,
    double ProgressPercentage,
    bool IsCompleted,
    int QuizzesTaken,
    double AverageQuizScore
);

public record CourseGradebookDto(
    Guid CourseId,
    string CourseTitle,
    int TotalStudents,
    double AverageCompletionPercentage,
    List<StudentGradeSummaryDto> Students
);
