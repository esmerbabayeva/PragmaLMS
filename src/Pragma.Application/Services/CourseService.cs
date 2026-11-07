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
        var query = _context.Courses
            .AsNoTracking()
            .Include(c => c.Category)
            .Include(c => c.Reviews)
            .Include(c => c.Modules)
            .ThenInclude(m => m.Lessons)
            .Where(c => c.Status == CourseStatus.Published);

        // Axtarış
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim().ToLower();
            query = query.Where(c => c.Title.ToLower().Contains(search) || (c.ShortDescription != null && c.ShortDescription.ToLower().Contains(search)));
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

        var courses = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new CourseSummaryDto(
                c.Id,
                c.Title,
                c.Slug,
                c.ShortDescription,
                c.ThumbnailUrl,
                c.Price,
                c.IsFree,
                c.Level,
                c.Status,
                c.CategoryId,
                c.Category != null ? c.Category.Name : string.Empty,
                c.InstructorId,
                "Təlimçi",
                c.Reviews.Any() ? Math.Round(c.Reviews.Average(r => r.Rating), 1) : 5.0,
                c.Reviews.Count,
                c.Modules.SelectMany(m => m.Lessons).Count(),
                c.Modules.SelectMany(m => m.Lessons).Sum(l => l.DurationSeconds) / 60,
                c.CreatedAtUtc
            ))
            .ToListAsync(cancellationToken);

        return new PagedResult<CourseSummaryDto>(courses, totalCount, pageNumber, pageSize);
    }

    public async Task<Result<CourseDetailDto>> GetCourseByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var course = await _context.Courses
            .AsNoTracking()
            .Include(c => c.Category)
            .Include(c => c.Reviews)
            .Include(c => c.Enrollments)
            .Include(c => c.Modules.OrderBy(m => m.OrderIndex))
            .ThenInclude(m => m.Lessons.OrderBy(l => l.OrderIndex))
            .ThenInclude(l => l.Quiz)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (course == null)
        {
            return Result<CourseDetailDto>.Failure(Error.NotFound("Course.NotFound", "Axtarılan kurs tapılmadı."));
        }

        return MapToDetailDto(course);
    }

    public async Task<Result<CourseDetailDto>> GetCourseBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        var course = await _context.Courses
            .AsNoTracking()
            .Include(c => c.Category)
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

        return MapToDetailDto(course);
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
            Status = CourseStatus.Draft, // Müəllim yaratdıqda ilkin olaraq Draft olur
            CategoryId = dto.CategoryId,
            InstructorId = instructorId
        };

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

        var course = await _context.Courses.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
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

        // Admin və ya müəllim tərəfindən status yenilənməsi
        if (isAdmin || dto.Status != CourseStatus.Published)
        {
            course.Status = dto.Status;
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

    private static CourseDetailDto MapToDetailDto(Course course)
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
            course.Title,
            course.Slug,
            course.ShortDescription,
            course.Description,
            course.ThumbnailUrl,
            course.Price,
            course.IsFree,
            course.Level,
            course.Status,
            course.CategoryId,
            course.Category?.Name ?? string.Empty,
            course.InstructorId,
            "Təlimçi",
            avgRating,
            course.Reviews.Count,
            course.Enrollments.Count,
            modules,
            course.CreatedAtUtc
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
