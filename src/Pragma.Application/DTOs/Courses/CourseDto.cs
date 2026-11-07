using Pragma.Application.DTOs.Modules;
using Pragma.Domain.Enums;

namespace Pragma.Application.DTOs.Courses;

public record CourseSummaryDto(
    Guid Id,
    string Title,
    string Slug,
    string? ShortDescription,
    string? ThumbnailUrl,
    decimal Price,
    bool IsFree,
    CourseLevel Level,
    CourseStatus Status,
    Guid CategoryId,
    string CategoryName,
    Guid InstructorId,
    string InstructorName,
    double AverageRating,
    int ReviewCount,
    int TotalLessons,
    int TotalDurationMinutes,
    DateTime CreatedAtUtc
);

public record CourseDetailDto(
    Guid Id,
    string Title,
    string Slug,
    string? ShortDescription,
    string? Description,
    string? ThumbnailUrl,
    decimal Price,
    bool IsFree,
    CourseLevel Level,
    CourseStatus Status,
    Guid CategoryId,
    string CategoryName,
    Guid InstructorId,
    string InstructorName,
    double AverageRating,
    int ReviewCount,
    int TotalEnrollments,
    IReadOnlyCollection<ModuleDto> Modules,
    DateTime CreatedAtUtc
);

public record CreateCourseDto(
    string Title,
    string? ShortDescription,
    string? Description,
    string? ThumbnailUrl,
    decimal Price,
    bool IsFree,
    CourseLevel Level,
    Guid CategoryId
);

public record UpdateCourseDto(
    string Title,
    string? ShortDescription,
    string? Description,
    string? ThumbnailUrl,
    decimal Price,
    bool IsFree,
    CourseLevel Level,
    Guid CategoryId,
    CourseStatus Status
);

public record CourseFilterDto(
    int PageNumber = 1,
    int PageSize = 10,
    string? Search = null,
    Guid? CategoryId = null,
    CourseLevel? Level = null,
    bool? IsFree = null,
    string? SortBy = "newest" // "newest", "price_asc", "price_desc", "rating"
);
