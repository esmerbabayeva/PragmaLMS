using Microsoft.EntityFrameworkCore;
using Pragma.Application.Common;
using Pragma.Application.Contracts;
using Pragma.Domain.Entities;
using Pragma.Infrastructure.Persistence;

namespace Pragma.Infrastructure.Services;

public class AuditLogService : IAuditLogService
{
    private readonly AppDbContext _context;

    public AuditLogService(AppDbContext context)
    {
        _context = context;
    }

    public async Task LogAsync(
        Guid? userId,
        string? userEmail,
        string action,
        string entityName,
        string entityId,
        string details,
        string? ipAddress = null,
        CancellationToken cancellationToken = default)
    {
        var log = new AuditLog
        {
            UserId = userId,
            UserEmail = userEmail,
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            Details = details,
            IpAddress = ipAddress
        };

        _context.AuditLogs.Add(log);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<PagedResult<AuditLogDto>> GetAuditLogsAsync(
        int pageNumber = 1,
        int pageSize = 20,
        string? actionFilter = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.AuditLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(actionFilter))
        {
            query = query.Where(a => a.Action.ToLower().Contains(actionFilter.Trim().ToLower()));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var items = await query
            .OrderByDescending(a => a.CreatedAtUtc)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AuditLogDto(
                a.Id,
                a.UserId,
                a.UserEmail,
                a.Action,
                a.EntityName,
                a.EntityId,
                a.Details,
                a.CreatedAtUtc,
                a.IpAddress
            ))
            .ToListAsync(cancellationToken);

        return new PagedResult<AuditLogDto>(items, totalCount, pageNumber, pageSize);
    }
}
