using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pragma.Application.Common;
using Pragma.Application.Contracts;
using Pragma.Application.DTOs.Progress;

namespace Pragma.API.Controllers;

/// <summary>
/// Video izlənmə davamiyyəti, 90% avto-tamamlama və kursa qaldığı yerdən davametmə (Yalnız Student).
/// </summary>
[Authorize(Policy = AuthorizationPolicies.CanTrackProgress)]
[ApiController]
[Route("api")]
public class ProgressController : BaseApiController
{
    private readonly ILearningProgressService _progressService;

    public ProgressController(ILearningProgressService progressService)
    {
        _progressService = progressService;
    }

    /// <summary>
    /// Cari baxış saniyəsini yeniləyir və 90% çatdıqda dərsi avtomatik tamamlayır.
    /// </summary>
    [HttpPut("lessons/{id:guid}/video-position")]
    public async Task<IActionResult> UpdateVideoPosition(
        Guid id,
        [FromBody] UpdateVideoPositionRequestDto dto,
        CancellationToken ct)
    {
        var result = await _progressService.UpdateVideoPositionAsync(id, dto, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Dərsin tamamlanma vəziyyətini əllə dəyişir (Toggle Complete / Incomplete).
    /// </summary>
    [HttpPost("lessons/{id:guid}/toggle-complete")]
    public async Task<IActionResult> ToggleComplete(Guid id, CancellationToken ct)
    {
        var result = await _progressService.ToggleLessonCompleteAsync(id, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Tələbənin kursda ən son qaldığı dərsi və dəqiq saniyəsini qaytarır (Resume Course).
    /// </summary>
    [HttpGet("courses/{id:guid}/resume")]
    public async Task<IActionResult> GetResumeState(Guid id, CancellationToken ct)
    {
        var result = await _progressService.GetResumeStateAsync(id, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Tələbənin bu kurs üzrə ümumi faizini və bitirdiyi dərslərin siyahısını qaytarır.
    /// </summary>
    [HttpGet("courses/{id:guid}/progress")]
    public async Task<IActionResult> GetCourseProgress(Guid id, CancellationToken ct)
    {
        var result = await _progressService.GetCourseProgressAsync(id, ct);
        return HandleResult(result);
    }
}
