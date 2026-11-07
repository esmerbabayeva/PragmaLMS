using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pragma.Admin.Services;

namespace Pragma.Admin.Controllers;

[Authorize]
public class CoursesController : Controller
{
    private readonly IPragmaApiClient _apiClient;

    public CoursesController(IPragmaApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? search = null, int page = 1)
    {
        var courses = await _apiClient.GetCoursesAsync(search, page, pageSize: 50);
        ViewData["Search"] = search;
        return View(courses);
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id)
    {
        var course = await _apiClient.GetCourseByIdAsync(id);
        if (course == null)
        {
            TempData["Error"] = "Kurs tapılmadı.";
            return RedirectToAction(nameof(Index));
        }

        return View(course);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Publish(Guid id)
    {
        var success = await _apiClient.PublishCourseAsync(id);
        if (success)
            TempData["Success"] = "Kurs uğurla dərc edildi (Published)! Tələbələr artıq qeydiyyatdan keçə bilər.";
        else
            TempData["Error"] = "Kurs dərc edilərkən xəta baş verdi.";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unpublish(Guid id)
    {
        var success = await _apiClient.UnpublishCourseAsync(id);
        if (success)
            TempData["Success"] = "Kurs qaralama rejiminə (Draft) keçirildi.";
        else
            TempData["Error"] = "Əməliyyat uğursuz oldu.";

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Gradebook(Guid id)
    {
        var gradebook = await _apiClient.GetGradebookAsync(id);
        if (gradebook == null)
        {
            TempData["Error"] = "Kurs və ya Qiymət jurnalı tapılmadı.";
            return RedirectToAction(nameof(Index));
        }

        return View(gradebook);
    }

    [HttpGet]
    public async Task<IActionResult> ExportExcel(Guid id)
    {
        var file = await _apiClient.DownloadGradebookExcelAsync(id);
        if (file == null)
        {
            TempData["Error"] = "Excel faylı generasiya edilərkən xəta baş verdi.";
            return RedirectToAction(nameof(Gradebook), new { id });
        }

        return File(file.Value.Bytes, file.Value.ContentType, file.Value.FileName);
    }
}
