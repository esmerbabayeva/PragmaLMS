using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pragma.Application.Common;
using Pragma.Application.Contracts;
using Pragma.Application.DTOs.Announcements;
using Pragma.Application.DTOs.Notifications;
using Pragma.Domain.Entities;
using Pragma.Domain.Enums;

namespace Pragma.Application.Services;

public class AnnouncementService : IAnnouncementService
{
    private readonly IAppDbContext _context;
    private readonly INotificationService _notificationService;
    private readonly IUserLookupService _userLookup;
    private readonly ILogger<AnnouncementService> _logger;

    public AnnouncementService(
        IAppDbContext context,
        INotificationService notificationService,
        IUserLookupService userLookup,
        ILogger<AnnouncementService> logger)
    {
        _context = context;
        _notificationService = notificationService;
        _userLookup = userLookup;
        _logger = logger;
    }

    public async Task<Result<PagedResult<AnnouncementDto>>> GetAnnouncementsAsync(
        Guid? cohortId,
        Guid currentUserId,
        bool isAdminOrTeacher,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var query = _context.Announcements
            .Include(a => a.Cohort)
            .AsNoTracking();

        if (cohortId.HasValue)
        {
            query = query.Where(a => a.CohortId == cohortId.Value || a.CohortId == null);
        }
        else if (!isAdminOrTeacher)
        {
            // Tələbə yalnız qlobal elanları və ya üzv olduğu kohortların elanlarını görə bilər
            var enrolledCohortIds = await _context.CohortEnrollments
                .AsNoTracking()
                .Where(e => e.StudentId == currentUserId && !e.IsDeleted)
                .Select(e => e.CohortId)
                .Distinct()
                .ToListAsync(ct);

            query = query.Where(a => a.CohortId == null || enrolledCohortIds.Contains(a.CohortId.Value));
        }

        var totalCount = await query.CountAsync(ct);

        var list = await query
            .OrderByDescending(a => a.IsPinned)
            .ThenByDescending(a => a.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new
            {
                a.Id,
                a.Title,
                a.Content,
                a.AuthorId,
                a.CohortId,
                CohortName = a.Cohort != null ? a.Cohort.Name : null,
                a.IsPinned,
                a.CreatedAtUtc
            })
            .ToListAsync(ct);

        // Müəllif adlarını gətir
        var authorIds = list.Select(x => x.AuthorId).Distinct().ToList();
        var authors = await _userLookup.GetUsersByIdsAsync(authorIds, ct);

        var items = list.Select(x => new AnnouncementDto(
            x.Id,
            x.Title,
            x.Content,
            x.AuthorId,
            authors.TryGetValue(x.AuthorId, out var u) ? u.FullName : null,
            x.CohortId,
            x.CohortName,
            x.IsPinned,
            x.CreatedAtUtc
        )).ToList();

        return Result<PagedResult<AnnouncementDto>>.Success(
            PagedResult<AnnouncementDto>.Create(items, totalCount, page, pageSize));
    }

    public async Task<Result<AnnouncementDto>> CreateAnnouncementAsync(
        CreateAnnouncementDto dto,
        Guid authorId,
        bool isAdmin,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Title))
            return Result<AnnouncementDto>.Failure(Error.Validation("Elanın başlığı boş ola bilməz."));

        if (string.IsNullOrWhiteSpace(dto.Content))
            return Result<AnnouncementDto>.Failure(Error.Validation("Elanın mətni boş ola bilməz."));

        string? cohortName = null;

        if (dto.CohortId == null)
        {
            // Qlobal elanı yalnız admin verə bilər
            if (!isAdmin)
                return Result<AnnouncementDto>.Failure(Error.Forbidden("Qlobal elan yaratmaq üçün yalnız Admin səlahiyyəti tələb olunur."));
        }
        else
        {
            var cohort = await _context.Cohorts
                .FirstOrDefaultAsync(c => c.Id == dto.CohortId.Value, ct);

            if (cohort == null)
                return Result<AnnouncementDto>.Failure(Error.NotFound("Göstərilən kohort tapılmadı."));

            if (!isAdmin && cohort.TeacherId != authorId)
                return Result<AnnouncementDto>.Failure(Error.Forbidden("Bu kohorta elan yaratmaq hüququnuz yoxdur."));

            cohortName = cohort.Name;
        }

        var announcement = new Announcement
        {
            Title = dto.Title.Trim(),
            Content = dto.Content.Trim(),
            AuthorId = authorId,
            CohortId = dto.CohortId,
            IsPinned = dto.IsPinned
        };

        _context.Announcements.Add(announcement);
        await _context.SaveChangesAsync(ct);

        // Bildirişlərin göndərilməsi (asinxron şəkildə tələbələrə)
        try
        {
            List<Guid> recipientStudentIds = new();
            if (dto.CohortId.HasValue)
            {
                recipientStudentIds = await _context.CohortEnrollments
                    .Where(e => e.CohortId == dto.CohortId.Value && !e.IsDeleted)
                    .Select(e => e.StudentId)
                    .Distinct()
                    .ToListAsync(ct);
            }
            else
            {
                // Qlobal elan: aktiv tələbə qeydiyyatları
                recipientStudentIds = await _context.CohortEnrollments
                    .Where(e => !e.IsDeleted)
                    .Select(e => e.StudentId)
                    .Distinct()
                    .ToListAsync(ct);
            }

            foreach (var studentId in recipientStudentIds)
            {
                await _notificationService.CreateNotificationAsync(new CreateNotificationDto(
                    UserId: studentId,
                    Title: $"Yeni Elan: {announcement.Title}",
                    Message: announcement.Content.Length > 150 ? announcement.Content[..147] + "..." : announcement.Content,
                    Type: NotificationType.Announcement,
                    TargetUrl: "/portal/announcements.html"
                ), ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Elan bildirişləri yaradılarkən xəta baş verdi. AnnouncementId: {Id}", announcement.Id);
        }

        var author = await _userLookup.GetUserByIdAsync(authorId, ct);

        var resultDto = new AnnouncementDto(
            announcement.Id,
            announcement.Title,
            announcement.Content,
            announcement.AuthorId,
            author?.FullName,
            announcement.CohortId,
            cohortName,
            announcement.IsPinned,
            announcement.CreatedAtUtc
        );

        return Result<AnnouncementDto>.Success(resultDto);
    }

    public async Task<Result<bool>> DeleteAnnouncementAsync(
        Guid announcementId,
        Guid currentUserId,
        bool isAdmin,
        CancellationToken ct = default)
    {
        var announcement = await _context.Announcements
            .FirstOrDefaultAsync(a => a.Id == announcementId, ct);

        if (announcement == null)
            return Result<bool>.Failure(Error.NotFound("Elan tapılmadı."));

        if (!isAdmin && announcement.AuthorId != currentUserId)
            return Result<bool>.Failure(Error.Forbidden("Yalnız elanın müəllifi və ya Admin elanı silə bilər."));

        announcement.MarkDeleted();
        await _context.SaveChangesAsync(ct);

        return Result<bool>.Success(true);
    }
}
