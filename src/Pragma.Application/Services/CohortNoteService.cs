using Microsoft.EntityFrameworkCore;
using Pragma.Application.Common;
using Pragma.Application.Contracts;
using Pragma.Application.DTOs.Cohorts;
using Pragma.Domain.Entities;
using Pragma.Domain.Enums;

namespace Pragma.Application.Services;

public class CohortNoteService : ICohortNoteService
{
    private readonly IAppDbContext _context;
    private readonly IUserLookupService _userLookup;
    private readonly IAuditLogService _auditLog;
    private readonly TimeProvider _timeProvider;

    public CohortNoteService(
        IAppDbContext context,
        IUserLookupService userLookup,
        IAuditLogService auditLog,
        TimeProvider? timeProvider = null)
    {
        _context = context;
        _userLookup = userLookup;
        _auditLog = auditLog;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<Result<CohortNoteDto>> CreateNoteAsync(
        Guid cohortId,
        Guid studentId,
        CreateCohortNoteDto dto,
        Guid currentUserId,
        bool isAdmin,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Text))
        {
            return Result<CohortNoteDto>.Failure(Error.Validation("CohortNote.TextRequired", "Şərh mətni boş ola bilməz."));
        }

        var cohort = await _context.Cohorts
            .AsNoTracking()
            .Include(c => c.Course)
            .FirstOrDefaultAsync(c => c.Id == cohortId && !c.IsDeleted, ct);

        if (cohort == null)
        {
            return Result<CohortNoteDto>.Failure(Error.NotFound("Cohort.NotFound", "Kohort tapılmadı."));
        }

        if (!isAdmin && cohort.TeacherId != currentUserId && cohort.Course?.InstructorId != currentUserId)
        {
            return Result<CohortNoteDto>.Failure(Error.Forbidden("CohortNote.Forbidden", "Yalnız qrupun təyin olunmuş müəllimi və ya admin şərh yaza bilər."));
        }

        var note = new CohortNote
        {
            CohortId = cohortId,
            StudentId = studentId,
            AuthorTeacherId = currentUserId,
            Type = dto.Type,
            Text = dto.Text.Trim(),
            GradeComponent = dto.GradeComponent
        };

        _context.CohortNotes.Add(note);
        await _context.SaveChangesAsync(ct);

        var author = await _userLookup.GetUserByIdAsync(currentUserId, ct);

        var resultDto = new CohortNoteDto(
            note.Id,
            note.CohortId,
            note.StudentId,
            note.AuthorTeacherId,
            author?.FullName ?? "Müəllim",
            note.Type,
            note.Text,
            note.GradeComponent,
            note.CreatedAtUtc,
            note.UpdatedAtUtc
        );

        return Result<CohortNoteDto>.Success(resultDto);
    }

    public async Task<Result<CohortNoteDto>> UpdateNoteAsync(
        Guid cohortId,
        Guid noteId,
        UpdateCohortNoteDto dto,
        Guid currentUserId,
        bool isAdmin,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Text))
        {
            return Result<CohortNoteDto>.Failure(Error.Validation("CohortNote.TextRequired", "Şərh mətni boş ola bilməz."));
        }

        var note = await _context.CohortNotes
            .Include(n => n.Cohort)
                .ThenInclude(c => c!.Course)
            .FirstOrDefaultAsync(n => n.Id == noteId && n.CohortId == cohortId && !n.IsDeleted, ct);

        if (note == null)
        {
            return Result<CohortNoteDto>.Failure(Error.NotFound("CohortNote.NotFound", "Şərh tapılmadı."));
        }

        if (!isAdmin && note.Cohort?.TeacherId != currentUserId && note.Cohort?.Course?.InstructorId != currentUserId)
        {
            return Result<CohortNoteDto>.Failure(Error.Forbidden("CohortNote.Forbidden", "Bu qrupun şərhlərini redaktə etmək səlahiyyətiniz yoxdur."));
        }

        if (!isAdmin && note.AuthorTeacherId != currentUserId)
        {
            return Result<CohortNoteDto>.Failure(Error.Forbidden("CohortNote.Forbidden", "Yalnız öz yazdığınız şərhi redaktə edə bilərsiniz."));
        }

        var currentUser = await _userLookup.GetUserByIdAsync(currentUserId, ct);
        var userEmail = currentUser?.Email ?? (isAdmin ? "Admin" : "Teacher");

        // Audit Log: Növ dəyişibsə qeyd olunur
        if (note.Type != dto.Type)
        {
            await _auditLog.LogAsync(
                userId: currentUserId,
                userEmail: userEmail,
                action: "CohortNoteTypeChanged",
                entityName: nameof(CohortNote),
                entityId: note.Id.ToString(),
                details: $"Note {note.Id} type changed from {note.Type} to {dto.Type} for student {note.StudentId}",
                ipAddress: null,
                cancellationToken: ct);
        }

        note.Type = dto.Type;
        note.Text = dto.Text.Trim();
        note.GradeComponent = dto.GradeComponent;
        note.MarkUpdated();

        await _context.SaveChangesAsync(ct);

        var author = note.AuthorTeacherId == currentUserId
            ? currentUser
            : await _userLookup.GetUserByIdAsync(note.AuthorTeacherId, ct);

        var resultDto = new CohortNoteDto(
            note.Id,
            note.CohortId,
            note.StudentId,
            note.AuthorTeacherId,
            author?.FullName ?? "Müəllim",
            note.Type,
            note.Text,
            note.GradeComponent,
            note.CreatedAtUtc,
            note.UpdatedAtUtc
        );

        return Result<CohortNoteDto>.Success(resultDto);
    }

    public async Task<Result<bool>> DeleteNoteAsync(
        Guid cohortId,
        Guid noteId,
        Guid currentUserId,
        bool isAdmin,
        CancellationToken ct = default)
    {
        var note = await _context.CohortNotes
            .Include(n => n.Cohort)
                .ThenInclude(c => c!.Course)
            .FirstOrDefaultAsync(n => n.Id == noteId && n.CohortId == cohortId && !n.IsDeleted, ct);

        if (note == null)
        {
            return Result<bool>.Failure(Error.NotFound("CohortNote.NotFound", "Şərh tapılmadı."));
        }

        if (!isAdmin && note.Cohort?.TeacherId != currentUserId && note.Cohort?.Course?.InstructorId != currentUserId)
        {
            return Result<bool>.Failure(Error.Forbidden("CohortNote.Forbidden", "Bu qrupun şərhlərini silmək səlahiyyətiniz yoxdur."));
        }

        if (!isAdmin && note.AuthorTeacherId != currentUserId)
        {
            return Result<bool>.Failure(Error.Forbidden("CohortNote.Forbidden", "Yalnız öz yazdığınız şərhi silə bilərsiniz."));
        }

        var currentUser = await _userLookup.GetUserByIdAsync(currentUserId, ct);
        var userEmail = currentUser?.Email ?? (isAdmin ? "Admin" : "Teacher");

        // Audit Log: Şərh silmə qeyd olunur
        await _auditLog.LogAsync(
            userId: currentUserId,
            userEmail: userEmail,
            action: "CohortNoteDeleted",
            entityName: nameof(CohortNote),
            entityId: note.Id.ToString(),
            details: $"Note {note.Id} ({note.Type}) deleted for student {note.StudentId}",
            ipAddress: null,
            cancellationToken: ct);

        note.MarkDeleted();
        await _context.SaveChangesAsync(ct);

        return Result<bool>.Success(true);
    }

    public async Task<Result<IReadOnlyCollection<CohortNoteDto>>> GetNotesForStudentAsync(
        Guid cohortId,
        Guid studentId,
        Guid currentUserId,
        bool isAdmin,
        CancellationToken ct = default)
    {
        var cohort = await _context.Cohorts
            .AsNoTracking()
            .Include(c => c.Course)
            .FirstOrDefaultAsync(c => c.Id == cohortId && !c.IsDeleted, ct);

        if (cohort == null)
        {
            return Result<IReadOnlyCollection<CohortNoteDto>>.Failure(Error.NotFound("Cohort.NotFound", "Kohort tapılmadı."));
        }

        if (!isAdmin && cohort.TeacherId != currentUserId && cohort.Course?.InstructorId != currentUserId)
        {
            return Result<IReadOnlyCollection<CohortNoteDto>>.Failure(Error.Forbidden("CohortNote.Forbidden", "Bu qrupun şərhlərinə baxmaq səlahiyyətiniz yoxdur."));
        }

        var notes = await _context.CohortNotes
            .AsNoTracking()
            .Where(n => n.CohortId == cohortId && n.StudentId == studentId && !n.IsDeleted)
            .OrderByDescending(n => n.CreatedAtUtc)
            .ToListAsync(ct);

        var authorIds = notes.Select(n => n.AuthorTeacherId).Distinct().ToList();
        var authorDict = (await _userLookup.GetUsersByIdsAsync(authorIds, ct)) ?? new Dictionary<Guid, UserBasicInfoDto>();

        var result = new List<CohortNoteDto>();
        foreach (var n in notes)
        {
            if (!authorDict.TryGetValue(n.AuthorTeacherId, out var author) || author == null)
            {
                author = await _userLookup.GetUserByIdAsync(n.AuthorTeacherId, ct);
            }

            result.Add(new CohortNoteDto(
                n.Id,
                n.CohortId,
                n.StudentId,
                n.AuthorTeacherId,
                author?.FullName ?? "Müəllim",
                n.Type,
                n.Text,
                n.GradeComponent,
                n.CreatedAtUtc,
                n.UpdatedAtUtc
            ));
        }

        return Result<IReadOnlyCollection<CohortNoteDto>>.Success(result);
    }

    public async Task<Result<IReadOnlyCollection<CohortNoteDto>>> GetNotesForCohortAsync(
        Guid cohortId,
        Guid currentUserId,
        bool isAdmin,
        CancellationToken ct = default)
    {
        var cohort = await _context.Cohorts
            .AsNoTracking()
            .Include(c => c.Course)
            .FirstOrDefaultAsync(c => c.Id == cohortId && !c.IsDeleted, ct);

        if (cohort == null)
        {
            return Result<IReadOnlyCollection<CohortNoteDto>>.Failure(Error.NotFound("Cohort.NotFound", "Kohort tapılmadı."));
        }

        if (!isAdmin && cohort.TeacherId != currentUserId && cohort.Course?.InstructorId != currentUserId)
        {
            return Result<IReadOnlyCollection<CohortNoteDto>>.Failure(Error.Forbidden("CohortNote.Forbidden", "Bu qrupun şərhlərinə baxmaq səlahiyyətiniz yoxdur."));
        }

        var notes = await _context.CohortNotes
            .AsNoTracking()
            .Where(n => n.CohortId == cohortId && !n.IsDeleted)
            .OrderByDescending(n => n.CreatedAtUtc)
            .ToListAsync(ct);

        var authorIds = notes.Select(n => n.AuthorTeacherId).Distinct().ToList();
        var authorDict = (await _userLookup.GetUsersByIdsAsync(authorIds, ct)) ?? new Dictionary<Guid, UserBasicInfoDto>();

        var result = new List<CohortNoteDto>();
        foreach (var n in notes)
        {
            if (!authorDict.TryGetValue(n.AuthorTeacherId, out var author) || author == null)
            {
                author = await _userLookup.GetUserByIdAsync(n.AuthorTeacherId, ct);
            }

            result.Add(new CohortNoteDto(
                n.Id,
                n.CohortId,
                n.StudentId,
                n.AuthorTeacherId,
                author?.FullName ?? "Müəllim",
                n.Type,
                n.Text,
                n.GradeComponent,
                n.CreatedAtUtc,
                n.UpdatedAtUtc
            ));
        }

        return Result<IReadOnlyCollection<CohortNoteDto>>.Success(result);
    }
}
