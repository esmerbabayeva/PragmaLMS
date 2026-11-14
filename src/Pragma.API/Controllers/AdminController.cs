using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pragma.Application.Common;
using Pragma.Application.Contracts;
using Pragma.Application.DTOs.Admin;

namespace Pragma.API.Controllers;

/// <summary>
/// Sistem SuperAdmin-i üçün idarəetmə endpoint-ləri (İstifadəçilər, Rollar, Audit, Statistika, Bal düzəlişi).
/// </summary>
[Authorize(Policy = AuthorizationPolicies.RequireAdmin)]
[Route("api/admin")]
public class AdminController : BaseApiController
{
    private readonly IAdminService _adminService;
    private readonly IAuditLogService _auditLogService;
    private readonly ICurrentUserService _currentUserService;

    public AdminController(
        IAdminService adminService,
        IAuditLogService auditLogService,
        ICurrentUserService currentUserService)
    {
        _adminService = adminService;
        _auditLogService = auditLogService;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Sistem istifadəçilərinin axtarış, rol filteri və səhifələmə ilə siyahısı.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.CanManageUsers)]
    [HttpGet("users")]
    public async Task<ActionResult<PagedResult<AdminUserListItemDto>>> GetUsers(
        [FromQuery] string? search,
        [FromQuery] string? role,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _adminService.GetUsersAsync(search, role, pageNumber, pageSize, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Yeni istifadəçi yaradır və rol təyin edir.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.CanManageUsers)]
    [HttpPost("users")]
    public async Task<ActionResult<Guid>> CreateUser(
        [FromBody] CreateAdminUserDto dto,
        CancellationToken cancellationToken = default)
    {
        var currentUserId = _currentUserService.UserId ?? Guid.Empty;
        var currentUserEmail = _currentUserService.Email ?? "admin@pragma.edu.az";

        var result = await _adminService.CreateUserAsync(dto, currentUserId, currentUserEmail, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// İstifadəçi məlumatlarını redaktə edir.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.CanManageUsers)]
    [HttpPut("users/{id:guid}")]
    public async Task<ActionResult> UpdateUser(
        Guid id,
        [FromBody] UpdateAdminUserDto dto,
        CancellationToken cancellationToken = default)
    {
        var currentUserId = _currentUserService.UserId ?? Guid.Empty;
        var currentUserEmail = _currentUserService.Email ?? "admin@pragma.edu.az";

        var result = await _adminService.UpdateUserAsync(id, dto, currentUserId, currentUserEmail, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// İstifadəçini silir (Admin özünü və ya sonuncu Admin-i silə bilməz).
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.CanManageUsers)]
    [HttpDelete("users/{id:guid}")]
    public async Task<ActionResult> DeleteUser(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var currentUserId = _currentUserService.UserId ?? Guid.Empty;
        var currentUserEmail = _currentUserService.Email ?? "admin@pragma.edu.az";

        var result = await _adminService.DeleteUserAsync(id, currentUserId, currentUserEmail, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// İstifadəçini bloklayır və ya blokdan çıxarır (Admin özünü və ya sonuncu Admin-i bloklaya bilməz).
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.CanManageUsers)]
    [HttpPost("users/{id:guid}/toggle-lock")]
    public async Task<ActionResult<bool>> ToggleUserLock(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var currentUserId = _currentUserService.UserId ?? Guid.Empty;
        var currentUserEmail = _currentUserService.Email ?? "admin@pragma.edu.az";

        var result = await _adminService.ToggleUserLockAsync(id, currentUserId, currentUserEmail, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// İstifadəçinin şifrəsini sıfırlayır (Açıq şifrə heç vaxt geri qaytarılmır).
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.CanManageUsers)]
    [HttpPost("users/{id:guid}/reset-password")]
    public async Task<ActionResult> ResetUserPassword(
        Guid id,
        [FromBody] ResetUserPasswordDto dto,
        CancellationToken cancellationToken = default)
    {
        var currentUserId = _currentUserService.UserId ?? Guid.Empty;
        var currentUserEmail = _currentUserService.Email ?? "admin@pragma.edu.az";

        var result = await _adminService.ResetUserPasswordAsync(id, dto, currentUserId, currentUserEmail, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// İstifadəçinin rollarını dəyişir (Admin öz Admin rolunu və ya sonuncu Admin-in rolunu ala bilməz).
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.CanManageRoles)]
    [HttpPut("users/{id:guid}/roles")]
    public async Task<ActionResult> AssignUserRoles(
        Guid id,
        [FromBody] ChangeUserRolesDto dto,
        CancellationToken cancellationToken = default)
    {
        var currentUserId = _currentUserService.UserId ?? Guid.Empty;
        var currentUserEmail = _currentUserService.Email ?? "admin@pragma.edu.az";

        var result = await _adminService.AssignUserRolesAsync(id, dto, currentUserId, currentUserEmail, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Tələbənin balını və ya davamiyyətini inzibati qaydada düzəldir və audit log-a yazır.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.CanAdjustGrades)]
    [HttpPost("grades/adjust")]
    public async Task<ActionResult> AdjustGrade(
        [FromBody] AdjustGradeDto dto,
        CancellationToken cancellationToken = default)
    {
        var currentUserId = _currentUserService.UserId ?? Guid.Empty;
        var currentUserEmail = _currentUserService.Email ?? "admin@pragma.edu.az";

        var result = await _adminService.AdjustStudentGradeAsync(dto, currentUserId, currentUserEmail, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Ümumi sistem statistikaları və analitik göstəricilər.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.CanViewSystemStatistics)]
    [HttpGet("statistics")]
    public async Task<ActionResult<SystemStatisticsDto>> GetSystemStatistics(CancellationToken cancellationToken = default)
    {
        var stats = await _adminService.GetSystemStatisticsAsync(cancellationToken);
        return Ok(stats);
    }

    /// <summary>
    /// Sistem hesabatını ClosedXML ilə Excel (.xlsx) olaraq ixrac edir.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.CanViewSystemStatistics)]
    [HttpGet("statistics/export-excel")]
    public async Task<IActionResult> ExportSystemReportExcel(CancellationToken cancellationToken = default)
    {
        var fileBytes = await _adminService.ExportSystemReportExcelAsync(cancellationToken);
        var fileName = $"Pragma_SystemReport_{DateTime.UtcNow:yyyyMMdd_HHmm}.xlsx";
        return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    /// <summary>
    /// Kritik inzibati əməliyyatların audit log qeydləri (kim, nə vaxt, nəyi dəyişdi).
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.CanViewAuditLogs)]
    [HttpGet("audit-logs")]
    public async Task<ActionResult<PagedResult<AuditLogDto>>> GetAuditLogs(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? action = null,
        CancellationToken cancellationToken = default)
    {
        var logs = await _auditLogService.GetAuditLogsAsync(pageNumber, pageSize, action, cancellationToken);
        return Ok(logs);
    }
}
