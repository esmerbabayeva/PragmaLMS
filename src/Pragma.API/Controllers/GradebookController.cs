using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pragma.Application.Contracts;
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
    private readonly IExcelExporter _excelExporter;

    public GradebookController(IExcelExporter excelExporter)
    {
        _excelExporter = excelExporter;
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
