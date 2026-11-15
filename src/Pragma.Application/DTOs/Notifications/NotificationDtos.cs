using Pragma.Domain.Enums;

namespace Pragma.Application.DTOs.Notifications;

public record NotificationDto(
    Guid Id,
    Guid UserId,
    string Title,
    string Message,
    NotificationType Type,
    bool IsRead,
    DateTime? ReadAtUtc,
    string? TargetUrl,
    DateTime CreatedAtUtc
);

public record CreateNotificationDto(
    Guid UserId,
    string Title,
    string Message,
    NotificationType Type = NotificationType.System,
    string? TargetUrl = null
);
