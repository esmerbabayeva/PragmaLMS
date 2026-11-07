using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pragma.Application.Contracts;
using Pragma.Application.DTOs.Categories;
using Pragma.Domain.Entities;
using Pragma.Domain.Enums;

namespace Pragma.API.Controllers;

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
            .OrderBy(c => c.SortOrder)
            .Select(c => new CategoryDto(
                c.Id,
                c.Name,
                c.Slug,
                c.Description,
                c.IconName,
                c.SortOrder,
                c.Courses.Count(course => course.Status == CourseStatus.Published)
            ))
            .ToListAsync(cancellationToken);

        return Ok(categories);
    }

    /// <summary>
    /// Yeni kateqoriya yaradılması (Yalnız Admin üçün).
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

        return CreatedAtAction(nameof(GetCategories), new { id = category.Id }, category.Id);
    }
}
