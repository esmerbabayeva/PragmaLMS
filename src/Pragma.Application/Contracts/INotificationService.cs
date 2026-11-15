using Pragma.Application.Common;
using Pragma.Application.DTOs.Notifications;

namespace Pragma.Application.Contracts;

public interface INotificationService
{
    Task<Result<PagedResult<NotificationDto>>> GetUserNotificationsAsync(
        Guid userId,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default);

    Task<Result<int>> GetUnreadCountAsync(
        Guid userId,
        CancellationToken ct = default);

    Task<Result<bool>> MarkAsReadAsync(
        Guid notificationId,
        Guid userId,
        CancellationToken ct = default);

    Task<Result<bool>> MarkAllAsReadAsync(
        Guid userId,
        CancellationToken ct = default);

    Task<Result<Guid>> CreateNotificationAsync(
        CreateNotificationDto dto,
        CancellationToken ct = default);
}
