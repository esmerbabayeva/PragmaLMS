namespace Pragma.Application.Contracts;

/// <summary>
/// 3 Səviyyəli Avtorizasiya: Kurs sahibliyi (Ownership) və dərsə giriş icazələrini yoxlayan servis.
/// </summary>
public interface ICourseAccessService
{
    Task<bool> CanManageCourseAsync(Guid courseId, Guid userId, bool isAdmin, CancellationToken cancellationToken = default);
    Task<bool> CanAccessLessonAsync(Guid lessonId, Guid? userId, CancellationToken cancellationToken = default);
}
