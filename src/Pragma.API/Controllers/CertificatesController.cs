using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pragma.Application.Common;
using Pragma.Application.Contracts;
using Pragma.Application.DTOs.Certificates;

namespace Pragma.API.Controllers;

/// <summary>
/// Bitirmə sertifikatlarının generasiyası, PDF endirilməsi və QR yoxlanışı.
/// </summary>
[ApiController]
[Route("api")]
public class CertificatesController : BaseApiController
{
    private readonly ICertificateService _certificateService;

    public CertificatesController(ICertificateService certificateService)
    {
        _certificateService = certificateService;
    }

    /// <summary>
    /// Kursu 100% bitirən tələbə üçün rəsmi sertifikat generasiya edir (Yalnız Student).
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireStudent)]
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
    /// Sertifikatın unikallığını və rəsmiliyini yoxlayır (İctimai açıq endpoint).
    /// </summary>
    [AllowAnonymous]
    [HttpGet("certificates/verify/{code}")]
    public async Task<ActionResult<CertificateVerificationDto>> VerifyCertificate(string code, CancellationToken cancellationToken)
    {
        var result = await _certificateService.VerifyCertificateAsync(code, cancellationToken);
        return HandleResult(result);
    }
}
