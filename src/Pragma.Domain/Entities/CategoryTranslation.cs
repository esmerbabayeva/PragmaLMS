using Pragma.Domain.Common;

namespace Pragma.Domain.Entities;

/// <summary>
/// Kateqoriya məlumatlarının çoxdilli tərcüməsi (AZ, EN, RU).
/// </summary>
public class CategoryTranslation : BaseEntity
{
    public Guid CategoryId { get; set; }
    public Category? Category { get; set; }

    /// <summary>
    /// Dil kodu: "az", "en", "ru".
    /// </summary>
    public string Language { get; set; } = "az";

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}
