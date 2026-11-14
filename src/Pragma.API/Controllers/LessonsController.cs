using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pragma.Application.Common;
using Pragma.Application.Contracts;
using Pragma.Application.DTOs.Lessons;
using Pragma.Domain.Enums;

namespace Pragma.API.Controllers;

/// <summary>
/// Dərslərin (Video və Markdown) idarə edilməsi.
/// </summary>
[Route("api/lessons")]
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
    /// Modula yeni dərs (Video və ya Markdown) əlavə edir (Admin və Moderator üçün).
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.CanManageCourses)]
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
    /// Dərsin detallarını, video linkini və ya mətnini qaytarır (Giriş: Qeydiyyat və ya Pulsuz önbaxış).
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<LessonDetailDto>> GetLessonById(Guid id, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        var result = await _lessonService.GetLessonByIdAsync(id, currentUserId, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Dərs məlumatlarını (ad, video URL, mətn) yeniləyir (Admin və Moderator üçün).
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.CanManageCourses)]
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
    /// Dərsi silir (Soft delete, Admin və Moderator üçün).
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.CanManageCourses)]
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
