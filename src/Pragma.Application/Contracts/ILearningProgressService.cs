using Pragma.Application.Common;
using Pragma.Application.DTOs.Progress;

namespace Pragma.Application.Contracts;

public interface ILearningProgressService
{
    Task<Result<LessonProgressDto>> UpdateVideoPositionAsync(Guid lessonId, UpdateVideoPositionRequestDto dto, CancellationToken ct = default);
    Task<Result<LessonProgressDto>> ToggleLessonCompleteAsync(Guid lessonId, CancellationToken ct = default);
    Task<Result<ResumeCourseDto>> GetResumeStateAsync(Guid courseId, CancellationToken ct = default);
    Task<Result<CourseProgressSummaryDto>> GetCourseProgressAsync(Guid courseId, CancellationToken ct = default);
}
