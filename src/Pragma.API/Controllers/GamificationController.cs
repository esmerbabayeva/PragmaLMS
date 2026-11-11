using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pragma.Application.Common;
using Pragma.Application.Contracts;
using Pragma.Application.DTOs.Gamification;

namespace Pragma.API.Controllers;

/// <summary>
/// Qamifikasiya: Ardıcıl öyrənmə zəncirləri (Streak 🔥), nailiyyət nişanları və liderlər cədvəli.
/// </summary>
[Authorize]
[ApiController]
[Route("api/gamification")]
public class GamificationController : BaseApiController
{
    private readonly IGamificationService _gamificationService;

    public GamificationController(IGamificationService gamificationService)
    {
        _gamificationService = gamificationService;
    }

    /// <summary>
    /// Cari tələbənin öyrənmə statistikası, aktiv zənciri (Streak) və qazandığı nişanlar (Yalnız Student).
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireStudent)]
    [HttpGet("my-stats")]
    public async Task<ActionResult<UserGamificationStatsDto>> GetMyStats(CancellationToken cancellationToken)
    {
        var result = await _gamificationService.GetMyStatsAsync(cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Ən çox xal (XP) və aktiv öyrənmə zənciri olan tələbələrin liderlik cədvəli (Top 10).
    /// </summary>
    [AllowAnonymous]
    [HttpGet("leaderboard")]
    public async Task<ActionResult<IReadOnlyList<LeaderboardEntryDto>>> GetLeaderboard(
        [FromQuery] int top = 10,
        CancellationToken cancellationToken = default)
    {
        var result = await _gamificationService.GetLeaderboardAsync(top, cancellationToken);
        return HandleResult(result);
    }
}
