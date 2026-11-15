using System.Text.RegularExpressions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Pragma.Application.Common;
using Pragma.Application.Contracts;
using Pragma.Application.DTOs.Courses;
using Pragma.Application.DTOs.Lessons;
using Pragma.Application.DTOs.Modules;
using Pragma.Domain.Entities;
using Pragma.Domain.Enums;

namespace Pragma.Application.Services;

public class CourseService : ICourseService
{
    private readonly IAppDbContext _context;
    private readonly ICourseAccessService _accessService;
    private readonly IValidator<CreateCourseDto> _createValidator;
    private readonly IValidator<UpdateCourseDto> _updateValidator;

    public CourseService(
        IAppDbContext context,
        ICourseAccessService accessService,
        IValidator<CreateCourseDto> createValidator,
        IValidator<UpdateCourseDto> updateValidator)
    {
        _context = context;
        _accessService = accessService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<PagedResult<CourseSummaryDto>> GetCoursesAsync(CourseFilterDto filter, CancellationToken cancellationToken = default)
    {
        var lang = LanguageResolutionHelper.NormalizeLanguage(filter.Language);

        var query = _context.Courses
            .AsNoTracking()
            .Include(c => c.Translations)
            .Include(c => c.Category)
                .ThenInclude(cat => cat!.Translations)
            .Include(c => c.Reviews)
            .Include(c => c.Modules)
                .ThenInclude(m => m.Lessons)
            .AsQueryable();

        if (filter.Status.HasValue)
        {
            query = query.Where(c => c.Status == filter.Status.Value);
        }
        else if (!filter.IncludeAllStatuses)
        {
            query = query.Where(c => c.Status == CourseStatus.Published);
        }

        // Axtarış (Transliterasiya və çoxdilli axtarış)
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var searchTerms = SearchNormalizationHelper.GetSearchTerms(filter.Search);
            var term1 = searchTerms.ElementAtOrDefault(0);
            var term2 = searchTerms.ElementAtOrDefault(1);
            var term3 = searchTerms.ElementAtOrDefault(2);

            query = query.Where(c =>
                (term1 != null && (
                    c.Title.ToLower().Contains(term1) ||
                    (c.ShortDescription != null && c.ShortDescription.ToLower().Contains(term1)) ||
                    c.Translations.Any(t => t.Title.ToLower().Contains(term1) || (t.ShortDescription != null && t.ShortDescription.ToLower().Contains(term1)))
                )) ||
                (term2 != null && (
                    c.Title.ToLower().Contains(term2) ||
                    (c.ShortDescription != null && c.ShortDescription.ToLower().Contains(term2)) ||
                    c.Translations.Any(t => t.Title.ToLower().Contains(term2) || (t.ShortDescription != null && t.ShortDescription.ToLower().Contains(term2)))
                )) ||
                (term3 != null && (
                    c.Title.ToLower().Contains(term3) ||
                    (c.ShortDescription != null && c.ShortDescription.ToLower().Contains(term3)) ||
                    c.Translations.Any(t => t.Title.ToLower().Contains(term3) || (t.ShortDescription != null && t.ShortDescription.ToLower().Contains(term3)))
                ))
            );
        }

        // Kateqoriya filteri
        if (filter.CategoryId.HasValue)
        {
            query = query.Where(c => c.CategoryId == filter.CategoryId.Value);
        }

        // Səviyyə filteri
        if (filter.Level.HasValue)
        {
            query = query.Where(c => c.Level == filter.Level.Value);
        }

        // Qiymət filteri
        if (filter.IsFree.HasValue)
        {
            query = query.Where(c => c.IsFree == filter.IsFree.Value);
        }

        // Instructor filteri
        if (filter.InstructorId.HasValue)
        {
            query = query.Where(c => c.InstructorId == filter.InstructorId.Value);
        }

        // Sıralama
        query = filter.SortBy?.ToLowerInvariant() switch
        {
            "price_asc" => query.OrderBy(c => c.Price),
            "price_desc" => query.OrderByDescending(c => c.Price),
            "rating" => query.OrderByDescending(c => c.Reviews.Any() ? c.Reviews.Average(r => r.Rating) : 0),
            _ => query.OrderByDescending(c => c.CreatedAtUtc)
        };

        var totalCount = await query.CountAsync(cancellationToken);

        var pageNumber = Math.Max(1, filter.PageNumber);
        var pageSize = Math.Clamp(filter.PageSize, 1, 50);

        var coursesList = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var courses = coursesList.Select(c => new CourseSummaryDto(
            c.Id,
            LanguageResolutionHelper.ResolveTitle(c, lang),
            c.Slug,
            LanguageResolutionHelper.ResolveShortDescription(c, lang),
            c.ThumbnailUrl,
            c.Price,
            c.IsFree,
            c.Level,
            c.Status,
            c.CategoryId,
            LanguageResolutionHelper.ResolveCategoryName(c.Category, lang),
            c.InstructorId,
            "Instructor",
            c.Reviews.Any() ? Math.Round(c.Reviews.Average(r => r.Rating), 1) : 5.0,
            c.Reviews.Count,
            c.Modules.SelectMany(m => m.Lessons).Count(),
            c.Modules.SelectMany(m => m.Lessons).Sum(l => l.DurationSeconds) / 60,
            c.CreatedAtUtc,
            c.Status == CourseStatus.Published,
            c.Translations.Select(t => new CourseTranslationDto(t.Language, t.Title, t.ShortDescription, t.Description)).ToList()
        )).ToList();

        return new PagedResult<CourseSummaryDto>(courses, totalCount, pageNumber, pageSize);
    }

    public async Task<Result<CourseDetailDto>> GetCourseByIdAsync(Guid id, string? language = null, CancellationToken cancellationToken = default)
    {
        var lang = LanguageResolutionHelper.NormalizeLanguage(language);

        var course = await _context.Courses
            .AsNoTracking()
            .Include(c => c.Translations)
            .Include(c => c.Category)
                .ThenInclude(cat => cat!.Translations)
            .Include(c => c.Reviews)
            .Include(c => c.Enrollments)
            .Include(c => c.Modules.OrderBy(m => m.OrderIndex))
            .ThenInclude(m => m.Lessons.OrderBy(l => l.OrderIndex))
            .ThenInclude(l => l.Quiz)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (course == null)
        {
            var cohort = await _context.Cohorts
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted, cancellationToken);
            if (cohort != null && cohort.CourseId != Guid.Empty)
            {
                course = await _context.Courses
                    .AsNoTracking()
                    .Include(c => c.Translations)
                    .Include(c => c.Category)
                        .ThenInclude(cat => cat!.Translations)
                    .Include(c => c.Reviews)
                    .Include(c => c.Enrollments)
                    .Include(c => c.Modules.OrderBy(m => m.OrderIndex))
                    .ThenInclude(m => m.Lessons.OrderBy(l => l.OrderIndex))
                    .ThenInclude(l => l.Quiz)
                    .FirstOrDefaultAsync(c => c.Id == cohort.CourseId, cancellationToken);
            }
        }

        if (course == null)
        {
            return Result<CourseDetailDto>.Failure(Error.NotFound("Course.NotFound", "Axtarılan kurs tapılmadı."));
        }

        return MapToDetailDto(course, lang);
    }

    public async Task<Result<CourseDetailDto>> GetCourseBySlugAsync(string slug, string? language = null, CancellationToken cancellationToken = default)
    {
        var lang = LanguageResolutionHelper.NormalizeLanguage(language);

        var course = await _context.Courses
            .AsNoTracking()
            .Include(c => c.Translations)
            .Include(c => c.Category)
                .ThenInclude(cat => cat!.Translations)
            .Include(c => c.Reviews)
            .Include(c => c.Enrollments)
            .Include(c => c.Modules.OrderBy(m => m.OrderIndex))
            .ThenInclude(m => m.Lessons.OrderBy(l => l.OrderIndex))
            .ThenInclude(l => l.Quiz)
            .FirstOrDefaultAsync(c => c.Slug == slug.ToLowerInvariant(), cancellationToken);

        if (course == null)
        {
            return Result<CourseDetailDto>.Failure(Error.NotFound("Course.NotFound", "Axtarılan kurs tapılmadı."));
        }

        return MapToDetailDto(course, lang);
    }

    public async Task<Result<Guid>> CreateCourseAsync(CreateCourseDto dto, Guid instructorId, CancellationToken cancellationToken = default)
    {
        var validation = await _createValidator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<Guid>.Failure(Error.Validation("Course.Validation", string.Join("; ", validation.Errors.Select(e => e.ErrorMessage))));
        }

        var categoryExists = await _context.Categories.AnyAsync(c => c.Id == dto.CategoryId, cancellationToken);
        if (!categoryExists)
        {
            return Result<Guid>.Failure(Error.NotFound("Category.NotFound", "Seçilmiş kateqoriya tapılmadı."));
        }

        var slug = GenerateSlug(dto.Title);
        if (await _context.Courses.AnyAsync(c => c.Slug == slug, cancellationToken))
        {
            slug = $"{slug}-{Guid.NewGuid().ToString()[..6]}";
        }

        var course = new Course
        {
            Title = dto.Title.Trim(),
            Slug = slug,
            ShortDescription = dto.ShortDescription,
            Description = dto.Description,
            ThumbnailUrl = dto.ThumbnailUrl,
            Price = dto.IsFree ? 0 : dto.Price,
            IsFree = dto.IsFree,
            Level = dto.Level,
            Status = CourseStatus.Draft,
            CategoryId = dto.CategoryId,
            InstructorId = instructorId
        };

        // Çoxdilli tərcümələrin əlavə olunması (AZ məcburi, EN/RU ixtiyari)
        var hasAzTranslation = false;
        if (dto.Translations != null && dto.Translations.Count > 0)
        {
            foreach (var t in dto.Translations)
            {
                if (string.IsNullOrWhiteSpace(t.Title)) continue;
                var tLang = LanguageResolutionHelper.NormalizeLanguage(t.Language);
                if (tLang == "az") hasAzTranslation = true;

                course.Translations.Add(new CourseTranslation
                {
                    CourseId = course.Id,
                    Language = tLang,
                    Title = t.Title.Trim(),
                    ShortDescription = t.ShortDescription,
                    Description = t.Description
                });
            }
        }

        if (!hasAzTranslation)
        {
            course.Translations.Add(new CourseTranslation
            {
                CourseId = course.Id,
                Language = "az",
                Title = dto.Title.Trim(),
                ShortDescription = dto.ShortDescription,
                Description = dto.Description
            });
        }

        _context.Courses.Add(course);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(course.Id);
    }

    public async Task<Result> UpdateCourseAsync(Guid id, UpdateCourseDto dto, Guid userId, bool isAdmin, CancellationToken cancellationToken = default)
    {
        var validation = await _updateValidator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure(Error.Validation("Course.Validation", string.Join("; ", validation.Errors.Select(e => e.ErrorMessage))));
        }

        var course = await _context.Courses
            .Include(c => c.Translations)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (course == null)
        {
            return Result.Failure(Error.NotFound("Course.NotFound", "Kurs tapılmadı."));
        }

        var canManage = await _accessService.CanManageCourseAsync(id, userId, isAdmin, cancellationToken);
        if (!canManage)
        {
            return Result.Failure(Error.Forbidden("Course.Forbidden", "Bu kursu redaktə etmək icazəniz yoxdur."));
        }

        course.Title = dto.Title.Trim();
        course.ShortDescription = dto.ShortDescription;
        course.Description = dto.Description;
        course.ThumbnailUrl = dto.ThumbnailUrl;
        course.Price = dto.IsFree ? 0 : dto.Price;
        course.IsFree = dto.IsFree;
        course.Level = dto.Level;
        course.CategoryId = dto.CategoryId;
        course.Status = dto.Status;

        // Tərcümələrin yenilənməsi
        if (dto.Translations != null)
        {
            foreach (var t in dto.Translations)
            {
                if (string.IsNullOrWhiteSpace(t.Title)) continue;
                var tLang = LanguageResolutionHelper.NormalizeLanguage(t.Language);
                var existing = course.Translations.FirstOrDefault(tr => tr.Language.Equals(tLang, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    existing.Title = t.Title.Trim();
                    existing.ShortDescription = t.ShortDescription;
                    existing.Description = t.Description;
                }
                else
                {
                    course.Translations.Add(new CourseTranslation
                    {
                        CourseId = course.Id,
                        Language = tLang,
                        Title = t.Title.Trim(),
                        ShortDescription = t.ShortDescription,
                        Description = t.Description
                    });
                }
            }
        }

        // AZ tərcüməsini əsas sahə ilə sinxron saxla
        var az = course.Translations.FirstOrDefault(t => t.Language == "az");
        if (az != null)
        {
            az.Title = course.Title;
            az.ShortDescription = course.ShortDescription;
            az.Description = course.Description;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> DeleteCourseAsync(Guid id, Guid userId, bool isAdmin, CancellationToken cancellationToken = default)
    {
        var course = await _context.Courses.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (course == null)
        {
            return Result.Failure(Error.NotFound("Course.NotFound", "Kurs tapılmadı."));
        }

        var canManage = await _accessService.CanManageCourseAsync(id, userId, isAdmin, cancellationToken);
        if (!canManage)
        {
            return Result.Failure(Error.Forbidden("Course.Forbidden", "Bu kursu silmək icazəniz yoxdur."));
        }

        course.MarkDeleted();
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public async Task<Result> PublishCourseAsync(Guid courseId, Guid userId, bool isAdmin, CancellationToken cancellationToken = default)
    {
        var course = await _context.Courses.FirstOrDefaultAsync(c => c.Id == courseId, cancellationToken);
        if (course == null)
        {
            return Result.Failure(Error.NotFound("Course.NotFound", "Kurs tapılmadı."));
        }

        var canManage = await _accessService.CanManageCourseAsync(courseId, userId, isAdmin, cancellationToken);
        if (!canManage)
        {
            return Result.Failure(Error.Forbidden("Course.Forbidden", "Bu kursu dərc etmək icazəniz yoxdur."));
        }

        course.Status = CourseStatus.Published;
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public async Task<Result> UnpublishCourseAsync(Guid courseId, Guid userId, bool isAdmin, CancellationToken cancellationToken = default)
    {
        var course = await _context.Courses.FirstOrDefaultAsync(c => c.Id == courseId, cancellationToken);
        if (course == null)
        {
            return Result.Failure(Error.NotFound("Course.NotFound", "Kurs tapılmadı."));
        }

        var canManage = await _accessService.CanManageCourseAsync(courseId, userId, isAdmin, cancellationToken);
        if (!canManage)
        {
            return Result.Failure(Error.Forbidden("Course.Forbidden", "Bu kursu qaralamaya qaytarmaq icazəniz yoxdur."));
        }

        course.Status = CourseStatus.Draft;
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public async Task<Result> ReorderModulesAsync(Guid courseId, ReorderItemsRequestDto dto, Guid userId, bool isAdmin, CancellationToken cancellationToken = default)
    {
        var canManage = await _accessService.CanManageCourseAsync(courseId, userId, isAdmin, cancellationToken);
        if (!canManage)
        {
            return Result.Failure(Error.Forbidden("Course.Forbidden", "Bu kursun modullarını nizamlamaq icazəniz yoxdur."));
        }

        var modules = await _context.Modules.Where(m => m.CourseId == courseId).ToListAsync(cancellationToken);
        foreach (var item in dto.Items)
        {
            var module = modules.FirstOrDefault(m => m.Id == item.Id);
            if (module != null)
            {
                module.OrderIndex = item.OrderIndex;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private static CourseDetailDto MapToDetailDto(Course course, string language)
    {
        var modules = course.Modules.OrderBy(m => m.OrderIndex).Select(m => new ModuleDto(
            m.Id,
            m.CourseId,
            m.Title,
            m.Description,
            m.OrderIndex,
            m.Lessons.OrderBy(l => l.OrderIndex).Select(l => new LessonSummaryDto(
                l.Id,
                l.ModuleId,
                l.Title,
                l.OrderIndex,
                l.Type,
                l.DurationSeconds,
                l.IsPreview,
                l.Quiz != null,
                l.Quiz?.Id
            )).ToList()
        )).ToList();

        var avgRating = course.Reviews.Any() ? Math.Round(course.Reviews.Average(r => r.Rating), 1) : 5.0;

        return new CourseDetailDto(
            course.Id,
            LanguageResolutionHelper.ResolveTitle(course, language),
            course.Slug,
            LanguageResolutionHelper.ResolveShortDescription(course, language),
            LanguageResolutionHelper.ResolveDescription(course, language),
            course.ThumbnailUrl,
            course.Price,
            course.IsFree,
            course.Level,
            course.Status,
            course.CategoryId,
            LanguageResolutionHelper.ResolveCategoryName(course.Category, language),
            course.InstructorId,
            "Instructor",
            avgRating,
            course.Reviews.Count,
            course.Enrollments.Count,
            modules,
            course.CreatedAtUtc,
            course.Translations.Select(t => new CourseTranslationDto(t.Language, t.Title, t.ShortDescription, t.Description)).ToList()
        );
    }

    private static string GenerateSlug(string title)
    {
        var slug = title.ToLowerInvariant().Trim();
        slug = slug.Replace('ə', 'e').Replace('ı', 'i').Replace('ö', 'o').Replace('ü', 'u').Replace('ğ', 'g').Replace('ş', 's').Replace('ç', 'c');
        slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");
        slug = Regex.Replace(slug, @"\s+", "-").Trim('-');
        return string.IsNullOrWhiteSpace(slug) ? "kurs-" + Guid.NewGuid().ToString()[..6] : slug;
    }
}
