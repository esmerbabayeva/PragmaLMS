using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pragma.Application.Common;
using Pragma.Application.Contracts;
using Pragma.Application.DTOs.Categories;

namespace Pragma.API.Controllers;

/// <summary>
/// Kurs kateqoriyalarının idarə edilməsi.
/// </summary>
[Route("api/categories")]
public class CategoriesController : BaseApiController
{
    private readonly ICategoryService _categoryService;

    public CategoriesController(ICategoryService categoryService)
    {
        _categoryService = categoryService;
    }

    /// <summary>
    /// Bütün aktiv kateqoriyaların siyahısını qaytarır.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<CategoryDto>>> GetCategories([FromQuery] string? lang, CancellationToken cancellationToken)
    {
        var cookieLang = Request.Cookies["pragma-lang"];
        var headerLang = Request.Headers["X-Language"].ToString();
        var rawLanguage = !string.IsNullOrWhiteSpace(lang)
            ? lang
            : (!string.IsNullOrWhiteSpace(headerLang)
                ? headerLang
                : (!string.IsNullOrWhiteSpace(cookieLang)
                    ? cookieLang
                    : Request.Headers.AcceptLanguage.ToString()));

        var language = LanguageResolutionHelper.NormalizeLanguage(rawLanguage);
        var result = await _categoryService.GetCategoriesAsync(language, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// ID-yə görə tək bir kateqoriyanın məlumatlarını qaytarır.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CategoryDto>> GetCategoryById(Guid id, [FromQuery] string? lang, CancellationToken cancellationToken)
    {
        var cookieLang = Request.Cookies["pragma-lang"];
        var headerLang = Request.Headers["X-Language"].ToString();
        var rawLanguage = !string.IsNullOrWhiteSpace(lang)
            ? lang
            : (!string.IsNullOrWhiteSpace(headerLang)
                ? headerLang
                : (!string.IsNullOrWhiteSpace(cookieLang)
                    ? cookieLang
                    : Request.Headers.AcceptLanguage.ToString()));

        var language = LanguageResolutionHelper.NormalizeLanguage(rawLanguage);
        var result = await _categoryService.GetCategoryByIdAsync(id, language, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Yeni kurs kateqoriyası yaradır (Yalnız Admin və Moderator üçün).
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.CanManageCourses)]
    [HttpPost]
    public async Task<ActionResult<Guid>> CreateCategory([FromBody] CreateCategoryDto dto, CancellationToken cancellationToken)
    {
        var result = await _categoryService.CreateCategoryAsync(dto, cancellationToken);
        if (result.IsFailure)
        {
            return HandleResult(result);
        }

        return CreatedAtAction(nameof(GetCategoryById), new { id = result.Value }, result.Value);
    }

    /// <summary>
    /// Kateqoriya məlumatlarını yeniləyir (Yalnız Admin üçün).
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireAdmin)]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CategoryDto>> UpdateCategory(Guid id, [FromBody] UpdateCategoryDto dto, CancellationToken cancellationToken)
    {
        var result = await _categoryService.UpdateCategoryAsync(id, dto, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Kateqoriyanı silir (Soft delete, Yalnız Admin üçün).
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireAdmin)]
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> DeleteCategory(Guid id, CancellationToken cancellationToken)
    {
        var result = await _categoryService.DeleteCategoryAsync(id, cancellationToken);
        return HandleResult(result);
    }
}
