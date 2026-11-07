using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pragma.Application.Contracts;
using Pragma.Application.DTOs.LiveSession;

namespace Pragma.API.Controllers;

/// <summary>
/// Dərslər üzrə Jitsi Meet canlı video konfrans otaqlarının generasiyası.
/// </summary>
[Authorize]
[ApiController]
[Route("api")]
public class LiveSessionController : BaseApiController
{
    private readonly ILiveSessionService _liveSessionService;

    public LiveSessionController(ILiveSessionService liveSessionService)
    {
        _liveSessionService = liveSessionService;
    }

    /// <summary>
    /// Dərs üçün təhlükəsiz və unikal canlı yayım otağı linkini qaytarır.
    /// </summary>
    [HttpGet("lessons/{id:guid}/live-session")]
    public async Task<ActionResult<LiveSessionDto>> GetLiveSession(Guid id, CancellationToken cancellationToken)
    {
        var result = await _liveSessionService.GetLessonLiveSessionAsync(id, cancellationToken);
        return HandleResult(result);
    }
}
