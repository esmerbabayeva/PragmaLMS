namespace Pragma.Application.DTOs.Categories;

public record CategoryTranslationDto(
    string Language,
    string Name,
    string? Description
);

public record CategoryDto(
    Guid Id,
    string Name,
    string Slug,
    string? Description,
    string? IconName,
    int SortOrder,
    int CourseCount,
    IReadOnlyCollection<CategoryTranslationDto>? Translations = null
);

public record CreateCategoryDto(
    string Name,
    string? Description,
    string? IconName,
    int SortOrder,
    IReadOnlyCollection<CategoryTranslationDto>? Translations = null
);

public record UpdateCategoryDto(
    string Name,
    string? Description,
    string? IconName,
    int SortOrder,
    IReadOnlyCollection<CategoryTranslationDto>? Translations = null
);
