using Pragma.Domain.Common;

namespace Pragma.Domain.Entities;

/// <summary>
/// Qamifikasiya: Tələbənin gündəlik daxilolma və dərs bitirmə zənciri (Streak) və XP xalları.
/// (20% Fərdi Yaradıcılıq Funksiyası: Pragma Streak).
/// </summary>
public class UserStreak : BaseEntity
{
    public Guid UserId { get; set; }

    /// <summary>
    /// Cari fasiləsiz gün sayı (🔥 Streak).
    /// </summary>
    public int CurrentStreak { get; set; } = 0;

    /// <summary>
    /// Ən uzun qeydə alınmış zəncir rekordı.
    /// </summary>
    public int LongestStreak { get; set; } = 0;

    /// <summary>
    /// Son aktivlik tarixi (gün hesabı üçün).
    /// </summary>
    public DateTime? LastActivityDateUtc { get; set; }

    /// <summary>
    /// Tələbənin topladığı ümumi təcrübə xalları (XP).
    /// </summary>
    public int TotalXp { get; set; } = 0;
}
