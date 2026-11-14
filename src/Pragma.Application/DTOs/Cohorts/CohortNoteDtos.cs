using Pragma.Domain.Enums;

namespace Pragma.Application.DTOs.Cohorts;

public record CreateCohortNoteDto(
    CohortNoteType Type,
    string Text,
    GradeComponentType? GradeComponent = null
);

public record UpdateCohortNoteDto(
    CohortNoteType Type,
    string Text,
    GradeComponentType? GradeComponent = null
);

public record CohortNoteDto(
    Guid Id,
    Guid CohortId,
    Guid StudentId,
    Guid AuthorTeacherId,
    string AuthorTeacherName,
    CohortNoteType Type,
    string Text,
    GradeComponentType? GradeComponent,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc
);
