using Pragma.Application.DTOs.Lessons;

namespace Pragma.Application.DTOs.Modules;

public record ModuleDto(
    Guid Id,
    Guid CourseId,
    string Title,
    string? Description,
    int OrderIndex,
    IReadOnlyCollection<LessonSummaryDto> Lessons
);

public record CreateModuleDto(
    string Title,
    string? Description,
    int OrderIndex
);

public record UpdateModuleDto(
    string Title,
    string? Description,
    int OrderIndex
);

public record ReorderItemDto(
    Guid Id,
    int OrderIndex
);

public record ReorderItemsRequestDto(
    IReadOnlyCollection<ReorderItemDto> Items
);
