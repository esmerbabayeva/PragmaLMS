using System.ComponentModel.DataAnnotations;
using Pragma.Application.Common;
using Pragma.Application.DTOs.Categories;
using Pragma.Application.DTOs.Courses;
using Pragma.Application.DTOs.Gamification;
using Pragma.Domain.Enums;

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
    [Required(ErrorMessage = "Kateqoriya adı (AZ) mütləqdir")]
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public string? NameEn { get; set; }
    public string? DescriptionEn { get; set; }

    public string? NameRu { get; set; }
    public string? DescriptionRu { get; set; }

    public string? IconName { get; set; }
    public int SortOrder { get; set; } = 1;
}

public class CourseListViewModel
{
    public PagedResult<CourseSummaryDto> Courses { get; set; } = new([], 0, 1, 50);
    public IReadOnlyList<CategoryDto> Categories { get; set; } = [];
    public CreateCourseViewModel NewCourse { get; set; } = new();
}

public class CreateCourseViewModel
{
    [Required(ErrorMessage = "Kurs başlığı (AZ) mütləqdir")]
    public string Title { get; set; } = string.Empty;
    public string? ShortDescription { get; set; }
    public string? Description { get; set; }

    public string? TitleEn { get; set; }
    public string? ShortDescriptionEn { get; set; }
    public string? DescriptionEn { get; set; }

    public string? TitleRu { get; set; }
    public string? ShortDescriptionRu { get; set; }
    public string? DescriptionRu { get; set; }

    public decimal Price { get; set; }
    public bool IsFree { get; set; } = true;
    public CourseLevel Level { get; set; } = CourseLevel.Beginner;

    [Required(ErrorMessage = "Kateqoriya seçilməlidir")]
    public Guid CategoryId { get; set; }

    public string? ThumbnailUrl { get; set; }
}
