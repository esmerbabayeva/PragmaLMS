using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pragma.Application.Contracts;
using Pragma.Application.DTOs.Enrollments;

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
    [HttpGet("my-courses")]
    public async Task<ActionResult<IReadOnlyCollection<EnrollmentDto>>> GetMyEnrollments(CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue) return Unauthorized();

        var enrollments = await _enrollmentService.GetMyEnrollmentsAsync(currentUserId.Value, cancellationToken);
        return Ok(enrollments);
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
