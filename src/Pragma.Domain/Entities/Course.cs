using Pragma.Domain.Common;
using Pragma.Domain.Enums;

namespace Pragma.Domain.Entities;

/// <summary>
/// Sistemdəki əsas kurs modeli.
/// </summary>
public class Course : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? ShortDescription { get; set; }
    public string? Description { get; set; }
    public string? ThumbnailUrl { get; set; }
    
    public decimal Price { get; set; } = 0m;
    public bool IsFree { get; set; } = true;
    
    public CourseLevel Level { get; set; } = CourseLevel.Beginner;
    public CourseStatus Status { get; set; } = CourseStatus.Draft;
    
    // Təlimçi identifikasiyası
    public Guid InstructorId { get; set; }
    
    // Kateqoriya
    public Guid CategoryId { get; set; }
    public Category? Category { get; set; }
    
    // İcazə və təsdiq məlumatları
    public DateTime? ApprovedAtUtc { get; set; }
    public string? RejectionReason { get; set; }

    // Naviqasiya əlaqələri
    public ICollection<Module> Modules { get; set; } = new List<Module>();
    public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
    public ICollection<Review> Reviews { get; set; } = new List<Review>();
    public ICollection<Certificate> Certificates { get; set; } = new List<Certificate>();
}
