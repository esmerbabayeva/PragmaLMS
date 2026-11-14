namespace Pragma.Application.DTOs.Admin;

public record AdminUserListItemDto(
    Guid Id,
    string Email,
    string FullName,
    IReadOnlyList<string> Roles,
    bool IsLockedOut,
    DateTime CreatedAtUtc
);

public record CreateAdminUserDto(
    string Email,
    string FullName,
    string Password,
    string Role
);

public record UpdateAdminUserDto(
    string FullName,
    string Email
);

public record ChangeUserRolesDto(
    IReadOnlyList<string> Roles
);

public record ResetUserPasswordDto(
    string NewPassword
);

public record AdjustGradeDto(
    Guid EnrollmentId,
    decimal? MiniProjectGrade,
    decimal? FinalGrade,
    int? AbsentCount,
    int? LateCount,
    string Reason
);

public record SystemStatisticsDto(
    int TotalUsers,
    int TotalStudents,
    int TotalInstructors,
    int TotalTeachers,
    int TotalCourses,
    int PublishedCourses,
    int TotalCategories,
    int TotalEnrollments,
    int TotalCompletedCourses,
    int TotalQuizzes,
    decimal AverageScore
);
