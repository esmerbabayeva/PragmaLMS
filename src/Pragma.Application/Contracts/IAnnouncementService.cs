using Pragma.Application.Common;
using Pragma.Application.DTOs.Announcements;

namespace Pragma.Application.Contracts;

public interface IAnnouncementService
{
    Task<Result<PagedResult<AnnouncementDto>>> GetAnnouncementsAsync(
        Guid? cohortId,
        Guid currentUserId,
        bool isAdminOrTeacher,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default);

    Task<Result<AnnouncementDto>> CreateAnnouncementAsync(
        CreateAnnouncementDto dto,
        Guid authorId,
        bool isAdmin,
        CancellationToken ct = default);

    Task<Result<bool>> DeleteAnnouncementAsync(
        Guid announcementId,
        Guid currentUserId,
        bool isAdmin,
        CancellationToken ct = default);
}
