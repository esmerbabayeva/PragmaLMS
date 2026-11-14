using Microsoft.EntityFrameworkCore;
using Pragma.Application.Common;
using Pragma.Application.Contracts;
using Pragma.Application.DTOs.Categories;
using Pragma.Domain.Entities;
using Pragma.Domain.Enums;

namespace Pragma.Application.Services;

public class CategoryService : ICategoryService
{
    private readonly IAppDbContext _context;

    public CategoryService(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<Result<IReadOnlyCollection<CategoryDto>>> GetCategoriesAsync(string? requestedLanguage = null, CancellationToken ct = default)
    {
        var language = LanguageResolutionHelper.NormalizeLanguage(requestedLanguage);

        var categories = await _context.Categories
            .AsNoTracking()
            .Include(c => c.Translations)
            .Include(c => c.Courses)
            .Where(c => !c.IsDeleted)
            .OrderBy(c => c.SortOrder)
            .ToListAsync(ct);

        var dtos = categories.Select(c => new CategoryDto(
            c.Id,
            LanguageResolutionHelper.ResolveCategoryName(c, language),
            c.Slug,
            LanguageResolutionHelper.ResolveCategoryDescription(c, language),
            c.IconName,
            c.SortOrder,
            c.Courses.Count(course => course.Status == CourseStatus.Published && !course.IsDeleted),
            c.Translations.Where(t => !t.IsDeleted).Select(t => new CategoryTranslationDto(t.Language, t.Name, t.Description)).ToList()
        )).ToList();

        return Result<IReadOnlyCollection<CategoryDto>>.Success(dtos);
    }

    public async Task<Result<CategoryDto>> GetCategoryByIdAsync(Guid id, string? requestedLanguage = null, CancellationToken ct = default)
    {
        var language = LanguageResolutionHelper.NormalizeLanguage(requestedLanguage);

        var category = await _context.Categories
            .AsNoTracking()
            .Include(c => c.Translations)
            .Include(c => c.Courses)
            .Where(c => c.Id == id && !c.IsDeleted)
            .FirstOrDefaultAsync(ct);

        if (category == null)
        {
            return Result<CategoryDto>.Failure(Error.NotFound("Category.NotFound", "Kateqoriya tapılmadı."));
        }

        var dto = new CategoryDto(
            category.Id,
            LanguageResolutionHelper.ResolveCategoryName(category, language),
            category.Slug,
            LanguageResolutionHelper.ResolveCategoryDescription(category, language),
            category.IconName,
            category.SortOrder,
            category.Courses.Count(course => course.Status == CourseStatus.Published && !course.IsDeleted),
            category.Translations.Where(t => !t.IsDeleted).Select(t => new CategoryTranslationDto(t.Language, t.Name, t.Description)).ToList()
        );

        return Result<CategoryDto>.Success(dto);
    }

    public async Task<Result<Guid>> CreateCategoryAsync(CreateCategoryDto dto, CancellationToken ct = default)
    {
        var name = dto.Name.Trim();
        var slug = GenerateSlug(name);

        var category = new Category
        {
            Name = name,
            Slug = slug,
            Description = dto.Description?.Trim(),
            IconName = dto.IconName?.Trim(),
            SortOrder = dto.SortOrder
        };

        var hasAz = false;
        if (dto.Translations != null)
        {
            foreach (var t in dto.Translations)
            {
                if (string.IsNullOrWhiteSpace(t.Name)) continue;
                var tLang = LanguageResolutionHelper.NormalizeLanguage(t.Language);
                if (tLang == "az") hasAz = true;

                category.Translations.Add(new CategoryTranslation
                {
                    CategoryId = category.Id,
                    Language = tLang,
                    Name = t.Name.Trim(),
                    Description = t.Description?.Trim()
                });
            }
        }

        if (!hasAz)
        {
            category.Translations.Add(new CategoryTranslation
            {
                CategoryId = category.Id,
                Language = "az",
                Name = name,
                Description = dto.Description?.Trim()
            });
        }

        _context.Categories.Add(category);
        await _context.SaveChangesAsync(ct);

        return Result<Guid>.Success(category.Id);
    }

    public async Task<Result<CategoryDto>> UpdateCategoryAsync(Guid id, UpdateCategoryDto dto, CancellationToken ct = default)
    {
        var category = await _context.Categories
            .Include(c => c.Translations)
            .Include(c => c.Courses)
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted, ct);

        if (category == null)
        {
            return Result<CategoryDto>.Failure(Error.NotFound("Category.NotFound", "Kateqoriya tapılmadı."));
        }

        var name = dto.Name.Trim();
        category.Name = name;
        category.Slug = GenerateSlug(name);
        category.Description = dto.Description?.Trim();
        category.IconName = dto.IconName?.Trim();
        category.SortOrder = dto.SortOrder;

        if (dto.Translations != null)
        {
            foreach (var t in dto.Translations)
            {
                if (string.IsNullOrWhiteSpace(t.Name)) continue;
                var tLang = LanguageResolutionHelper.NormalizeLanguage(t.Language);
                var existing = category.Translations.FirstOrDefault(tr => tr.Language.Equals(tLang, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    existing.Name = t.Name.Trim();
                    existing.Description = t.Description?.Trim();
                }
                else
                {
                    var newTrans = new CategoryTranslation
                    {
                        CategoryId = category.Id,
                        Language = tLang,
                        Name = t.Name.Trim(),
                        Description = t.Description?.Trim()
                    };
                    _context.CategoryTranslations.Add(newTrans);
                }
            }
        }

        // AZ tərcüməsini əsas sahə ilə sinxron saxla
        var az = category.Translations.FirstOrDefault(t => t.Language == "az");
        if (az != null)
        {
            az.Name = category.Name;
            az.Description = category.Description;
        }

        await _context.SaveChangesAsync(ct);

        var updatedDto = new CategoryDto(
            category.Id,
            category.Name,
            category.Slug,
            category.Description,
            category.IconName,
            category.SortOrder,
            category.Courses.Count(c => c.Status == CourseStatus.Published && !c.IsDeleted),
            category.Translations.Where(t => !t.IsDeleted).Select(t => new CategoryTranslationDto(t.Language, t.Name, t.Description)).ToList()
        );

        return Result<CategoryDto>.Success(updatedDto);
    }

    public async Task<Result> DeleteCategoryAsync(Guid id, CancellationToken ct = default)
    {
        var category = await _context.Categories
            .Include(c => c.Translations)
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted, ct);

        if (category == null)
        {
            return Result.Failure(Error.NotFound("Category.NotFound", "Kateqoriya tapılmadı."));
        }

        var hasLinkedCourses = await _context.Courses
            .AnyAsync(c => c.CategoryId == id && !c.IsDeleted, ct);

        if (hasLinkedCourses)
        {
            return Result.Failure(Error.Conflict("Category.HasLinkedCourses", "Bu kateqoriyaya bağlı aktiv kurslar mövcuddur. Silinə bilməz."));
        }

        category.MarkDeleted();
        foreach (var t in category.Translations)
        {
            t.MarkDeleted();
        }

        await _context.SaveChangesAsync(ct);
        return Result.Success();
    }

    private static string GenerateSlug(string name)
    {
        return name.ToLowerInvariant().Trim().Replace(' ', '-');
    }
}
