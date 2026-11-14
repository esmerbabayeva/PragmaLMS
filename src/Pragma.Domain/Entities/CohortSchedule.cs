using Pragma.Domain.Common;

namespace Pragma.Domain.Entities;

/// <summary>
/// Qrupun həftəlik dərs cədvəli (məs: Bazar ertəsi 19:00 - 21:00).
/// </summary>
public class CohortSchedule : BaseEntity
{
    public Guid CohortId { get; set; }
    public Cohort? Cohort { get; set; }

    public DayOfWeek DayOfWeek { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }

    /// <summary>
    /// Otaq nömrəsi və ya online keçirildikdə Jitsi / Meet keçidi.
    /// </summary>
    public string? RoomOrMeetingUrl { get; set; }
}
