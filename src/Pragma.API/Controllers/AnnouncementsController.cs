using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pragma.Application.Common;
using Pragma.Application.Contracts;
using Pragma.Application.DTOs.Announcements;

namespace Pragma.API.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/announcements")]
public class AnnouncementsController : BaseApiController
{
    private readonly IAnnouncementService _announcementService;
    private readonly ICurrentUserService _currentUserService;

    public AnnouncementsController(
        IAnnouncementService announcementService,
        ICurrentUserService currentUserService)
    {
        _announcementService = announcementService;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Elanlar siyahısı (Qlobal və ya seçilmiş kohort üzrə, səhifələnmiş).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<AnnouncementDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<AnnouncementDto>>> GetAnnouncements(
        [FromQuery] Guid? cohortId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue) return Unauthorized();

        var isAdminOrTeacher = User.IsInRole("Admin") || User.IsInRole("Teacher") || User.IsInRole("Instructor");
        var result = await _announcementService.GetAnnouncementsAsync(cohortId, userId.Value, isAdminOrTeacher, page, pageSize, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Yeni elan paylaşımı (Admin qlobal və ya qrup elanı, Teacher yalnız öz qrupunun elanını yarada bilər).
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Admin,Teacher,Instructor")]
    [ProducesResponseType(typeof(AnnouncementDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AnnouncementDto>> CreateAnnouncement(
        [FromBody] CreateAnnouncementDto dto,
        CancellationToken ct = default)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue) return Unauthorized();

        var isAdmin = User.IsInRole("Admin");
        var result = await _announcementService.CreateAnnouncementAsync(dto, userId.Value, isAdmin, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Elanın silinməsi (Müəllif və ya Admin).
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAnnouncement(Guid id, CancellationToken ct = default)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue) return Unauthorized();

        var isAdmin = User.IsInRole("Admin");
        var result = await _announcementService.DeleteAnnouncementAsync(id, userId.Value, isAdmin, ct);
        return HandleResult(result);
    }
}
