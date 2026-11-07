using Microsoft.EntityFrameworkCore;
using Pragma.Application.Common;
using Pragma.Application.Contracts;
using Pragma.Application.DTOs.Progress;
using Pragma.Domain.Entities;

namespace Pragma.Application.Services;

public class LearningProgressService : ILearningProgressService
{
    private readonly IAppDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICourseAccessService _accessService;

    public LearningProgressService(
        IAppDbContext context,
        ICurrentUserService currentUserService,
        ICourseAccessService accessService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _accessService = accessService;
    }

    public async Task<Result<LessonProgressDto>> UpdateVideoPositionAsync(
        Guid lessonId,
        UpdateVideoPositionRequestDto dto,
        CancellationToken ct = default)
    {
        var studentId = _currentUserService.UserId;
        if (!studentId.HasValue)
            return Result<LessonProgressDto>.Failure(Error.Unauthorized("İstifadəçi autentifikasiyadan keçməyib."));

        var canAccess = await _accessService.CanAccessLessonAsync(lessonId, studentId, ct);
        if (!canAccess)
            return Result<LessonProgressDto>.Failure(Error.Forbidden("Bu dərsə baxmaq icazəniz yoxdur."));

        var lesson = await _context.Lessons
            .AsNoTracking()
            .Include(l => l.Module)
            .FirstOrDefaultAsync(l => l.Id == lessonId && !l.IsDeleted, ct);

        if (lesson == null)
            return Result<LessonProgressDto>.Failure(Error.NotFound("Dərs tapılmadı."));

        var progress = await _context.LessonProgresses
            .FirstOrDefaultAsync(p => p.StudentId == studentId.Value && p.LessonId == lessonId && !p.IsDeleted, ct);

        if (progress == null)
        {
            progress = new LessonProgress
            {
                StudentId = studentId.Value,
                LessonId = lessonId,
                LastPositionSeconds = dto.CurrentPositionSeconds,
                MaxPositionSeconds = dto.CurrentPositionSeconds,
                IsCompleted = false
            };
            _context.LessonProgresses.Add(progress);
        }
        else
        {
            progress.LastPositionSeconds = dto.CurrentPositionSeconds;
            if (dto.CurrentPositionSeconds > progress.MaxPositionSeconds)
            {
                progress.MaxPositionSeconds = dto.CurrentPositionSeconds;
            }
            progress.MarkUpdated();
        }

        // 90% YouTube avto-tamamlama qaydası
        if (dto.DurationSeconds > 0 && !progress.IsCompleted)
        {
            var completionThreshold = (int)Math.Ceiling(dto.DurationSeconds * 0.90);
            if (progress.MaxPositionSeconds >= completionThreshold)
            {
                progress.IsCompleted = true;
                progress.CompletedAtUtc = DateTime.UtcNow;
            }
        }

        await _context.SaveChangesAsync(ct);

        // Kursun ümumi tamamlanma faizini yeniləyirik
        await RecalculateCourseProgressAsync(studentId.Value, lesson.Module!.CourseId, ct);

        return Result<LessonProgressDto>.Success(new LessonProgressDto(
            progress.LessonId,
            progress.IsCompleted,
            progress.LastPositionSeconds,
            progress.MaxPositionSeconds,
            progress.CompletedAtUtc
        ));
    }

    public async Task<Result<LessonProgressDto>> ToggleLessonCompleteAsync(Guid lessonId, CancellationToken ct = default)
    {
        var studentId = _currentUserService.UserId;
        if (!studentId.HasValue)
            return Result<LessonProgressDto>.Failure(Error.Unauthorized("İstifadəçi autentifikasiyadan keçməyib."));

        var canAccess = await _accessService.CanAccessLessonAsync(lessonId, studentId, ct);
        if (!canAccess)
            return Result<LessonProgressDto>.Failure(Error.Forbidden("Bu dərsə baxmaq icazəniz yoxdur."));

        var lesson = await _context.Lessons
            .AsNoTracking()
            .Include(l => l.Module)
            .FirstOrDefaultAsync(l => l.Id == lessonId && !l.IsDeleted, ct);

        if (lesson == null)
            return Result<LessonProgressDto>.Failure(Error.NotFound("Dərs tapılmadı."));

        var progress = await _context.LessonProgresses
            .FirstOrDefaultAsync(p => p.StudentId == studentId.Value && p.LessonId == lessonId && !p.IsDeleted, ct);

        if (progress == null)
        {
            progress = new LessonProgress
            {
                StudentId = studentId.Value,
                LessonId = lessonId,
                IsCompleted = true,
                CompletedAtUtc = DateTime.UtcNow
            };
            _context.LessonProgresses.Add(progress);
        }
        else
        {
            progress.IsCompleted = !progress.IsCompleted;
            progress.CompletedAtUtc = progress.IsCompleted ? DateTime.UtcNow : null;
            progress.MarkUpdated();
        }

        await _context.SaveChangesAsync(ct);
        await RecalculateCourseProgressAsync(studentId.Value, lesson.Module!.CourseId, ct);

        return Result<LessonProgressDto>.Success(new LessonProgressDto(
            progress.LessonId,
            progress.IsCompleted,
            progress.LastPositionSeconds,
            progress.MaxPositionSeconds,
            progress.CompletedAtUtc
        ));
    }

    public async Task<Result<ResumeCourseDto>> GetResumeStateAsync(Guid courseId, CancellationToken ct = default)
    {
        var studentId = _currentUserService.UserId;
        if (!studentId.HasValue)
            return Result<ResumeCourseDto>.Failure(Error.Unauthorized("İstifadəçi autentifikasiyadan keçməyib."));

        var course = await _context.Courses
            .AsNoTracking()
            .Include(c => c.Modules.Where(m => !m.IsDeleted).OrderBy(m => m.OrderIndex))
                .ThenInclude(m => m.Lessons.Where(l => !l.IsDeleted).OrderBy(l => l.OrderIndex))
            .FirstOrDefaultAsync(c => c.Id == courseId && !c.IsDeleted, ct);

        if (course == null)
            return Result<ResumeCourseDto>.Failure(Error.NotFound("Kurs tapılmadı."));

        var enrollment = await _context.Enrollments
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.StudentId == studentId.Value && e.CourseId == courseId && !e.IsDeleted, ct);

        if (enrollment == null)
            return Result<ResumeCourseDto>.Failure(Error.Forbidden("Bu kursa qeydiyyatınız yoxdur."));

        var allLessons = course.Modules
            .SelectMany(m => m.Lessons.Select(l => new { Lesson = l, Module = m }))
            .ToList();

        if (allLessons.Count == 0)
            return Result<ResumeCourseDto>.Failure(Error.NotFound("Kursda aktiv dərs tapılmadı."));

        var lessonIds = allLessons.Select(x => x.Lesson.Id).ToList();

        var progresses = await _context.LessonProgresses
            .AsNoTracking()
            .Where(p => p.StudentId == studentId.Value && lessonIds.Contains(p.LessonId) && !p.IsDeleted)
            .ToDictionaryAsync(p => p.LessonId, ct);

        // 1. Əvvəlcə yarıda qalmış (başlanmış amma bitməmiş) dərsi axtarırıq
        var inProgressItem = allLessons.FirstOrDefault(x =>
            progresses.TryGetValue(x.Lesson.Id, out var p) && !p.IsCompleted && p.LastPositionSeconds > 0);

        // 2. Əgər yoxdursa, ilk tamamlanmamış dərsi götürürük
        var resumeItem = inProgressItem ?? allLessons.FirstOrDefault(x =>
            !progresses.TryGetValue(x.Lesson.Id, out var p) || !p.IsCompleted) ?? allLessons.First();

        var lastPos = progresses.TryGetValue(resumeItem.Lesson.Id, out var existingP) ? existingP.LastPositionSeconds : 0;

        return Result<ResumeCourseDto>.Success(new ResumeCourseDto(
            course.Id,
            course.Title,
            resumeItem.Lesson.Id,
            resumeItem.Lesson.Title,
            resumeItem.Module.Id,
            resumeItem.Module.Title,
            lastPos,
            enrollment.ProgressPercentage
        ));
    }

    public async Task<Result<CourseProgressSummaryDto>> GetCourseProgressAsync(Guid courseId, CancellationToken ct = default)
    {
        var studentId = _currentUserService.UserId;
        if (!studentId.HasValue)
            return Result<CourseProgressSummaryDto>.Failure(Error.Unauthorized("İstifadəçi autentifikasiyadan keçməyib."));

        var enrollment = await _context.Enrollments
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.StudentId == studentId.Value && e.CourseId == courseId && !e.IsDeleted, ct);

        if (enrollment == null)
            return Result<CourseProgressSummaryDto>.Failure(Error.Forbidden("Bu kursa qeydiyyatınız yoxdur."));

        var lessonIds = await _context.Lessons
            .AsNoTracking()
            .Where(l => l.Module!.CourseId == courseId && !l.IsDeleted && !l.Module.IsDeleted)
            .Select(l => l.Id)
            .ToListAsync(ct);

        var completedIds = await _context.LessonProgresses
            .AsNoTracking()
            .Where(p => p.StudentId == studentId.Value && lessonIds.Contains(p.LessonId) && p.IsCompleted && !p.IsDeleted)
            .Select(p => p.LessonId)
            .ToListAsync(ct);

        return Result<CourseProgressSummaryDto>.Success(new CourseProgressSummaryDto(
            courseId,
            enrollment.ProgressPercentage,
            lessonIds.Count,
            completedIds.Count,
            enrollment.IsCompleted,
            completedIds
        ));
    }

    private async Task RecalculateCourseProgressAsync(Guid studentId, Guid courseId, CancellationToken ct)
    {
        var enrollment = await _context.Enrollments
            .FirstOrDefaultAsync(e => e.StudentId == studentId && e.CourseId == courseId && !e.IsDeleted, ct);

        if (enrollment == null) return;

        var totalLessons = await _context.Lessons
            .CountAsync(l => l.Module!.CourseId == courseId && !l.IsDeleted && !l.Module.IsDeleted, ct);

        if (totalLessons == 0)
        {
            enrollment.ProgressPercentage = 100.0;
            enrollment.IsCompleted = true;
            enrollment.CompletedAtUtc = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);
            return;
        }

        var completedLessons = await _context.LessonProgresses
            .CountAsync(p => p.StudentId == studentId &&
                             p.IsCompleted &&
                             !p.IsDeleted &&
                             _context.Lessons.Any(l => l.Id == p.LessonId && l.Module!.CourseId == courseId && !l.IsDeleted), ct);

        var percentage = Math.Round((double)completedLessons / totalLessons * 100.0, 1);
        if (percentage > 100.0) percentage = 100.0;

        enrollment.ProgressPercentage = percentage;
        if (percentage >= 100.0 && !enrollment.IsCompleted)
        {
            enrollment.IsCompleted = true;
            enrollment.CompletedAtUtc = DateTime.UtcNow;
        }
        else if (percentage < 100.0 && enrollment.IsCompleted)
        {
            enrollment.IsCompleted = false;
            enrollment.CompletedAtUtc = null;
        }

        enrollment.MarkUpdated();
        await _context.SaveChangesAsync(ct);
    }
}
