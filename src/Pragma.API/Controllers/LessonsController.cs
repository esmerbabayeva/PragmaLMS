using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pragma.Application.Contracts;
using Pragma.Application.DTOs.Lessons;
using Pragma.Domain.Enums;

namespace Pragma.API.Controllers;

public class LessonsController : BaseApiController
{
    private readonly ILessonService _lessonService;
    private readonly ICurrentUserService _currentUserService;

    public LessonsController(
        ILessonService lessonService,
        ICurrentUserService currentUserService)
    {
        _lessonService = lessonService;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Modula yeni dərs (Video və ya Markdown) əlavə edilməsi.
    /// </summary>
    [Authorize(Roles = $"{UserRoles.Instructor},{UserRoles.Admin}")]
    [HttpPost("/api/modules/{moduleId:guid}/lessons")]
    public async Task<ActionResult<Guid>> CreateLesson(Guid moduleId, [FromBody] CreateLessonDto dto, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue) return Unauthorized();

        var isAdmin = _currentUserService.IsInRole(UserRoles.Admin);
        var result = await _lessonService.CreateLessonAsync(moduleId, dto, currentUserId.Value, isAdmin, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Dərsin detalları, videosu və ya mətni (Giriş icazəsi: Pulsuz önbaxış və ya kursa enrollment tələb olunur).
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<LessonDetailDto>> GetLessonById(Guid id, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        var result = await _lessonService.GetLessonByIdAsync(id, currentUserId, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Dərs məlumatlarının redaktəsi.
    /// </summary>
    [Authorize(Roles = $"{UserRoles.Instructor},{UserRoles.Admin}")]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult> UpdateLesson(Guid id, [FromBody] UpdateLessonDto dto, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue) return Unauthorized();

        var isAdmin = _currentUserService.IsInRole(UserRoles.Admin);
        var result = await _lessonService.UpdateLessonAsync(id, dto, currentUserId.Value, isAdmin, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Dərsin silinməsi (Soft delete).
    /// </summary>
    [Authorize(Roles = $"{UserRoles.Instructor},{UserRoles.Admin}")]
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> DeleteLesson(Guid id, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue) return Unauthorized();

        var isAdmin = _currentUserService.IsInRole(UserRoles.Admin);
        var result = await _lessonService.DeleteLessonAsync(id, currentUserId.Value, isAdmin, cancellationToken);
        return HandleResult(result);
    }
}
