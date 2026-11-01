namespace Pragma.Application.Contracts;

/// <summary>
/// İstifadəçi üçün JWT Access və Refresh Token formalaşdıran servis interfeysi.
/// </summary>
public interface IJwtTokenGenerator
{
    string GenerateAccessToken(Guid userId, string email, string fullName, IEnumerable<string> roles);
    string GenerateRefreshToken();
}
