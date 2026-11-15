namespace Pragma.Application.DTOs.Announcements;

public record AnnouncementDto(
    Guid Id,
    string Title,
    string Content,
    Guid AuthorId,
    string? AuthorName,
    Guid? CohortId,
    string? CohortName,
    bool IsPinned,
    DateTime CreatedAtUtc
);

public record CreateAnnouncementDto(
    string Title,
    string Content,
    Guid? CohortId = null,
    bool IsPinned = false
);
