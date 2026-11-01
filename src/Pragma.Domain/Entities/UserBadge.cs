using Pragma.Domain.Common;

namespace Pragma.Domain.Entities;

/// <summary>
/// Qamifikasiya: Tələbənin qazandığı xüsusi nailiyyət nişanları (Badges).
/// Məsələn: "FIRST_LESSON", "STREAK_7", "QUIZ_MASTER", "NIGHT_OWL".
/// </summary>
public class UserBadge : BaseEntity
{
    public Guid UserId { get; set; }

    public string BadgeCode { get; set; } = string.Empty;
    public string BadgeName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? IconName { get; set; }

    public DateTime AwardedAtUtc { get; set; } = DateTime.UtcNow;
}
