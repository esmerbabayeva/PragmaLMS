using Pragma.Application.Common;
using Pragma.Application.DTOs.Lessons;

namespace Pragma.Application.Contracts;

public interface ILessonService
{
    Task<Result<Guid>> CreateLessonAsync(Guid moduleId, CreateLessonDto dto, Guid userId, bool isAdmin, CancellationToken cancellationToken = default);
    Task<Result<LessonDetailDto>> GetLessonByIdAsync(Guid lessonId, Guid? userId, CancellationToken cancellationToken = default);
    Task<Result> UpdateLessonAsync(Guid lessonId, UpdateLessonDto dto, Guid userId, bool isAdmin, CancellationToken cancellationToken = default);
    Task<Result> DeleteLessonAsync(Guid lessonId, Guid userId, bool isAdmin, CancellationToken cancellationToken = default);
}
