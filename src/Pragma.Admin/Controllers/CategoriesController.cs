using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pragma.Admin.Models;
using Pragma.Admin.Services;
using Pragma.Application.DTOs.Categories;
using Pragma.Domain.Enums;

namespace Pragma.Admin.Controllers;

[Authorize(Roles = UserRoles.Admin)]
public class CategoriesController : Controller
{
    private readonly IPragmaApiClient _apiClient;

    public CategoriesController(IPragmaApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var categories = await _apiClient.GetCategoriesAsync() ?? [];
        var vm = new CategoryListViewModel
        {
            Categories = categories,
            NewCategory = new CreateCategoryViewModel()
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateCategoryViewModel model)
    {
        if (!ModelState.IsValid)
        {
            var categories = await _apiClient.GetCategoriesAsync() ?? [];
            return View("Index", new CategoryListViewModel { Categories = categories, NewCategory = model });
        }

        var translations = new List<CategoryTranslationDto>
        {
            new("az", model.Name.Trim(), model.Description?.Trim())
        };
        if (!string.IsNullOrWhiteSpace(model.NameEn))
            translations.Add(new("en", model.NameEn.Trim(), model.DescriptionEn?.Trim()));
        if (!string.IsNullOrWhiteSpace(model.NameRu))
            translations.Add(new("ru", model.NameRu.Trim(), model.DescriptionRu?.Trim()));

        var dto = new CreateCategoryDto(
            Name: model.Name.Trim(),
            Description: model.Description?.Trim(),
            IconName: model.IconName?.Trim(),
            SortOrder: model.SortOrder,
            Translations: translations
        );

        var success = await _apiClient.CreateCategoryAsync(dto);
        if (success)
            TempData["Success"] = $"'{model.Name}' kateqoriyası uğurla yaradıldı!";
        else
            TempData["Error"] = "Kateqoriya yaradılarkən xəta baş verdi.";

        return RedirectToAction(nameof(Index));
    }
}
