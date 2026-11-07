namespace Pragma.Application.DTOs.Gamification;

public record UserBadgeDto(
    string BadgeCode,
    string BadgeName,
    string Description,
    string? IconName,
    DateTime AwardedAtUtc
);

public record UserGamificationStatsDto(
    int CurrentStreak,
    int LongestStreak,
    int TotalXp,
    int TotalCompletedLessons,
    int TotalPassedQuizzes,
    List<UserBadgeDto> Badges
);

public record LeaderboardEntryDto(
    int Rank,
    Guid UserId,
    string FullName,
    int CurrentStreak,
    int TotalXp,
    int BadgesCount
);
