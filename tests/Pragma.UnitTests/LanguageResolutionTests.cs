using Pragma.Application.Common;
using Pragma.Domain.Entities;
using Xunit;

namespace Pragma.UnitTests;

public class LanguageResolutionTests
{
    [Theory]
    [InlineData("az", "az")]
    [InlineData("AZ", "az")]
    [InlineData("az-AZ", "az")]
    [InlineData("en", "en")]
    [InlineData("EN", "en")]
    [InlineData("en-US", "en")]
    [InlineData("en-GB,en;q=0.9", "en")]
    [InlineData("ru", "ru")]
    [InlineData("ru-RU", "ru")]
    [InlineData("fr", "az")] // unsupported falls back to az
    [InlineData(null, "az")]
    [InlineData("", "az")]
    public void NormalizeLanguage_ShouldReturnExpectedNormalizedCode(string? input, string expected)
    {
        var result = LanguageResolutionHelper.NormalizeLanguage(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ResolveTitle_WhenRequestedLanguageExists_ShouldReturnThatTranslation()
    {
        var course = new Course
        {
            Title = "Base Title (AZ)"
        };
        course.Translations.Add(new CourseTranslation { Language = "en", Title = "English Title" });
        course.Translations.Add(new CourseTranslation { Language = "ru", Title = "Russian Title" });

        var titleEn = LanguageResolutionHelper.ResolveTitle(course, "en");
        var titleRu = LanguageResolutionHelper.ResolveTitle(course, "ru");

        Assert.Equal("English Title", titleEn);
        Assert.Equal("Russian Title", titleRu);
    }

    [Fact]
    public void ResolveTitle_WhenRequestedLanguageMissing_ShouldFallbackToAz()
    {
        var course = new Course
        {
            Title = "Base Title"
        };
        course.Translations.Add(new CourseTranslation { Language = "az", Title = "Azerbaijani Title" });

        var titleRu = LanguageResolutionHelper.ResolveTitle(course, "ru");

        Assert.Equal("Azerbaijani Title", titleRu);
    }

    [Fact]
    public void ResolveTitle_WhenNoTranslationsExist_ShouldFallbackToBaseEntityTitle()
    {
        var course = new Course
        {
            Title = "Standalone Base Title"
        };

        var title = LanguageResolutionHelper.ResolveTitle(course, "en");

        Assert.Equal("Standalone Base Title", title);
    }

    [Fact]
    public void ResolveCategoryName_WhenRequestedLanguageExists_ShouldReturnCategoryTranslation()
    {
        var category = new Category
        {
            Name = "Frontend & UI/UX"
        };
        category.Translations.Add(new CategoryTranslation { Language = "az", Name = "Frontend & UI/UX" });
        category.Translations.Add(new CategoryTranslation { Language = "ru", Name = "Фронтенд и UI/UX" });

        var nameRu = LanguageResolutionHelper.ResolveCategoryName(category, "ru");
        var nameEn = LanguageResolutionHelper.ResolveCategoryName(category, "en"); // falls back to az

        Assert.Equal("Фронтенд и UI/UX", nameRu);
        Assert.Equal("Frontend & UI/UX", nameEn);
    }
}
