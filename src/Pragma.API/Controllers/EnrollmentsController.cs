using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pragma.Application.Common;
using Pragma.Application.Contracts;
using Pragma.Application.DTOs.Enrollments;
using Pragma.Domain.Enums;

namespace Pragma.API.Controllers;

/// <summary>
/// Kurs qeydiyyatı və tələbənin kursları əməliyyatları.
/// </summary>
[Authorize]
[Route("api/enrollments")]
public class EnrollmentsController : BaseApiController
{
    private readonly IEnrollmentService _enrollmentService;
    private readonly ICurrentUserService _currentUserService;

    public EnrollmentsController(
        IEnrollmentService enrollmentService,
        ICurrentUserService currentUserService)
    {
        _enrollmentService = enrollmentService;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Kursa 1-kliklə qeydiyyatdan keçir (Enrollment).
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<EnrollmentDto>> Enroll([FromBody] EnrollCourseDto dto, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue) return Unauthorized();

        var result = await _enrollmentService.EnrollAsync(dto.CourseId, currentUserId.Value, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Cari tələbənin qeydiyyatdan keçdiyi bütün kursları və tərəqqi faizlərini qaytarır.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.CanViewOwnCourses)]
    [HttpGet("my-courses")]
    [HttpGet("/api/courses/my-courses")]
    public async Task<ActionResult<IReadOnlyCollection<EnrollmentDto>>> GetMyEnrollments([FromQuery] string? lang, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue) return Unauthorized();

        var language = !string.IsNullOrWhiteSpace(lang) ? lang : Request.Headers.AcceptLanguage.ToString();
        var enrollments = await _enrollmentService.GetMyEnrollmentsAsync(currentUserId.Value, language, cancellationToken);
        return Ok(enrollments);
    }

    /// <summary>
    /// Kursa tələbə əlavə edir (Admin hər kursa, Instructor yalnız öz kursuna).
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.CanManageEnrollments)]
    [HttpPost("manage/add-student")]
    public async Task<ActionResult<EnrollmentDto>> AddStudentToCourse([FromBody] ManageEnrollmentDto dto, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue) return Unauthorized();

        var isAdmin = _currentUserService.IsInRole(UserRoles.Admin);
        var result = await _enrollmentService.ManageEnrollStudentAsync(dto.CourseId, dto.StudentId, currentUserId.Value, isAdmin, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Kursdan tələbəni çıxarır (Admin hər kursdan, Instructor yalnız öz kursundan).
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.CanManageEnrollments)]
    [HttpPost("manage/remove-student")]
    public async Task<ActionResult> RemoveStudentFromCourse([FromBody] ManageEnrollmentDto dto, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue) return Unauthorized();

        var isAdmin = _currentUserService.IsInRole(UserRoles.Admin);
        var result = await _enrollmentService.ManageUnenrollStudentAsync(dto.CourseId, dto.StudentId, currentUserId.Value, isAdmin, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Konkret kursa qeydiyyat statusunu yoxlayır (Query parameter üsulu).
    /// </summary>
    [HttpGet("check")]
    public async Task<ActionResult<bool>> CheckEnrollment([FromQuery] Guid courseId, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue) return Ok(false);

        var isEnrolled = await _enrollmentService.IsEnrolledAsync(courseId, currentUserId.Value, cancellationToken);
        return Ok(isEnrolled);
    }
}
