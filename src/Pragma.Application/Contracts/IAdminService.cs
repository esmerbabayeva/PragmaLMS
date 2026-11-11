using Pragma.Application.Common;
using Pragma.Application.DTOs.Admin;

namespace Pragma.Application.Contracts;

public interface IAdminService
{
    Task<PagedResult<AdminUserListItemDto>> GetUsersAsync(
        string? search = null,
        string? role = null,
        int pageNumber = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    Task<Result<Guid>> CreateUserAsync(
        CreateAdminUserDto dto,
        Guid currentAdminId,
        string currentAdminEmail,
        CancellationToken cancellationToken = default);

    Task<Result> UpdateUserAsync(
        Guid id,
        UpdateAdminUserDto dto,
        Guid currentAdminId,
        string currentAdminEmail,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteUserAsync(
        Guid id,
        Guid currentAdminId,
        string currentAdminEmail,
        CancellationToken cancellationToken = default);

    Task<Result<bool>> ToggleUserLockAsync(
        Guid id,
        Guid currentAdminId,
        string currentAdminEmail,
        CancellationToken cancellationToken = default);

    Task<Result> ResetUserPasswordAsync(
        Guid id,
        ResetUserPasswordDto dto,
        Guid currentAdminId,
        string currentAdminEmail,
        CancellationToken cancellationToken = default);

    Task<Result> AssignUserRolesAsync(
        Guid id,
        ChangeUserRolesDto dto,
        Guid currentAdminId,
        string currentAdminEmail,
        CancellationToken cancellationToken = default);

    Task<Result> AdjustStudentGradeAsync(
        AdjustGradeDto dto,
        Guid currentAdminId,
        string currentAdminEmail,
        CancellationToken cancellationToken = default);

    Task<SystemStatisticsDto> GetSystemStatisticsAsync(CancellationToken cancellationToken = default);

    Task<byte[]> ExportSystemReportExcelAsync(CancellationToken cancellationToken = default);
}
