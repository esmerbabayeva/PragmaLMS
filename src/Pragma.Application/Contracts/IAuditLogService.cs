using Pragma.Application.Common;

namespace Pragma.Application.Contracts;

public record AuditLogDto(
    Guid Id,
    Guid? UserId,
    string? UserEmail,
    string Action,
    string EntityName,
    string EntityId,
    string Details,
    DateTime CreatedAtUtc,
    string? IpAddress
);

public interface IAuditLogService
{
    Task LogAsync(
        Guid? userId,
        string? userEmail,
        string action,
        string entityName,
        string entityId,
        string details,
        string? ipAddress = null,
        CancellationToken cancellationToken = default);

    Task<PagedResult<AuditLogDto>> GetAuditLogsAsync(
        int pageNumber = 1,
        int pageSize = 20,
        string? actionFilter = null,
        CancellationToken cancellationToken = default);
}
