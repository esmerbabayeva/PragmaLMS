using Microsoft.EntityFrameworkCore;
using Pragma.Application.Contracts;
using Pragma.Domain.Enums;

namespace Pragma.Application.Services;

/// <summary>
/// Kurslara və dərslərə giriş icazələrini və müəllim sahibliyini (Ownership) yoxlayan servis.
/// </summary>
public class CourseAccessService : ICourseAccessService
{
    private readonly IAppDbContext _context;
    private readonly IUserLookupService? _userLookup;

    public CourseAccessService(IAppDbContext context, IUserLookupService? userLookup = null)
    {
        _context = context;
        _userLookup = userLookup;
    }

    public async Task<bool> CanManageCourseAsync(Guid courseId, Guid userId, bool isAdmin, CancellationToken cancellationToken = default)
    {
        if (isAdmin) return true;

        var isInstructor = await _context.Courses
            .AsNoTracking()
            .AnyAsync(c => c.Id == courseId && (c.InstructorId == userId || c.InstructorId == Guid.Empty), cancellationToken);
        if (isInstructor) return true;

        var isCohortTeacher = await _context.Cohorts
            .AsNoTracking()
            .AnyAsync(c => c.CourseId == courseId && c.TeacherId == userId && !c.IsDeleted, cancellationToken);
        if (isCohortTeacher) return true;

        if (_userLookup != null)
        {
            var teacherIds = await _userLookup.GetUserIdsInRoleAsync(UserRoles.Teacher, cancellationToken);
            if (teacherIds.Contains(userId))
            {
                return true;
            }

            var instructorIds = await _userLookup.GetUserIdsInRoleAsync(UserRoles.Instructor, cancellationToken);
            if (instructorIds.Contains(userId))
            {
                return true;
            }
        }

        return false;
    }

    public Task<bool> CanAccessLessonAsync(Guid lessonId, Guid? userId, CancellationToken cancellationToken = default)
    {
        return CanAccessLessonAsync(lessonId, userId, isAdmin: false, cancellationToken);
    }

    public async Task<bool> CanAccessLessonAsync(Guid lessonId, Guid? userId, bool isAdmin, CancellationToken cancellationToken = default)
    {
        if (isAdmin) return true;

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
