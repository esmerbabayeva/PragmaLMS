using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pragma.Application.Contracts;
using Pragma.Application.DTOs.Gradebook;
using Pragma.Domain.Enums;

namespace Pragma.API.Controllers;

/// <summary>
/// Müəllimlər və Adminlər üçün qiymət cədvəli və tələbə analitikası ixracı.
/// </summary>
[Authorize(Roles = $"{UserRoles.Instructor},{UserRoles.Admin}")]
[ApiController]
[Route("api/courses/{id:guid}/gradebook")]
public class GradebookController : BaseApiController
{
    private readonly IGradebookService _gradebookService;
    private readonly IExcelExporter _excelExporter;

    public GradebookController(
        IGradebookService gradebookService,
        IExcelExporter excelExporter)
    {
        _gradebookService = gradebookService;
        _excelExporter = excelExporter;
    }

    /// <summary>
    /// Kursun qiymət cədvəlini və tələbə analitikasını qaytarır (Admin / Müəllim üçün).
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<CourseGradebookDto>> GetGradebook(Guid id, CancellationToken ct)
    {
        var result = await _gradebookService.GetCourseGradebookAsync(id, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Kursun 3-vərəqli tələbə qiymət cədvəlini və analitikasını Excel (.xlsx) olaraq ixrac edir.
    /// </summary>
    [HttpGet("export-excel")]
    public async Task<IActionResult> ExportGradebook(Guid id, CancellationToken ct)
    {
        var result = await _excelExporter.ExportCourseGradebookAsync(id, ct);
        if (result.IsFailure)
            return HandleResult(result);

        var fileResult = result.Value;
        return File(fileResult.Content, fileResult.ContentType, fileResult.FileName);
    }
}
