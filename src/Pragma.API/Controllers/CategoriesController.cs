using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pragma.Application.Contracts;
using Pragma.Application.DTOs.Categories;
using Pragma.Domain.Entities;
using Pragma.Domain.Enums;

namespace Pragma.API.Controllers;

/// <summary>
/// Kurs kateqoriyalarının idarə edilməsi.
/// </summary>
[Route("api/categories")]
public class CategoriesController : BaseApiController
{
    private readonly IAppDbContext _context;

    public CategoriesController(IAppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Bütün aktiv kateqoriyaların siyahısını qaytarır.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<CategoryDto>>> GetCategories(CancellationToken cancellationToken)
    {
        var categories = await _context.Categories
            .AsNoTracking()
            .Where(c => !c.IsDeleted)
            .OrderBy(c => c.SortOrder)
            .Select(c => new CategoryDto(
                c.Id,
                c.Name,
                c.Slug,
                c.Description,
                c.IconName,
                c.SortOrder,
                c.Courses.Count(course => course.Status == CourseStatus.Published && !course.IsDeleted)
            ))
            .ToListAsync(cancellationToken);

        return Ok(categories);
    }

    /// <summary>
    /// ID-yə görə tək bir kateqoriyanın məlumatlarını qaytarır.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CategoryDto>> GetCategoryById(Guid id, CancellationToken cancellationToken)
    {
        var category = await _context.Categories
            .AsNoTracking()
            .Where(c => c.Id == id && !c.IsDeleted)
            .Select(c => new CategoryDto(
                c.Id,
                c.Name,
                c.Slug,
                c.Description,
                c.IconName,
                c.SortOrder,
                c.Courses.Count(course => course.Status == CourseStatus.Published && !course.IsDeleted)
            ))
            .FirstOrDefaultAsync(cancellationToken);

        if (category == null)
            return NotFound("Kateqoriya tapılmadı.");

        return Ok(category);
    }

    /// <summary>
    /// Yeni kurs kateqoriyası yaradır (Yalnız Admin üçün).
    /// </summary>
    [Authorize(Roles = UserRoles.Admin)]
    [HttpPost]
    public async Task<ActionResult<Guid>> CreateCategory([FromBody] CreateCategoryDto dto, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            return BadRequest("Kateqoriya adı mütləq daxil edilməlidir.");
        }

        var slug = dto.Name.ToLowerInvariant().Trim().Replace(' ', '-');
        var category = new Category
        {
            Name = dto.Name.Trim(),
            Slug = slug,
            Description = dto.Description,
            IconName = dto.IconName,
            SortOrder = dto.SortOrder
        };

        _context.Categories.Add(category);
        await _context.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetCategoryById), new { id = category.Id }, category.Id);
    }

    /// <summary>
    /// Kateqoriyanı silir (Soft delete, yalnız Admin üçün).
    /// </summary>
    [Authorize(Roles = UserRoles.Admin)]
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> DeleteCategory(Guid id, CancellationToken cancellationToken)
    {
        var category = await _context.Categories.FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted, cancellationToken);
        if (category == null)
            return NotFound("Kateqoriya tapılmadı.");

        category.MarkDeleted();
        await _context.SaveChangesAsync(cancellationToken);

        return NoContent();
    }
}
