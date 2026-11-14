using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Pragma.Application.Common;
using Pragma.Application.Contracts;
using Pragma.Application.DTOs.Certificates;

namespace Pragma.API.Controllers;

/// <summary>
/// Bitirmə sertifikatlarının generasiyası, kohort uyğunluğu, PDF endirilməsi, QR/kod doğrulanması və ləğv edilməsi.
/// </summary>
[ApiController]
[Route("api")]
public class CertificatesController : BaseApiController
{
    private readonly ICertificateService _certificateService;
    private readonly ICurrentUserService _currentUserService;

    public CertificatesController(
        ICertificateService certificateService,
        ICurrentUserService currentUserService)
    {
        _certificateService = certificateService;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Self-paced kursu 100% bitirən tələbə üçün rəsmi sertifikat gətirir və ya generasiya edir (Yalnız Student).
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireStudent)]
    [HttpGet("courses/{courseId:guid}/certificate")]
    [HttpPost("courses/{courseId:guid}/certificate")]
    public async Task<ActionResult<CertificateDto>> GenerateCertificate(Guid courseId, CancellationToken cancellationToken)
    {
        var result = await _certificateService.GenerateCertificateAsync(courseId, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Rəsmi PDF sertifikatı endirir (QuestPDF və QR kodlu, Yalnız Student).
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireStudent)]
    [HttpGet("courses/{courseId:guid}/certificate/download")]
    public async Task<IActionResult> DownloadCertificatePdf(Guid courseId, CancellationToken cancellationToken)
    {
        var result = await _certificateService.DownloadCertificatePdfAsync(courseId, cancellationToken);
        if (result.IsFailure)
            return HandleResult(result);

        var fileResult = result.Value;
        return File(fileResult.Content, fileResult.ContentType, fileResult.FileName);
    }

    /// <summary>
    /// Sertifikatın unikallığını və rəsmiliyini yoxlayır (İctimai açıq endpoint, rate limited).
    /// </summary>
    [AllowAnonymous]
    [EnableRateLimiting("verify")]
    [HttpGet("certificates/verify/{code}")]
    public async Task<ActionResult<CertificateVerificationDto>> VerifyCertificate(string code, CancellationToken cancellationToken)
    {
        var result = await _certificateService.VerifyCertificateAsync(code, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Kohort tələbələrinin sertifikat uyğunluq siyahısını gətirir (Admin və ya müvafiq Instructor).
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.CanManageCohorts)]
    [HttpGet("certificates/cohorts/{cohortId:guid}/eligibility")]
    public async Task<ActionResult<IReadOnlyCollection<CohortStudentEligibilityDto>>> GetCohortEligibility(
        Guid cohortId,
        CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId ?? Guid.Empty;
        var isAdmin = _currentUserService.IsInRole("Admin");
        var result = await _certificateService.GetCohortCertificateEligibilityAsync(cohortId, userId, isAdmin, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Kohort bitdikdə uyğun tələbələrə rəsmi sertifikatları toplu generasiya edir (Admin və ya müvafiq Instructor).
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.CanManageCohorts)]
    [HttpPost("certificates/cohorts/{cohortId:guid}/issue")]
    public async Task<ActionResult<int>> IssueCohortCertificates(
        Guid cohortId,
        CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId ?? Guid.Empty;
        var isAdmin = _currentUserService.IsInRole("Admin");
        var result = await _certificateService.IssueCohortCertificatesAsync(cohortId, userId, isAdmin, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Saxtakarlıq və ya inzibati səbəbdən sertifikatı ləğv edir (Yalnız Admin).
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireAdmin)]
    [HttpPost("certificates/{code}/revoke")]
    public async Task<ActionResult<bool>> RevokeCertificate(
        string code,
        [FromBody] RevokeCertificateDto dto,
        CancellationToken cancellationToken)
    {
        var adminId = _currentUserService.UserId ?? Guid.Empty;
        var result = await _certificateService.RevokeCertificateAsync(code, dto.Reason, adminId, cancellationToken);
        return HandleResult(result);
    }
}
