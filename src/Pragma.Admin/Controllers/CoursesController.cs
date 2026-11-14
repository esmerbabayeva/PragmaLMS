using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pragma.Admin.Models;
using Pragma.Admin.Services;
using Pragma.Application.Common;
using Pragma.Application.DTOs.Courses;
using Pragma.Domain.Enums;

namespace Pragma.Admin.Controllers;

[Authorize(Roles = UserRoles.Admin)]
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
        var categories = await _apiClient.GetCategoriesAsync();
        ViewData["Search"] = search;
        var viewModel = new CourseListViewModel
        {
            Courses = courses ?? new PagedResult<CourseSummaryDto>([], 0, page, 50),
            Categories = categories ?? [],
            NewCourse = new CreateCourseViewModel()
        };
        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateCourseViewModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Məlumatları düzgün doldurun.";
            return RedirectToAction(nameof(Index));
        }

        var translations = new List<CourseTranslationDto>();
        if (!string.IsNullOrWhiteSpace(model.TitleEn))
        {
            translations.Add(new CourseTranslationDto("en", model.TitleEn, model.ShortDescriptionEn, model.DescriptionEn));
        }
        if (!string.IsNullOrWhiteSpace(model.TitleRu))
        {
            translations.Add(new CourseTranslationDto("ru", model.TitleRu, model.ShortDescriptionRu, model.DescriptionRu));
        }

        var dto = new CreateCourseDto(
            Title: model.Title,
            ShortDescription: model.ShortDescription,
            Description: model.Description,
            Price: model.Price,
            IsFree: model.IsFree,
            Level: model.Level,
            CategoryId: model.CategoryId,
            ThumbnailUrl: model.ThumbnailUrl,
            Translations: translations
        );

        var id = await _apiClient.CreateCourseAsync(dto);
        if (id.HasValue)
            TempData["Success"] = "Yeni kurs uğurla yaradıldı və tərcümələri əlavə edildi!";
        else
            TempData["Error"] = "Kurs yaradılarkən xəta baş verdi.";

        return RedirectToAction(nameof(Index));
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
