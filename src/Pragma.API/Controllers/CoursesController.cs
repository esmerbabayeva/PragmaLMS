using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pragma.Application.Common;
using Pragma.Application.Contracts;
using Pragma.Application.DTOs.Courses;
using Pragma.Application.DTOs.Modules;
using Pragma.Domain.Enums;

namespace Pragma.API.Controllers;

public class CoursesController : BaseApiController
{
    private readonly ICourseService _courseService;
    private readonly ICurrentUserService _currentUserService;

    public CoursesController(
        ICourseService courseService,
        ICurrentUserService currentUserService)
    {
        _courseService = courseService;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Kurs kataloqu: Axtarış, kateqoriya, səviyyə və qiymət filterləri ilə səhifələnmiş siyahı.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<CourseSummaryDto>>> GetCourses([FromQuery] CourseFilterDto filter, CancellationToken cancellationToken)
    {
        var result = await _courseService.GetCoursesAsync(filter, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// ID-yə görə kursun detalları, modulları və dərsləri.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CourseDetailDto>> GetCourseById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _courseService.GetCourseByIdAsync(id, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Slug-a görə kursun detalları (SEO dostu URL üçün).
    /// </summary>
    [HttpGet("by-slug/{slug}")]
    public async Task<ActionResult<CourseDetailDto>> GetCourseBySlug(string slug, CancellationToken cancellationToken)
    {
        var result = await _courseService.GetCourseBySlugAsync(slug, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Yeni kurs yaradılması (Təlimçi və ya Admin).
    /// </summary>
    [Authorize(Roles = $"{UserRoles.Instructor},{UserRoles.Admin}")]
    [HttpPost]
    public async Task<ActionResult<Guid>> CreateCourse([FromBody] CreateCourseDto dto, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue)
        {
            return Unauthorized();
        }

        var result = await _courseService.CreateCourseAsync(dto, currentUserId.Value, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Kurs məlumatlarının redaktə edilməsi (Müəllim yalnız öz kursunu redaktə edə bilər).
    /// </summary>
    [Authorize(Roles = $"{UserRoles.Instructor},{UserRoles.Admin}")]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult> UpdateCourse(Guid id, [FromBody] UpdateCourseDto dto, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue) return Unauthorized();

        var isAdmin = _currentUserService.IsInRole(UserRoles.Admin);
        var result = await _courseService.UpdateCourseAsync(id, dto, currentUserId.Value, isAdmin, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Kursun silinməsi (Soft delete).
    /// </summary>
    [Authorize(Roles = $"{UserRoles.Instructor},{UserRoles.Admin}")]
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> DeleteCourse(Guid id, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue) return Unauthorized();

        var isAdmin = _currentUserService.IsInRole(UserRoles.Admin);
        var result = await _courseService.DeleteCourseAsync(id, currentUserId.Value, isAdmin, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// SortableJS ilə modulların ardıcıllığının (OrderIndex) yenidən nizamlanması.
    /// </summary>
    [Authorize(Roles = $"{UserRoles.Instructor},{UserRoles.Admin}")]
    [HttpPut("{id:guid}/modules/reorder")]
    public async Task<ActionResult> ReorderModules(Guid id, [FromBody] ReorderItemsRequestDto dto, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue) return Unauthorized();

        var isAdmin = _currentUserService.IsInRole(UserRoles.Admin);
        var result = await _courseService.ReorderModulesAsync(id, dto, currentUserId.Value, isAdmin, cancellationToken);
        return HandleResult(result);
    }
}
