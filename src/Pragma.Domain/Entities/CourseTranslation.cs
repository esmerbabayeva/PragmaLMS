using Pragma.Domain.Common;

namespace Pragma.Domain.Entities;

/// <summary>
/// Kurs məlumatlarının çoxdilli tərcüməsi (AZ, EN, RU).
/// </summary>
public class CourseTranslation : BaseEntity
{
    public Guid CourseId { get; set; }
    public Course? Course { get; set; }

    /// <summary>
    /// Dil kodu: "az", "en", "ru".
    /// </summary>
    public string Language { get; set; } = "az";

    public string Title { get; set; } = string.Empty;
    public string? ShortDescription { get; set; }
    public string? Description { get; set; }
}
