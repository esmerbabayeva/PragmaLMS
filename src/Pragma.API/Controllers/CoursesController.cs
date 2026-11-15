using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pragma.Application.Common;
using Pragma.Application.Contracts;
using Pragma.Application.DTOs.Courses;
using Pragma.Application.DTOs.Enrollments;
using Pragma.Application.DTOs.Modules;
using Pragma.Domain.Enums;

namespace Pragma.API.Controllers;

/// <summary>
/// Kurs kataloqu, idarəetmə və qeydiyyat əməliyyatları.
/// </summary>
[Route("api/courses")]
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
    /// Kurs kataloqu: Axtarış, filterlər və paginasiya ilə dərc olunmuş kursların siyahısı.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<CourseSummaryDto>>> GetCourses([FromQuery] CourseFilterDto filter, [FromQuery] string? lang, CancellationToken cancellationToken)
    {
        var cookieLang = Request.Cookies["pragma-lang"];
        var headerLang = Request.Headers["X-Language"].ToString();

        var resolvedLanguage = !string.IsNullOrWhiteSpace(lang)
            ? lang
            : (!string.IsNullOrWhiteSpace(headerLang)
                ? headerLang
                : (!string.IsNullOrWhiteSpace(cookieLang)
                    ? cookieLang
                    : (!string.IsNullOrWhiteSpace(filter.Language)
                        ? filter.Language
                        : Request.Headers.AcceptLanguage.ToString())));

        filter = filter with { Language = resolvedLanguage };

        var result = await _courseService.GetCoursesAsync(filter, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// ID-yə görə kursun detalları, modulları və dərsləri.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CourseDetailDto>> GetCourseById(Guid id, [FromQuery] string? lang, CancellationToken cancellationToken)
    {
        var cookieLang = Request.Cookies["pragma-lang"];
        var headerLang = Request.Headers["X-Language"].ToString();
        var language = !string.IsNullOrWhiteSpace(lang)
            ? lang
            : (!string.IsNullOrWhiteSpace(headerLang)
                ? headerLang
                : (!string.IsNullOrWhiteSpace(cookieLang)
                    ? cookieLang
                    : Request.Headers.AcceptLanguage.ToString()));

        var result = await _courseService.GetCourseByIdAsync(id, language, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// SEO-dostu Slug-a görə kursun detalları.
    /// </summary>
    [HttpGet("by-slug/{slug}")]
    public async Task<ActionResult<CourseDetailDto>> GetCourseBySlug(string slug, [FromQuery] string? lang, CancellationToken cancellationToken)
    {
        var cookieLang = Request.Cookies["pragma-lang"];
        var headerLang = Request.Headers["X-Language"].ToString();
        var language = !string.IsNullOrWhiteSpace(lang)
            ? lang
            : (!string.IsNullOrWhiteSpace(headerLang)
                ? headerLang
                : (!string.IsNullOrWhiteSpace(cookieLang)
                    ? cookieLang
                    : Request.Headers.AcceptLanguage.ToString()));

        var result = await _courseService.GetCourseBySlugAsync(slug, language, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Yeni kurs yaradır (Admin və Moderator üçün).
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.CanManageCourses)]
    [HttpPost]
    public async Task<ActionResult<Guid>> CreateCourse([FromBody] CreateCourseDto dto, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue) return Unauthorized();

        var result = await _courseService.CreateCourseAsync(dto, currentUserId.Value, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Kurs məlumatlarını yeniləyir (Admin və Moderator üçün).
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.CanManageCourses)]
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
    /// Kursu silir (Soft delete, Admin və Moderator üçün).
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.CanManageCourses)]
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
    /// Kursu ictimai kataloqa dərc edir (Status: Published, Admin və Moderator üçün).
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.CanManageCourses)]
    [HttpPut("{id:guid}/publish")]
    public async Task<ActionResult> PublishCourse(Guid id, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue) return Unauthorized();

        var isAdmin = _currentUserService.IsInRole(UserRoles.Admin);
        var result = await _courseService.PublishCourseAsync(id, currentUserId.Value, isAdmin, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Kursu ictimai kataloqdan çıxarıb qaralamaya qaytarır (Status: Draft, Admin və Moderator üçün).
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.CanManageCourses)]
    [HttpPut("{id:guid}/unpublish")]
    public async Task<ActionResult> UnpublishCourse(Guid id, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue) return Unauthorized();

        var isAdmin = _currentUserService.IsInRole(UserRoles.Admin);
        var result = await _courseService.UnpublishCourseAsync(id, currentUserId.Value, isAdmin, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// SortableJS ilə modulların ardıcıllığını (OrderIndex) yenidən nizamlayır (Admin və Moderator üçün).
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.CanManageCourses)]
    [HttpPut("{id:guid}/modules/reorder")]
    public async Task<ActionResult> ReorderModules(Guid id, [FromBody] ReorderItemsRequestDto dto, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue) return Unauthorized();

        var isAdmin = _currentUserService.IsInRole(UserRoles.Admin);
        var result = await _courseService.ReorderModulesAsync(id, dto, currentUserId.Value, isAdmin, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Cari tələbənin bu kursa qeydiyyat statusunu yoxlayır (Nested sub-resource).
    /// </summary>
    [Authorize]
    [HttpGet("{id:guid}/enrollment-status")]
    public async Task<ActionResult<bool>> GetEnrollmentStatus(Guid id, [FromServices] IEnrollmentService enrollmentService, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue) return Ok(false);

        var isEnrolled = await enrollmentService.IsEnrolledAsync(id, currentUserId.Value, cancellationToken);
        return Ok(isEnrolled);
    }

    /// <summary>
    /// Tələbənin bu kursa 1-kliklə qeydiyyatdan keçməsi (Nested sub-resource, Yalnız Student).
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.CanViewOwnCourses)]
    [HttpPost("{id:guid}/enroll")]
    public async Task<ActionResult<EnrollmentDto>> EnrollInCourse(Guid id, [FromServices] IEnrollmentService enrollmentService, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue) return Unauthorized();

        var result = await enrollmentService.EnrollAsync(id, currentUserId.Value, cancellationToken);
        return HandleResult(result);
    }
}
