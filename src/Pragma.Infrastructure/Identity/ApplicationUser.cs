using Microsoft.AspNetCore.Identity;

namespace Pragma.Infrastructure.Identity;

/// <summary>
/// Sistem istifadəçisi üçün ASP.NET Identity modeli (Guid açarlı).
/// </summary>
public class ApplicationUser : IdentityUser<Guid>
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? ProfilePictureUrl { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
    public bool IsDeleted { get; set; } = false;

    public string FullName => $"{FirstName} {LastName}".Trim();
}
