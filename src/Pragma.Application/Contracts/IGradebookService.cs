using Pragma.Application.Common;
using Pragma.Application.DTOs.Gradebook;

namespace Pragma.Application.Contracts;

public interface IGradebookService
{
    Task<Result<CourseGradebookDto>> GetCourseGradebookAsync(Guid courseId, CancellationToken ct = default);
}
