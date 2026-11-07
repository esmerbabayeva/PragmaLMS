namespace Pragma.Application.DTOs.Notes;

public record CreateLessonNoteRequestDto(
    int PositionSeconds,
    string NoteText
);

public record UpdateLessonNoteRequestDto(
    string NoteText
);

public record LessonNoteDto(
    Guid Id,
    Guid LessonId,
    int PositionSeconds,
    string FormattedTimestamp, // Məs: "03:45"
    string NoteText,
    DateTime CreatedAtUtc
);
