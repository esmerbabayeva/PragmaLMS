using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Pragma.Application.Common;
using Pragma.Application.Contracts;
using Pragma.Application.DTOs.Lessons;
using Pragma.Domain.Entities;

namespace Pragma.Application.Services;

public class LessonService : ILessonService
{
    private readonly IAppDbContext _context;
    private readonly ICourseAccessService _accessService;
    private readonly IValidator<CreateLessonDto> _createValidator;

    public LessonService(
        IAppDbContext context,
        ICourseAccessService accessService,
        IValidator<CreateLessonDto> createValidator)
    {
        _context = context;
        _accessService = accessService;
        _createValidator = createValidator;
    }

    public async Task<Result<Guid>> CreateLessonAsync(Guid moduleId, CreateLessonDto dto, Guid userId, bool isAdmin, CancellationToken cancellationToken = default)
    {
        var validation = await _createValidator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<Guid>.Failure(Error.Validation("Lesson.Validation", string.Join("; ", validation.Errors.Select(e => e.ErrorMessage))));
        }

        var module = await _context.Modules.FirstOrDefaultAsync(m => m.Id == moduleId, cancellationToken);
        if (module == null)
        {
            return Result<Guid>.Failure(Error.NotFound("Module.NotFound", "Modul tapılmadı."));
        }

        var canManage = await _accessService.CanManageCourseAsync(module.CourseId, userId, isAdmin, cancellationToken);
        if (!canManage)
        {
            return Result<Guid>.Failure(Error.Forbidden("Course.Forbidden", "Bu modula dərs əlavə etmək icazəniz yoxdur."));
        }

        var lesson = new Lesson
        {
            ModuleId = moduleId,
            Title = dto.Title.Trim(),
            OrderIndex = dto.OrderIndex,
            Type = dto.Type,
            Content = dto.Content,
            VideoUrl = dto.VideoUrl,
            DurationSeconds = dto.DurationSeconds,
            IsPreview = dto.IsPreview
        };

        _context.Lessons.Add(lesson);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(lesson.Id);
    }

    public async Task<Result<LessonDetailDto>> GetLessonByIdAsync(Guid lessonId, Guid? userId, CancellationToken cancellationToken = default)
    {
        var lesson = await _context.Lessons
            .AsNoTracking()
            .Include(l => l.Module)
            .ThenInclude(m => m!.Course)
            .Include(l => l.Quiz)
            .Include(l => l.Resources)
            .FirstOrDefaultAsync(l => l.Id == lessonId, cancellationToken);

        if (lesson == null)
        {
            return Result<LessonDetailDto>.Failure(Error.NotFound("Lesson.NotFound", "Dərs tapılmadı."));
        }

        // Giriş icazəsi yoxlanışı (Pulsuz önbaxış, müəllim və ya tələbə qeydiyyatı)
        var canAccess = await _accessService.CanAccessLessonAsync(lessonId, userId, cancellationToken);
        if (!canAccess)
        {
            return Result<LessonDetailDto>.Failure(Error.Forbidden("Lesson.AccessDenied", "Bu dərsi izləmək üçün kursa qeydiyyatdan keçməlisiniz."));
        }

        var resources = lesson.Resources.Select(r => new LessonResourceDto(
            r.Id,
            r.Title,
            r.FileUrl,
            r.FileSizeBytes,
            r.FileExtension
        )).ToList();

        var detail = new LessonDetailDto(
            lesson.Id,
            lesson.ModuleId,
            lesson.Module?.CourseId ?? Guid.Empty,
            lesson.Module?.Course?.Title ?? string.Empty,
            lesson.Title,
            lesson.OrderIndex,
            lesson.Type,
            lesson.Content,
            lesson.VideoUrl,
            lesson.DurationSeconds,
            lesson.IsPreview,
            lesson.Quiz != null,
            lesson.Quiz?.Id,
            resources
        );

        return Result<LessonDetailDto>.Success(detail);
    }

    public async Task<Result> UpdateLessonAsync(Guid lessonId, UpdateLessonDto dto, Guid userId, bool isAdmin, CancellationToken cancellationToken = default)
    {
        var lesson = await _context.Lessons
            .Include(l => l.Module)
            .FirstOrDefaultAsync(l => l.Id == lessonId, cancellationToken);

        if (lesson == null)
        {
            return Result.Failure(Error.NotFound("Lesson.NotFound", "Dərs tapılmadı."));
        }

        var canManage = await _accessService.CanManageCourseAsync(lesson.Module!.CourseId, userId, isAdmin, cancellationToken);
        if (!canManage)
        {
            return Result.Failure(Error.Forbidden("Course.Forbidden", "Bu dərsi redaktə etmək icazəniz yoxdur."));
        }

        lesson.Title = dto.Title.Trim();
        lesson.OrderIndex = dto.OrderIndex;
        lesson.Type = dto.Type;
        lesson.Content = dto.Content;
        lesson.VideoUrl = dto.VideoUrl;
        lesson.DurationSeconds = dto.DurationSeconds;
        lesson.IsPreview = dto.IsPreview;

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> DeleteLessonAsync(Guid lessonId, Guid userId, bool isAdmin, CancellationToken cancellationToken = default)
    {
        var lesson = await _context.Lessons
            .Include(l => l.Module)
            .FirstOrDefaultAsync(l => l.Id == lessonId, cancellationToken);

        if (lesson == null)
        {
            return Result.Failure(Error.NotFound("Lesson.NotFound", "Dərs tapılmadı."));
        }

        var canManage = await _accessService.CanManageCourseAsync(lesson.Module!.CourseId, userId, isAdmin, cancellationToken);
        if (!canManage)
        {
            return Result.Failure(Error.Forbidden("Course.Forbidden", "Bu dərsi silmək icazəniz yoxdur."));
        }

        lesson.MarkDeleted();
        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
