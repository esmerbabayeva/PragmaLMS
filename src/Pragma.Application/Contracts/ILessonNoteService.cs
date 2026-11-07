using Pragma.Application.Common;
using Pragma.Application.DTOs.Notes;

namespace Pragma.Application.Contracts;

public interface ILessonNoteService
{
    Task<Result<LessonNoteDto>> CreateNoteAsync(Guid lessonId, CreateLessonNoteRequestDto dto, CancellationToken ct = default);
    Task<Result<IReadOnlyList<LessonNoteDto>>> GetNotesForLessonAsync(Guid lessonId, CancellationToken ct = default);
    Task<Result> DeleteNoteAsync(Guid noteId, CancellationToken ct = default);
}
