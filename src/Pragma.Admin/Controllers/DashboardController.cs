using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pragma.Admin.Models;
using Pragma.Admin.Services;
using Pragma.Domain.Enums;

namespace Pragma.Admin.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private readonly IPragmaApiClient _apiClient;

    public DashboardController(IPragmaApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [HttpGet]
    [Route("")]
    [Route("Dashboard")]
    [Route("Dashboard/Index")]
    public async Task<IActionResult> Index()
    {
        var coursesTask = _apiClient.GetCoursesAsync(pageSize: 50);
        var categoriesTask = _apiClient.GetCategoriesAsync();
        var leaderboardTask = _apiClient.GetLeaderboardAsync(top: 5);

        await Task.WhenAll(coursesTask, categoriesTask, leaderboardTask);

        var courses = await coursesTask;
        var categories = await categoriesTask;
        var leaderboard = await leaderboardTask;

        var vm = new DashboardViewModel
        {
            TotalCourses = courses?.TotalCount ?? 0,
            PublishedCourses = courses?.Items.Count(c => c.Status == CourseStatus.Published) ?? 0,
            TotalCategories = categories?.Count ?? 0,
            RecentCourses = courses?.Items.Take(6).ToList() ?? [],
            TopStudents = leaderboard ?? []
        };

        return View(vm);
    }
}
