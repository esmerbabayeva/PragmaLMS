using Pragma.Domain.Common;

namespace Pragma.Domain.Entities;

/// <summary>
/// Kursların təsnifatı üçün kateqoriya modeli.
/// </summary>
public class Category : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? IconName { get; set; }
    public int SortOrder { get; set; } = 0;

    // Naviqasiya xassələri
    public ICollection<CategoryTranslation> Translations { get; set; } = new List<CategoryTranslation>();
    public ICollection<Course> Courses { get; set; } = new List<Course>();
}
