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
            "Təlimçi",
            enrollment.EnrolledAtUtc,
            enrollment.ProgressPercentage,
            enrollment.IsCompleted,
            enrollment.CompletedAtUtc
        );

        return Result<EnrollmentDto>.Success(dto);
    }

    public async Task<IReadOnlyCollection<EnrollmentDto>> GetMyEnrollmentsAsync(Guid studentId, CancellationToken cancellationToken = default)
    {
        return await _context.Enrollments
            .AsNoTracking()
            .Include(e => e.Course)
                .ThenInclude(c => c!.Category)
            .Where(e => e.StudentId == studentId && !e.IsDeleted)
            .OrderByDescending(e => e.EnrolledAtUtc)
            .Select(e => new EnrollmentDto(
                e.Id,
                e.CourseId,
                e.Course != null ? e.Course.Title : string.Empty,
                e.Course != null ? e.Course.Slug : string.Empty,
                e.Course != null ? e.Course.ThumbnailUrl : null,
                e.Course != null && e.Course.Category != null ? e.Course.Category.Name : "Ümumi",
                "Təlimçi",
                e.EnrolledAtUtc,
                e.ProgressPercentage,
                e.IsCompleted,
                e.CompletedAtUtc
            ))
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> IsEnrolledAsync(Guid courseId, Guid studentId, CancellationToken cancellationToken = default)
    {
        return await _context.Enrollments
            .AsNoTracking()
            .AnyAsync(e => e.CourseId == courseId && e.StudentId == studentId, cancellationToken);
    }
}
