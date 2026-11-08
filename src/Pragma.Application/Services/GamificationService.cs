using Microsoft.EntityFrameworkCore;
using Pragma.Application.Common;
using Pragma.Application.Contracts;
using Pragma.Application.DTOs.Gamification;
using Pragma.Domain.Entities;

namespace Pragma.Application.Services;

public class GamificationService : IGamificationService
{
    private readonly IAppDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserLookupService _userLookup;

    public GamificationService(
        IAppDbContext context,
        ICurrentUserService currentUserService,
        IUserLookupService userLookup)
    {
        _context = context;
        _currentUserService = currentUserService;
        _userLookup = userLookup;
    }

    public async Task RecordActivityAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var streak = await _context.UserStreaks
            .FirstOrDefaultAsync(s => s.UserId == userId && !s.IsDeleted, cancellationToken);

        var today = DateTime.UtcNow.Date;

        if (streak == null)
        {
            streak = new UserStreak
            {
                UserId = userId,
                CurrentStreak = 1,
                LongestStreak = 1,
                TotalXp = 20,
                LastActivityDateUtc = DateTime.UtcNow
            };
            _context.UserStreaks.Add(streak);
        }
        else
        {
            if (streak.LastActivityDateUtc.HasValue)
            {
                var lastDate = streak.LastActivityDateUtc.Value.Date;
                var diff = (today - lastDate).TotalDays;

                if (diff == 1)
                {
                    // Növbəti ardıcıl gün! Streak artır
                    streak.CurrentStreak += 1;
                    if (streak.CurrentStreak > streak.LongestStreak)
                    {
                        streak.LongestStreak = streak.CurrentStreak;
                    }
                    streak.TotalXp += 25; // Ardıcıl gün bonusu
                }
                else if (diff > 1)
                {
                    // Zəncir qırıldı, yenidən 1-dən başlayır
                    streak.CurrentStreak = 1;
                    streak.TotalXp += 10;
                }
                else
                {
                    // Eyni gün daxilində fəaliyyət
                    streak.TotalXp += 5;
                }
            }
            else
            {
                streak.CurrentStreak = 1;
                streak.LongestStreak = 1;
                streak.TotalXp += 10;
            }

            streak.LastActivityDateUtc = DateTime.UtcNow;
            streak.MarkUpdated();
        }

        await _context.SaveChangesAsync(cancellationToken);
        await CheckAndAwardMilestonesAsync(userId, cancellationToken);
    }

    public async Task CheckAndAwardMilestonesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var existingBadges = await _context.UserBadges
            .AsNoTracking()
            .Where(b => b.UserId == userId && !b.IsDeleted)
            .Select(b => b.BadgeCode)
            .ToListAsync(cancellationToken);

        var existingSet = new HashSet<string>(existingBadges);
        var newBadges = new List<UserBadge>();

        // 1. Nailiyyət: "FIRST_STEP" (İlk dərs tamamlandıqda)
        if (!existingSet.Contains("FIRST_STEP"))
        {
            var hasCompletedLesson = await _context.LessonProgresses
                .AnyAsync(p => p.StudentId == userId && p.IsCompleted && !p.IsDeleted, cancellationToken);

            if (hasCompletedLesson)
            {
                newBadges.Add(new UserBadge
                {
                    UserId = userId,
                    BadgeCode = "FIRST_STEP",
                    BadgeName = "İlk Addım 🚀",
                    Description = "Platformada ilk dərsinizi uğurla tamamladınız!",
                    IconName = "rocket-launch"
                });
            }
        }

        // 2. Nailiyyət: "STREAK_3" (3 gün ardıcıl öyrəndikdə 🔥)
        if (!existingSet.Contains("STREAK_3"))
        {
            var streak = await _context.UserStreaks
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.UserId == userId && !s.IsDeleted, cancellationToken);

            if (streak != null && streak.CurrentStreak >= 3)
            {
                newBadges.Add(new UserBadge
                {
                    UserId = userId,
                    BadgeCode = "STREAK_3",
                    BadgeName = "Alovlu Zəncir 🔥",
                    Description = "Fasiləsiz 3 gün ardıcıl öyrənmə zənciri qurdunuz!",
                    IconName = "fire"
                });
            }
        }

        // 3. Nailiyyət: "QUIZ_MASTER" (Kvizdən 100% topladıqda 🏆)
        if (!existingSet.Contains("QUIZ_MASTER"))
        {
            var hasPerfectQuiz = await _context.QuizAttempts
                .AnyAsync(a => a.StudentId == userId && a.ScorePercentage >= 100.0 && !a.IsDeleted, cancellationToken);

            if (hasPerfectQuiz)
            {
                newBadges.Add(new UserBadge
                {
                    UserId = userId,
                    BadgeCode = "QUIZ_MASTER",
                    BadgeName = "Kviz Çempionu 🏆",
                    Description = "İmtahan sınağında 100% mükəmməl nəticə göstərdiniz!",
                    IconName = "trophy"
                });
            }
        }

        // 4. Nailiyyət: "GRADUATE" (Kursu 100% bitirib sertifikat qazandıqda 🎓)
        if (!existingSet.Contains("GRADUATE"))
        {
            var hasFinishedCourse = await _context.Enrollments
                .AnyAsync(e => e.StudentId == userId && e.IsCompleted && !e.IsDeleted, cancellationToken);

            if (hasFinishedCourse)
            {
                newBadges.Add(new UserBadge
                {
                    UserId = userId,
                    BadgeCode = "GRADUATE",
                    BadgeName = "Pragma Məzunu 🎓",
                    Description = "Bütöv bir peşəkar kurs proqramını 100% başa vurdunuz!",
                    IconName = "academic-cap"
                });
            }
        }

        if (newBadges.Count > 0)
        {
            _context.UserBadges.AddRange(newBadges);

            // Hər nişan üçün +50 XP bonusu
            var streak = await _context.UserStreaks
                .FirstOrDefaultAsync(s => s.UserId == userId && !s.IsDeleted, cancellationToken);

            if (streak != null)
            {
                streak.TotalXp += newBadges.Count * 50;
                streak.MarkUpdated();
            }

            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<Result<UserGamificationStatsDto>> GetMyStatsAsync(CancellationToken cancellationToken = default)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue)
            return Result<UserGamificationStatsDto>.Failure(Error.Unauthorized("İstifadəçi autentifikasiyadan keçməyib."));

        var streak = await _context.UserStreaks
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == currentUserId.Value && !s.IsDeleted, cancellationToken);

        if (streak == null)
        {
            await RecordActivityAsync(currentUserId.Value, cancellationToken);
            streak = await _context.UserStreaks
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.UserId == currentUserId.Value && !s.IsDeleted, cancellationToken);
        }

        var badges = await _context.UserBadges
            .AsNoTracking()
            .Where(b => b.UserId == currentUserId.Value && !b.IsDeleted)
            .OrderByDescending(b => b.AwardedAtUtc)
            .Select(b => new UserBadgeDto(b.BadgeCode, b.BadgeName, b.Description, b.IconName, b.AwardedAtUtc))
            .ToListAsync(cancellationToken);

        var completedLessons = await _context.LessonProgresses
            .CountAsync(p => p.StudentId == currentUserId.Value && p.IsCompleted && !p.IsDeleted, cancellationToken);

        var passedQuizzes = await _context.QuizAttempts
            .CountAsync(a => a.StudentId == currentUserId.Value && a.IsPassed && !a.IsDeleted, cancellationToken);

        return Result<UserGamificationStatsDto>.Success(new UserGamificationStatsDto(
            CurrentStreak: streak?.CurrentStreak ?? 0,
            LongestStreak: streak?.LongestStreak ?? 0,
            TotalXp: streak?.TotalXp ?? 0,
            TotalCompletedLessons: completedLessons,
            TotalPassedQuizzes: passedQuizzes,
            Badges: badges
        ));
    }

    public async Task<Result<IReadOnlyList<LeaderboardEntryDto>>> GetLeaderboardAsync(int top = 10, CancellationToken cancellationToken = default)
    {
        var streaks = await _context.UserStreaks
            .AsNoTracking()
            .Where(s => !s.IsDeleted)
            .OrderByDescending(s => s.TotalXp)
            .ThenByDescending(s => s.CurrentStreak)
            .Take(top)
            .ToListAsync(cancellationToken);

        var userIds = streaks.Select(s => s.UserId).ToList();
        var userDict = await _userLookup.GetUsersByIdsAsync(userIds, cancellationToken);

        var badgeCounts = await _context.UserBadges
            .AsNoTracking()
            .Where(b => userIds.Contains(b.UserId) && !b.IsDeleted)
            .GroupBy(b => b.UserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.UserId, g => g.Count, cancellationToken);

        var leaderboard = new List<LeaderboardEntryDto>();
        for (int i = 0; i < streaks.Count; i++)
        {
            var s = streaks[i];
            var fullName = userDict.TryGetValue(s.UserId, out var u) ? u.FullName : "Tələbə";
            var bCount = badgeCounts.TryGetValue(s.UserId, out var cnt) ? cnt : 0;

            leaderboard.Add(new LeaderboardEntryDto(
                Rank: i + 1,
                UserId: s.UserId,
                FullName: fullName,
                CurrentStreak: s.CurrentStreak,
                TotalXp: s.TotalXp,
                BadgesCount: bCount
            ));
        }

        return Result<IReadOnlyList<LeaderboardEntryDto>>.Success(leaderboard);
    }
}
