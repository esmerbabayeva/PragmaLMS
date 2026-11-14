using FluentValidation;
using Pragma.Application.Common;
using Pragma.Application.DTOs.Categories;

namespace Pragma.Application.Validators.Categories;

public class CreateCategoryValidator : AbstractValidator<CreateCategoryDto>
{
    public CreateCategoryValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Kateqoriya adı mütləq daxil edilməlidir.")
            .Length(2, 100).WithMessage("Kateqoriya adı minimum 2, maksimum 100 simvol olmalıdır.");

        RuleFor(x => x.Translations)
            .Must(HaveUniqueLanguages).WithMessage("Təkrarlanan dil kodu daxil edilə bilməz.");

        RuleForEach(x => x.Translations).ChildRules(translation =>
        {
            translation.RuleFor(t => t.Language)
                .NotEmpty().WithMessage("Dil kodu mütləq daxil edilməlidir.")
                .Must(BeSupportedLanguage).WithMessage("Tərcümə dil kodları yalnız layihədə dəstəklənən dillər (az, en, ru) ola bilər.");

            translation.RuleFor(t => t.Name)
                .NotEmpty().WithMessage("Tərcümə adı mütləq daxil edilməlidir.")
                .Length(2, 100).WithMessage("Tərcümə adı minimum 2, maksimum 100 simvol olmalıdır.");
        });
    }

    private static bool HaveUniqueLanguages(IReadOnlyCollection<CategoryTranslationDto>? translations)
    {
        if (translations == null || translations.Count <= 1) return true;
        var langs = translations.Select(t => t.Language?.Trim().ToLowerInvariant()).Where(l => !string.IsNullOrEmpty(l)).ToList();
        return langs.Distinct().Count() == langs.Count;
    }

    private static bool BeSupportedLanguage(string? language)
    {
        if (string.IsNullOrWhiteSpace(language)) return false;
        var normalized = language.Trim().ToLowerInvariant();
        return LanguageResolutionHelper.SupportedLanguages.Contains(normalized);
    }
}

public class UpdateCategoryValidator : AbstractValidator<UpdateCategoryDto>
{
    public UpdateCategoryValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Kateqoriya adı mütləq daxil edilməlidir.")
            .Length(2, 100).WithMessage("Kateqoriya adı minimum 2, maksimum 100 simvol olmalıdır.");

        RuleFor(x => x.Translations)
            .Must(HaveUniqueLanguages).WithMessage("Təkrarlanan dil kodu daxil edilə bilməz.");

        RuleForEach(x => x.Translations).ChildRules(translation =>
        {
            translation.RuleFor(t => t.Language)
                .NotEmpty().WithMessage("Dil kodu mütləq daxil edilməlidir.")
                .Must(BeSupportedLanguage).WithMessage("Tərcümə dil kodları yalnız layihədə dəstəklənən dillər (az, en, ru) ola bilər.");

            translation.RuleFor(t => t.Name)
                .NotEmpty().WithMessage("Tərcümə adı mütləq daxil edilməlidir.")
                .Length(2, 100).WithMessage("Tərcümə adı minimum 2, maksimum 100 simvol olmalıdır.");
        });
    }

    private static bool HaveUniqueLanguages(IReadOnlyCollection<CategoryTranslationDto>? translations)
    {
        if (translations == null || translations.Count <= 1) return true;
        var langs = translations.Select(t => t.Language?.Trim().ToLowerInvariant()).Where(l => !string.IsNullOrEmpty(l)).ToList();
        return langs.Distinct().Count() == langs.Count;
    }

    private static bool BeSupportedLanguage(string? language)
    {
        if (string.IsNullOrWhiteSpace(language)) return false;
        var normalized = language.Trim().ToLowerInvariant();
        return LanguageResolutionHelper.SupportedLanguages.Contains(normalized);
    }
}
