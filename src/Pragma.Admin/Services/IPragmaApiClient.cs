using Pragma.Application.Common;
using Pragma.Application.DTOs.Auth;
using Pragma.Application.DTOs.Categories;
using Pragma.Application.DTOs.Courses;
using Pragma.Application.DTOs.Gamification;
using Pragma.Application.DTOs.Gradebook;

namespace Pragma.Admin.Services;

public interface IPragmaApiClient
{
    Task<AuthResponseDto?> LoginAsync(string email, string password, CancellationToken ct = default);
    Task<PagedResult<CourseSummaryDto>?> GetCoursesAsync(string? search = null, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default);
    Task<CourseDetailDto?> GetCourseByIdAsync(Guid id, CancellationToken ct = default);
    Task<Guid?> CreateCourseAsync(CreateCourseDto dto, CancellationToken ct = default);
    Task<bool> PublishCourseAsync(Guid id, CancellationToken ct = default);
    Task<bool> UnpublishCourseAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<CategoryDto>?> GetCategoriesAsync(CancellationToken ct = default);
    Task<bool> CreateCategoryAsync(CreateCategoryDto dto, CancellationToken ct = default);
    Task<CourseGradebookDto?> GetGradebookAsync(Guid courseId, CancellationToken ct = default);
    Task<(byte[] Bytes, string ContentType, string FileName)?> DownloadGradebookExcelAsync(Guid courseId, CancellationToken ct = default);
    Task<IReadOnlyList<LeaderboardEntryDto>?> GetLeaderboardAsync(int top = 10, CancellationToken ct = default);
}
