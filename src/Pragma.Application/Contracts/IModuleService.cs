using Pragma.Application.Common;
using Pragma.Application.DTOs.Modules;

namespace Pragma.Application.Contracts;

public interface IModuleService
{
    Task<Result<Guid>> CreateModuleAsync(Guid courseId, CreateModuleDto dto, Guid userId, bool isAdmin, CancellationToken cancellationToken = default);
    Task<Result> UpdateModuleAsync(Guid moduleId, UpdateModuleDto dto, Guid userId, bool isAdmin, CancellationToken cancellationToken = default);
    Task<Result> DeleteModuleAsync(Guid moduleId, Guid userId, bool isAdmin, CancellationToken cancellationToken = default);
    Task<Result> ReorderLessonsAsync(Guid moduleId, ReorderItemsRequestDto dto, Guid userId, bool isAdmin, CancellationToken cancellationToken = default);
}
