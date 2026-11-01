using Microsoft.AspNetCore.Identity;

namespace Pragma.Infrastructure.Identity;

/// <summary>
/// Sistem rolları üçün ASP.NET Identity modeli (Guid açarlı).
/// </summary>
public class ApplicationRole : IdentityRole<Guid>
{
    public ApplicationRole() : base() { }

    public ApplicationRole(string roleName) : base(roleName) { }
}
