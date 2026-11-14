using Pragma.Domain.Entities;

namespace Pragma.Application.Common;

/// <summary>
/// Çoxdilli məzmun üçün dil normallaşdırması və fallback həlli.
/// Fallback sırası: Seçilmiş dil -> "az" -> İlk mövcud tərcümə -> Əsas entity sahəsi.
/// </summary>
public static class LanguageResolutionHelper
{
    public const string DefaultLanguage = "az";
    public static readonly string[] SupportedLanguages = ["az", "en", "ru"];

    /// <summary>
    /// Accept-Language header-i və ya query parametrindən "az", "en", "ru" kodunu çıxarır.
    /// </summary>
    public static string NormalizeLanguage(string? rawLanguage)
    {
        if (string.IsNullOrWhiteSpace(rawLanguage))
            return DefaultLanguage;

        // Çoxlu dillər ("en-US,en;q=0.9,az;q=0.8,ru;q=0.7") vergüllə bölünür
        var candidates = rawLanguage.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var candidate in candidates)
        {
            // Keyfiyyət parametrini (q=...) təmizlə
            var tag = candidate.Split(';', StringSplitOptions.RemoveEmptyEntries)[0].Trim().ToLowerInvariant();

            // Regional kodu ("en-US" -> "en", "az-Latn-AZ" -> "az") çıxar
            var primaryLang = tag.Split('-', '_')[0];

            if (primaryLang == "az") return "az";
            if (primaryLang == "en") return "en";
            if (primaryLang == "ru") return "ru";
        }

        return DefaultLanguage;
    }

    /// <summary>
    /// Kurs üçün seçilmiş dildə (və ya fallback) başlığı qaytarır.
    /// </summary>
    public static string ResolveTitle(Course course, string language)
    {
        var lang = NormalizeLanguage(language);

        var match = course.Translations.FirstOrDefault(t => t.Language.Equals(lang, StringComparison.OrdinalIgnoreCase));
        if (match != null && !string.IsNullOrWhiteSpace(match.Title))
            return match.Title;

        var azMatch = course.Translations.FirstOrDefault(t => t.Language.Equals(DefaultLanguage, StringComparison.OrdinalIgnoreCase));
        if (azMatch != null && !string.IsNullOrWhiteSpace(azMatch.Title))
            return azMatch.Title;

        var anyMatch = course.Translations.FirstOrDefault(t => !string.IsNullOrWhiteSpace(t.Title));
        if (anyMatch != null)
            return anyMatch.Title;

        return course.Title;
    }

    /// <summary>
    /// Kurs üçün seçilmiş dildə (və ya fallback) qısa təsviri qaytarır.
    /// </summary>
    public static string? ResolveShortDescription(Course course, string language)
    {
        var lang = NormalizeLanguage(language);

        var match = course.Translations.FirstOrDefault(t => t.Language.Equals(lang, StringComparison.OrdinalIgnoreCase));
        if (match != null && !string.IsNullOrWhiteSpace(match.ShortDescription))
            return match.ShortDescription;

        var azMatch = course.Translations.FirstOrDefault(t => t.Language.Equals(DefaultLanguage, StringComparison.OrdinalIgnoreCase));
        if (azMatch != null && !string.IsNullOrWhiteSpace(azMatch.ShortDescription))
            return azMatch.ShortDescription;

        var anyMatch = course.Translations.FirstOrDefault(t => !string.IsNullOrWhiteSpace(t.ShortDescription));
        if (anyMatch != null)
            return anyMatch.ShortDescription;

        return course.ShortDescription;
    }

    /// <summary>
    /// Kurs üçün seçilmiş dildə (və ya fallback) tam təsviri qaytarır.
    /// </summary>
    public static string? ResolveDescription(Course course, string language)
    {
        var lang = NormalizeLanguage(language);

        var match = course.Translations.FirstOrDefault(t => t.Language.Equals(lang, StringComparison.OrdinalIgnoreCase));
        if (match != null && !string.IsNullOrWhiteSpace(match.Description))
            return match.Description;

        var azMatch = course.Translations.FirstOrDefault(t => t.Language.Equals(DefaultLanguage, StringComparison.OrdinalIgnoreCase));
        if (azMatch != null && !string.IsNullOrWhiteSpace(azMatch.Description))
            return azMatch.Description;

        var anyMatch = course.Translations.FirstOrDefault(t => !string.IsNullOrWhiteSpace(t.Description));
        if (anyMatch != null)
            return anyMatch.Description;

        return course.Description;
    }

    /// <summary>
    /// Kateqoriya üçün seçilmiş dildə (və ya fallback) adı qaytarır.
    /// </summary>
    public static string ResolveCategoryName(Category? category, string language)
    {
        if (category == null) return "Ümumi";

        var lang = NormalizeLanguage(language);

        var match = category.Translations.FirstOrDefault(t => t.Language.Equals(lang, StringComparison.OrdinalIgnoreCase));
        if (match != null && !string.IsNullOrWhiteSpace(match.Name))
            return match.Name;

        var azMatch = category.Translations.FirstOrDefault(t => t.Language.Equals(DefaultLanguage, StringComparison.OrdinalIgnoreCase));
        if (azMatch != null && !string.IsNullOrWhiteSpace(azMatch.Name))
            return azMatch.Name;

        var anyMatch = category.Translations.FirstOrDefault(t => !string.IsNullOrWhiteSpace(t.Name));
        if (anyMatch != null)
            return anyMatch.Name;

        return category.Name;
    }

    /// <summary>
    /// Kateqoriya üçün seçilmiş dildə (və ya fallback) təsviri qaytarır.
    /// </summary>
    public static string? ResolveCategoryDescription(Category? category, string language)
    {
        if (category == null) return null;

        var lang = NormalizeLanguage(language);

        var match = category.Translations.FirstOrDefault(t => t.Language.Equals(lang, StringComparison.OrdinalIgnoreCase));
        if (match != null && !string.IsNullOrWhiteSpace(match.Description))
            return match.Description;

        var azMatch = category.Translations.FirstOrDefault(t => t.Language.Equals(DefaultLanguage, StringComparison.OrdinalIgnoreCase));
        if (azMatch != null && !string.IsNullOrWhiteSpace(azMatch.Description))
            return azMatch.Description;

        var anyMatch = category.Translations.FirstOrDefault(t => !string.IsNullOrWhiteSpace(t.Description));
        if (anyMatch != null)
            return anyMatch.Description;

        return category.Description;
    }
}
