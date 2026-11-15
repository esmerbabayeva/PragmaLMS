using Pragma.Domain.Common;

namespace Pragma.Domain.Entities;

public class Announcement : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public Guid AuthorId { get; set; }

    /// <summary>
    /// Əgər null olarsa, qlobal sistem elanıdır (bütün tələbə və müəllimlər üçün).
    /// Dolu olarsa, yalnız həmin kohortun üzvləri üçün elandır.
    /// </summary>
    public Guid? CohortId { get; set; }
    public Cohort? Cohort { get; set; }

    public bool IsPinned { get; set; } = false;
}
