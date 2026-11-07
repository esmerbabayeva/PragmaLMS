using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pragma.Application.Contracts;
using Pragma.Application.DTOs.Modules;
using Pragma.Domain.Enums;

namespace Pragma.API.Controllers;

public class ModulesController : BaseApiController
{
    private readonly IModuleService _moduleService;
    private readonly ICurrentUserService _currentUserService;

    public ModulesController(
        IModuleService moduleService,
        ICurrentUserService currentUserService)
    {
        _moduleService = moduleService;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Kursa yeni tədris modulunun əlavə edilməsi.
    /// </summary>
    [Authorize(Roles = $"{UserRoles.Instructor},{UserRoles.Admin}")]
    [HttpPost("/api/courses/{courseId:guid}/modules")]
    public async Task<ActionResult<Guid>> CreateModule(Guid courseId, [FromBody] CreateModuleDto dto, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue) return Unauthorized();

        var isAdmin = _currentUserService.IsInRole(UserRoles.Admin);
        var result = await _moduleService.CreateModuleAsync(courseId, dto, currentUserId.Value, isAdmin, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Modulun ad və ya təsvirinin redaktəsi.
    /// </summary>
    [Authorize(Roles = $"{UserRoles.Instructor},{UserRoles.Admin}")]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult> UpdateModule(Guid id, [FromBody] UpdateModuleDto dto, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue) return Unauthorized();

        var isAdmin = _currentUserService.IsInRole(UserRoles.Admin);
        var result = await _moduleService.UpdateModuleAsync(id, dto, currentUserId.Value, isAdmin, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Modulun silinməsi (və bağlı dərslərin kaskad silinməsi).
    /// </summary>
    [Authorize(Roles = $"{UserRoles.Instructor},{UserRoles.Admin}")]
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> DeleteModule(Guid id, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue) return Unauthorized();

        var isAdmin = _currentUserService.IsInRole(UserRoles.Admin);
        var result = await _moduleService.DeleteModuleAsync(id, currentUserId.Value, isAdmin, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// SortableJS ilə modul daxilindəki dərslərin ardıcıllığının (OrderIndex) nizamlanması.
    /// </summary>
    [Authorize(Roles = $"{UserRoles.Instructor},{UserRoles.Admin}")]
    [HttpPut("{id:guid}/lessons/reorder")]
    public async Task<ActionResult> ReorderLessons(Guid id, [FromBody] ReorderItemsRequestDto dto, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue) return Unauthorized();

        var isAdmin = _currentUserService.IsInRole(UserRoles.Admin);
        var result = await _moduleService.ReorderLessonsAsync(id, dto, currentUserId.Value, isAdmin, cancellationToken);
        return HandleResult(result);
    }
}
