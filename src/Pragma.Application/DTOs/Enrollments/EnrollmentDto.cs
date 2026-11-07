namespace Pragma.Application.DTOs.Enrollments;

public record EnrollmentDto(
    Guid Id,
    Guid CourseId,
    string CourseTitle,
    string CourseSlug,
    string? CourseThumbnailUrl,
    string InstructorName,
    DateTime EnrolledAtUtc,
    double ProgressPercentage,
    bool IsCompleted,
    DateTime? CompletedAtUtc
);

public record EnrollCourseDto(
    Guid CourseId
);
