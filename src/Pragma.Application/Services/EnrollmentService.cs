using Microsoft.EntityFrameworkCore;
using Pragma.Application.Common;
using Pragma.Application.Contracts;
using Pragma.Application.DTOs.Enrollments;
using Pragma.Domain.Entities;
using Pragma.Domain.Enums;

namespace Pragma.Application.Services;

public class EnrollmentService : IEnrollmentService
{
    private readonly IAppDbContext _context;

    public EnrollmentService(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<Result<EnrollmentDto>> EnrollAsync(Guid courseId, Guid studentId, CancellationToken cancellationToken = default)
    {
        var course = await _context.Courses
            .AsNoTracking()
            .Include(c => c.Category)
            .FirstOrDefaultAsync(c => c.Id == courseId, cancellationToken);

        if (course == null)
        {
            return Result<EnrollmentDto>.Failure(Error.NotFound("Course.NotFound", "Kursa qeydiyyat üçün kurs tapılmadı."));
        }

        // Yalnız dərc edilmiş (Published) kurslara qeydiyyat mümkündür
        if (course.Status != CourseStatus.Published)
        {
            return Result<EnrollmentDto>.Failure(Error.Forbidden("Enrollment.NotPublished", "Yalnız dərc olunmuş və təsdiqlənmiş kurslara qeydiyyat mümkündür."));
        }

        // Dublikat qeydiyyat qorunması
        var alreadyEnrolled = await _context.Enrollments
            .AsNoTracking()
            .AnyAsync(e => e.CourseId == courseId && e.StudentId == studentId, cancellationToken);

        if (alreadyEnrolled)
        {
            return Result<EnrollmentDto>.Failure(Error.Conflict("Enrollment.AlreadyEnrolled", "Siz artıq bu kursa qeydiyyatdan keçmisiniz."));
        }

        var enrollment = new Enrollment
        {
            CourseId = courseId,
            StudentId = studentId,
            ProgressPercentage = 0.0,
            IsCompleted = false,
            EnrolledAtUtc = DateTime.UtcNow
        };

        _context.Enrollments.Add(enrollment);
        await _context.SaveChangesAsync(cancellationToken);

        var dto = new EnrollmentDto(
            enrollment.Id,
            course.Id,
            course.Title,
            course.Slug,
            course.ThumbnailUrl,
            course.Category?.Name ?? "Ümumi",
            "Instructor",
            enrollment.EnrolledAtUtc,
            enrollment.ProgressPercentage,
            enrollment.IsCompleted,
            enrollment.CompletedAtUtc
        );

        return Result<EnrollmentDto>.Success(dto);
    }

    public async Task<IReadOnlyCollection<EnrollmentDto>> GetMyEnrollmentsAsync(Guid studentId, string? language = null, CancellationToken cancellationToken = default)
    {
        var lang = LanguageResolutionHelper.NormalizeLanguage(language);

        var enrollments = await _context.Enrollments
            .AsNoTracking()
            .Include(e => e.Course)
                .ThenInclude(c => c!.Translations)
            .Include(e => e.Course)
                .ThenInclude(c => c!.Category)
                    .ThenInclude(cat => cat!.Translations)
            .Where(e => e.StudentId == studentId && !e.IsDeleted)
            .OrderByDescending(e => e.EnrolledAtUtc)
            .ToListAsync(cancellationToken);

        return enrollments.Select(e => new EnrollmentDto(
            e.Id,
            e.CourseId,
            e.Course != null ? LanguageResolutionHelper.ResolveTitle(e.Course, lang) : string.Empty,
            e.Course != null ? e.Course.Slug : string.Empty,
            e.Course != null ? e.Course.ThumbnailUrl : null,
            e.Course != null && e.Course.Category != null ? LanguageResolutionHelper.ResolveCategoryName(e.Course.Category, lang) : "Ümumi",
            "Instructor",
            e.EnrolledAtUtc,
            e.ProgressPercentage,
            e.IsCompleted,
            e.CompletedAtUtc
        )).ToList();
    }

    public async Task<bool> IsEnrolledAsync(Guid courseId, Guid studentId, CancellationToken cancellationToken = default)
    {
        return await _context.Enrollments
            .AsNoTracking()
            .AnyAsync(e => e.CourseId == courseId && e.StudentId == studentId, cancellationToken);
    }

    public async Task<Result<EnrollmentDto>> ManageEnrollStudentAsync(Guid courseId, Guid studentId, Guid currentUserId, bool isAdmin, CancellationToken cancellationToken = default)
    {
        var course = await _context.Courses
            .Include(c => c.Category)
            .FirstOrDefaultAsync(c => c.Id == courseId, cancellationToken);

        if (course == null)
        {
            return Result<EnrollmentDto>.Failure(Error.NotFound("Course.NotFound", "Kurs tapılmadı."));
        }

        if (!isAdmin && course.InstructorId != currentUserId)
        {
            return Result<EnrollmentDto>.Failure(Error.Forbidden("Course.Forbidden", "Yalnız öz kurslarınıza tələbə əlavə edə bilərsiniz."));
        }

        var alreadyEnrolled = await _context.Enrollments
            .AnyAsync(e => e.CourseId == courseId && e.StudentId == studentId && !e.IsDeleted, cancellationToken);

        if (alreadyEnrolled)
        {
            return Result<EnrollmentDto>.Failure(Error.Conflict("Enrollment.AlreadyEnrolled", "Tələbə artıq bu kursa qeydiyyatdan keçib."));
        }

        var enrollment = new Enrollment
        {
            CourseId = courseId,
            StudentId = studentId,
            ProgressPercentage = 0.0,
            IsCompleted = false,
            EnrolledAtUtc = DateTime.UtcNow
        };

        _context.Enrollments.Add(enrollment);
        await _context.SaveChangesAsync(cancellationToken);

        var dto = new EnrollmentDto(
            enrollment.Id,
            enrollment.CourseId,
            course.Title,
            course.Slug,
            course.ThumbnailUrl,
            course.Category?.Name ?? "Ümumi",
            "Instructor",
            enrollment.EnrolledAtUtc,
            enrollment.ProgressPercentage,
            enrollment.IsCompleted,
            enrollment.CompletedAtUtc
        );

        return Result<EnrollmentDto>.Success(dto);
    }

    public async Task<Result> ManageUnenrollStudentAsync(Guid courseId, Guid studentId, Guid currentUserId, bool isAdmin, CancellationToken cancellationToken = default)
    {
        var course = await _context.Courses
            .FirstOrDefaultAsync(c => c.Id == courseId, cancellationToken);

        if (course == null)
        {
            return Result.Failure(Error.NotFound("Course.NotFound", "Kurs tapılmadı."));
        }

        if (!isAdmin && course.InstructorId != currentUserId)
        {
            return Result.Failure(Error.Forbidden("Course.Forbidden", "Yalnız öz kurslarınızdan tələbə çıxara bilərsiniz."));
        }

        var enrollment = await _context.Enrollments
            .FirstOrDefaultAsync(e => e.CourseId == courseId && e.StudentId == studentId && !e.IsDeleted, cancellationToken);

        if (enrollment == null)
        {
            return Result.Failure(Error.NotFound("Enrollment.NotFound", "Tələbənin bu kursa aktiv qeydiyyatı tapılmadı."));
        }

        _context.Enrollments.Remove(enrollment);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
