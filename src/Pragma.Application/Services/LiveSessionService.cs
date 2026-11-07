using Microsoft.EntityFrameworkCore;
using Pragma.Application.Common;
using Pragma.Application.Contracts;
using Pragma.Application.DTOs.LiveSession;
using Pragma.Domain.Enums;

namespace Pragma.Application.Services;

public class LiveSessionService : ILiveSessionService
{
    private readonly IAppDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICourseAccessService _accessService;

    public LiveSessionService(
        IAppDbContext context,
        ICurrentUserService currentUserService,
        ICourseAccessService accessService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _accessService = accessService;
    }

    public async Task<Result<LiveSessionDto>> GetLessonLiveSessionAsync(Guid lessonId, CancellationToken cancellationToken = default)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue)
            return Result<LiveSessionDto>.Failure(Error.Unauthorized("İstifadəçi autentifikasiyadan keçməyib."));

        var canAccess = await _accessService.CanAccessLessonAsync(lessonId, currentUserId, cancellationToken);
        if (!canAccess)
            return Result<LiveSessionDto>.Failure(Error.Forbidden("Bu dərsin canlı yayımına qatılmaq icazəniz yoxdur."));

        var lesson = await _context.Lessons
            .AsNoTracking()
            .Include(l => l.Module)
                .ThenInclude(m => m!.Course)
            .FirstOrDefaultAsync(l => l.Id == lessonId && !l.IsDeleted, cancellationToken);

        if (lesson == null)
            return Result<LiveSessionDto>.Failure(Error.NotFound("Dərs tapılmadı."));

        var course = lesson.Module!.Course!;
        var isInstructor = course.InstructorId == currentUserId.Value || _currentUserService.IsInRole(UserRoles.Admin);

        var roomName = $"pragma-live-{course.Slug}-{lesson.Id.ToString()[..8]}";
        var meetingUrl = $"https://meet.jit.si/{roomName}";

        return Result<LiveSessionDto>.Success(new LiveSessionDto(
            lesson.Id,
            lesson.Title,
            roomName,
            meetingUrl,
            isInstructor
        ));
    }
}
