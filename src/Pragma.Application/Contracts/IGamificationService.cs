using Pragma.Application.Common;
using Pragma.Application.DTOs.Gamification;

namespace Pragma.Application.Contracts;

public interface IGamificationService
{
    Task RecordActivityAsync(Guid userId, CancellationToken cancellationToken = default);
    Task CheckAndAwardMilestonesAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Result<UserGamificationStatsDto>> GetMyStatsAsync(CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<LeaderboardEntryDto>>> GetLeaderboardAsync(int top = 10, CancellationToken cancellationToken = default);
}
