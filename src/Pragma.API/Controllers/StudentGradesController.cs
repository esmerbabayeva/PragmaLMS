using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pragma.Application.Common;
using Pragma.Application.Contracts;
using Pragma.Application.DTOs.Student;

namespace Pragma.API.Controllers;

/// <summary>
/// Tələbənin fərdi qiymət, davamiyyət və sertifikat statusu API nəzarətçisi ("Qiymətlərim" səhifəsi).
/// </summary>
[Authorize(Policy = AuthorizationPolicies.CanViewOwnGrades)]
[ApiController]
[Route("api/student/my-grades")]
public class StudentGradesController : BaseApiController
{
    private readonly IStudentGradeService _studentGradeService;
    private readonly ICurrentUserService _currentUserService;

    public StudentGradesController(
        IStudentGradeService studentGradeService,
        ICurrentUserService currentUserService)
    {
        _studentGradeService = studentGradeService;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Cari daxil olmuş tələbənin qeydiyyatda olduğu kohortların (qrupların) siyahısını qaytarır.
    /// </summary>
    [HttpGet("cohorts")]
    public async Task<ActionResult<IReadOnlyCollection<StudentEnrolledCohortDto>>> GetMyEnrolledCohorts(CancellationToken ct)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue) return Unauthorized();

        var result = await _studentGradeService.GetMyEnrolledCohortsAsync(currentUserId.Value, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Tələbənin seçilmiş kohort üzrə tam akademik göstəricilərini (qiymətlər, davamiyyət, rəylər və sertifikat vəziyyəti) qaytarır.
    /// Tələbə kohortda deyilsə 404 qaytarılır. Şəxsi qeydlər (Private notes) heç vaxt qaytarılmır.
    /// </summary>
    [HttpGet("cohorts/{cohortId:guid}")]
    public async Task<ActionResult<StudentCohortGradeDetailsDto>> GetMyCohortGradeDetails(Guid cohortId, CancellationToken ct)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue) return Unauthorized();

        var result = await _studentGradeService.GetMyCohortGradeDetailsAsync(cohortId, currentUserId.Value, ct);
        return HandleResult(result);
    }
}
