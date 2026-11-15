using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pragma.Application.Common;
using Pragma.Application.Contracts;
using Pragma.Application.DTOs.Notifications;
using Pragma.Domain.Entities;
using Pragma.Domain.Enums;

namespace Pragma.Application.Services;

public class NotificationService : INotificationService
{
    private readonly IAppDbContext _context;
    private readonly IEmailSender _emailSender;
    private readonly IUserLookupService _userLookup;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        IAppDbContext _context,
        IEmailSender emailSender,
        IUserLookupService userLookup,
        ILogger<NotificationService> logger)
    {
        this._context = _context;
        _emailSender = emailSender;
        _userLookup = userLookup;
        _logger = logger;
    }

    public async Task<Result<PagedResult<NotificationDto>>> GetUserNotificationsAsync(
        Guid userId,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var query = _context.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == userId);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(n => n.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(n => new NotificationDto(
                n.Id,
                n.UserId,
                n.Title,
                n.Message,
                n.Type,
                n.IsRead,
                n.ReadAtUtc,
                n.TargetUrl,
                n.CreatedAtUtc
            ))
            .ToListAsync(ct);

        return Result<PagedResult<NotificationDto>>.Success(
            PagedResult<NotificationDto>.Create(items, totalCount, page, pageSize));
    }

    public async Task<Result<int>> GetUnreadCountAsync(Guid userId, CancellationToken ct = default)
    {
        var count = await _context.Notifications
            .AsNoTracking()
            .CountAsync(n => n.UserId == userId && !n.IsRead, ct);

        return Result<int>.Success(count);
    }

    public async Task<Result<bool>> MarkAsReadAsync(Guid notificationId, Guid userId, CancellationToken ct = default)
    {
        var notification = await _context.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId, ct);

        if (notification == null)
            return Result<bool>.Failure(Error.NotFound("Bildiriş tapılmadı."));

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            notification.ReadAtUtc = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);
        }

        return Result<bool>.Success(true);
    }

    public async Task<Result<bool>> MarkAllAsReadAsync(Guid userId, CancellationToken ct = default)
    {
        var unreadNotifications = await _context.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ToListAsync(ct);

        if (unreadNotifications.Count == 0)
            return Result<bool>.Success(true);

        var now = DateTime.UtcNow;
        foreach (var n in unreadNotifications)
        {
            n.IsRead = true;
            n.ReadAtUtc = now;
        }

        await _context.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }

    public async Task<Result<Guid>> CreateNotificationAsync(CreateNotificationDto dto, CancellationToken ct = default)
    {
        var notification = new Notification
        {
            UserId = dto.UserId,
            Title = dto.Title,
            Message = dto.Message,
            Type = dto.Type,
            TargetUrl = dto.TargetUrl,
            IsRead = false
        };

        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync(ct);

        // Əgər mühüm bildirişdirsə (AbsenceWarning, CertificateReady, GradePosted, Announcement), e-poçt da göndər
        if (dto.Type is NotificationType.AbsenceWarning or NotificationType.CertificateReady or NotificationType.GradePosted or NotificationType.Announcement)
        {
            try
            {
                var user = await _userLookup.GetUserByIdAsync(dto.UserId, ct);
                if (user != null && !string.IsNullOrWhiteSpace(user.Email))
                {
                    var subject = $"[Pragma LMS] {dto.Title}";
                    var body = $"""
                        <div style="font-family: Arial, sans-serif; padding: 20px; color: #1e293b;">
                            <h2 style="color: #0284c7;">{dto.Title}</h2>
                            <p style="font-size: 15px; line-height: 1.5;">{dto.Message}</p>
                            {(string.IsNullOrWhiteSpace(dto.TargetUrl) ? "" : $"<p><a href=\"{dto.TargetUrl}\" style=\"display: inline-block; padding: 10px 18px; background-color: #0284c7; color: #fff; text-decoration: none; border-radius: 6px; font-weight: bold;\">Ətraflı bax</a></p>")}
                            <hr style="border: 0; border-top: 1px solid #e2e8f0; margin-top: 24px;" />
                            <p style="font-size: 12px; color: #64748b;">Bu avtomatik bildiriş Pragma LMS platformasından göndərilib.</p>
                        </div>
                        """;

                    await _emailSender.SendAsync(user.Email, subject, body, ct);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Bildiriş üçün e-poçt göndərilərkən xəta baş verdi. UserId: {UserId}", dto.UserId);
            }
        }

        return Result<Guid>.Success(notification.Id);
    }
}
