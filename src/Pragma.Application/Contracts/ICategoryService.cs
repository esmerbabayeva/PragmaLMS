using Pragma.Application.Common;
using Pragma.Application.DTOs.Categories;

namespace Pragma.Application.Contracts;

public interface ICategoryService
{
    Task<Result<IReadOnlyCollection<CategoryDto>>> GetCategoriesAsync(string? requestedLanguage = null, CancellationToken ct = default);
    Task<Result<CategoryDto>> GetCategoryByIdAsync(Guid id, string? requestedLanguage = null, CancellationToken ct = default);
    Task<Result<Guid>> CreateCategoryAsync(CreateCategoryDto dto, CancellationToken ct = default);
    Task<Result<CategoryDto>> UpdateCategoryAsync(Guid id, UpdateCategoryDto dto, CancellationToken ct = default);
    Task<Result> DeleteCategoryAsync(Guid id, CancellationToken ct = default);
}
