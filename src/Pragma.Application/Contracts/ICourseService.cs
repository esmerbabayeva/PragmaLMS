using Pragma.Application.Common;
using Pragma.Application.DTOs.Courses;
using Pragma.Application.DTOs.Modules;

namespace Pragma.Application.Contracts;

public interface ICourseService
{
    Task<PagedResult<CourseSummaryDto>> GetCoursesAsync(CourseFilterDto filter, CancellationToken cancellationToken = default);
    Task<Result<CourseDetailDto>> GetCourseByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<CourseDetailDto>> GetCourseBySlugAsync(string slug, CancellationToken cancellationToken = default);
    Task<Result<Guid>> CreateCourseAsync(CreateCourseDto dto, Guid instructorId, CancellationToken cancellationToken = default);
    Task<Result> UpdateCourseAsync(Guid id, UpdateCourseDto dto, Guid userId, bool isAdmin, CancellationToken cancellationToken = default);
    Task<Result> DeleteCourseAsync(Guid id, Guid userId, bool isAdmin, CancellationToken cancellationToken = default);
    Task<Result> PublishCourseAsync(Guid courseId, Guid userId, bool isAdmin, CancellationToken cancellationToken = default);
    Task<Result> UnpublishCourseAsync(Guid courseId, Guid userId, bool isAdmin, CancellationToken cancellationToken = default);
    Task<Result> ReorderModulesAsync(Guid courseId, ReorderItemsRequestDto dto, Guid userId, bool isAdmin, CancellationToken cancellationToken = default);
}
