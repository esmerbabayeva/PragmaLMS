using System.ComponentModel.DataAnnotations;
using Pragma.Application.DTOs.Categories;
using Pragma.Application.DTOs.Courses;
using Pragma.Application.DTOs.Gamification;

namespace Pragma.Admin.Models;

public class LoginViewModel
{
    [Required(ErrorMessage = "Email daxil edilməlidir")]
    [EmailAddress(ErrorMessage = "Düzgün email formatı daxil edin")]
    public string Email { get; set; } = "admin@pragma.edu.az";

    [Required(ErrorMessage = "Şifrə daxil edilməlidir")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = "PragmaAdmin2026!";

    public string? ErrorMessage { get; set; }
}

public class DashboardViewModel
{
    public int TotalCourses { get; set; }
    public int PublishedCourses { get; set; }
    public int TotalCategories { get; set; }
    public IReadOnlyList<CourseSummaryDto> RecentCourses { get; set; } = [];
    public IReadOnlyList<LeaderboardEntryDto> TopStudents { get; set; } = [];
}

public class CategoryListViewModel
{
    public IReadOnlyList<CategoryDto> Categories { get; set; } = [];
    public CreateCategoryViewModel NewCategory { get; set; } = new();
}

public class CreateCategoryViewModel
{
    [Required(ErrorMessage = "Kateqoriya adı mütləqdir")]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }
    public string? IconName { get; set; }
    public int SortOrder { get; set; } = 1;
}
