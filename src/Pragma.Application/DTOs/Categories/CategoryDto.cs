namespace Pragma.Application.DTOs.Categories;

public record CategoryDto(
    Guid Id,
    string Name,
    string Slug,
    string? Description,
    string? IconName,
    int SortOrder,
    int CourseCount
);

public record CreateCategoryDto(
    string Name,
    string? Description,
    string? IconName,
    int SortOrder
);
