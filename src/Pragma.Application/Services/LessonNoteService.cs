using Microsoft.EntityFrameworkCore;
using Pragma.Application.Common;
using Pragma.Application.Contracts;
using Pragma.Application.DTOs.Notes;
using Pragma.Domain.Entities;

namespace Pragma.Application.Services;

public class LessonNoteService : ILessonNoteService
{
    private readonly IAppDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICourseAccessService _accessService;

    public LessonNoteService(
        IAppDbContext context,
        ICurrentUserService currentUserService,
        ICourseAccessService accessService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _accessService = accessService;
    }

    public async Task<Result<LessonNoteDto>> CreateNoteAsync(
        Guid lessonId,
        CreateLessonNoteRequestDto dto,
        CancellationToken ct = default)
    {
        var studentId = _currentUserService.UserId;
        if (!studentId.HasValue)
            return Result<LessonNoteDto>.Failure(Error.Unauthorized("İstifadəçi autentifikasiyadan keçməyib."));

        var canAccess = await _accessService.CanAccessLessonAsync(lessonId, studentId, ct);
        if (!canAccess)
            return Result<LessonNoteDto>.Failure(Error.Forbidden("Bu dərsə baxmaq icazəniz yoxdur."));

        var note = new LessonNote
        {
            StudentId = studentId.Value,
            LessonId = lessonId,
            PositionSeconds = dto.PositionSeconds,
            NoteText = dto.NoteText.Trim()
        };

        _context.LessonNotes.Add(note);
        await _context.SaveChangesAsync(ct);

        return Result<LessonNoteDto>.Success(new LessonNoteDto(
            note.Id,
            note.LessonId,
            note.PositionSeconds,
            FormatTimestamp(note.PositionSeconds),
            note.NoteText,
            note.CreatedAtUtc
        ));
    }

    public async Task<Result<IReadOnlyList<LessonNoteDto>>> GetNotesForLessonAsync(
        Guid lessonId,
        CancellationToken ct = default)
    {
        var studentId = _currentUserService.UserId;
        if (!studentId.HasValue)
            return Result<IReadOnlyList<LessonNoteDto>>.Failure(Error.Unauthorized("İstifadəçi autentifikasiyadan keçməyib."));

        var canAccess = await _accessService.CanAccessLessonAsync(lessonId, studentId, ct);
        if (!canAccess)
            return Result<IReadOnlyList<LessonNoteDto>>.Failure(Error.Forbidden("Bu dərsə baxmaq icazəniz yoxdur."));

        var notes = await _context.LessonNotes
            .AsNoTracking()
            .Where(n => n.StudentId == studentId.Value && n.LessonId == lessonId && !n.IsDeleted)
            .OrderBy(n => n.PositionSeconds)
            .ThenBy(n => n.CreatedAtUtc)
            .Select(n => new LessonNoteDto(
                n.Id,
                n.LessonId,
                n.PositionSeconds,
                FormatTimestamp(n.PositionSeconds),
                n.NoteText,
                n.CreatedAtUtc
            ))
            .ToListAsync(ct);

        return Result<IReadOnlyList<LessonNoteDto>>.Success(notes);
    }

    public async Task<Result> DeleteNoteAsync(Guid noteId, CancellationToken ct = default)
    {
        var studentId = _currentUserService.UserId;
        if (!studentId.HasValue)
            return Result.Failure(Error.Unauthorized("İstifadəçi autentifikasiyadan keçməyib."));

        var note = await _context.LessonNotes
            .FirstOrDefaultAsync(n => n.Id == noteId && !n.IsDeleted, ct);

        if (note == null)
            return Result.Failure(Error.NotFound("Qeyd tapılmadı."));

        if (note.StudentId != studentId.Value)
            return Result.Failure(Error.Forbidden("Bu qeydi silmək səlahiyyətiniz yoxdur."));

        note.MarkDeleted();
        await _context.SaveChangesAsync(ct);

        return Result.Success();
    }

    private static string FormatTimestamp(int seconds)
    {
        var ts = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return ts.Hours > 0
            ? $"{ts.Hours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}"
            : $"{ts.Minutes:D2}:{ts.Seconds:D2}";
    }
}
