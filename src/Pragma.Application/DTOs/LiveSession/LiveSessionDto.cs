namespace Pragma.Application.DTOs.LiveSession;

public record LiveSessionDto(
    Guid LessonId,
    string LessonTitle,
    string RoomName,
    string MeetingUrl,
    bool IsInstructor
);
