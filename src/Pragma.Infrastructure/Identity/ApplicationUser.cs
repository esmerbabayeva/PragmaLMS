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
    public string? AvatarUrl
    {
        get => ProfilePictureUrl;
        set => ProfilePictureUrl = value;
    }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
    public bool IsDeleted { get; set; } = false;

    public string FullName => $"{FirstName} {LastName}".Trim();
}
