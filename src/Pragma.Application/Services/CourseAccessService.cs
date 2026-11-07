using Microsoft.EntityFrameworkCore;
using Pragma.Application.Contracts;

namespace Pragma.Application.Services;

/// <summary>
/// Kurslara və dərslərə giriş icazələrini və müəllim sahibliyini (Ownership) yoxlayan servis.
/// </summary>
public class CourseAccessService : ICourseAccessService
{
    private readonly IAppDbContext _context;

    public CourseAccessService(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<bool> CanManageCourseAsync(Guid courseId, Guid userId, bool isAdmin, CancellationToken cancellationToken = default)
    {
        if (isAdmin) return true;

        return await _context.Courses
            .AsNoTracking()
            .AnyAsync(c => c.Id == courseId && c.InstructorId == userId, cancellationToken);
    }

    public async Task<bool> CanAccessLessonAsync(Guid lessonId, Guid? userId, CancellationToken cancellationToken = default)
    {
        var lesson = await _context.Lessons
            .AsNoTracking()
            .Include(l => l.Module)
            .ThenInclude(m => m!.Course)
            .FirstOrDefaultAsync(l => l.Id == lessonId, cancellationToken);

        if (lesson == null) return false;

        // Pulsuz önbaxış dərslərinə hər kəs baxa bilər
        if (lesson.IsPreview) return true;

        if (!userId.HasValue) return false;

        var course = lesson.Module?.Course;
        if (course == null) return false;

        // Kursun müəllimi öz dərslərinə hər zaman baxa bilər
        if (course.InstructorId == userId.Value) return true;

        // Tələbənin qeydiyyatı (Enrollment) yoxlanılır
        return await _context.Enrollments
            .AsNoTracking()
            .AnyAsync(e => e.CourseId == course.Id && e.StudentId == userId.Value, cancellationToken);
    }
}
