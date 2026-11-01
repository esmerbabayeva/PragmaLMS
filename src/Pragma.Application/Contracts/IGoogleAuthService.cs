using Pragma.Application.Common;

namespace Pragma.Application.Contracts;

public record GoogleUserInfo(string Email, string FirstName, string LastName, string? PictureUrl);

/// <summary>
/// Google One-Tap id_token təsdiqləmə servisi üçün interfeys.
/// </summary>
public interface IGoogleAuthService
{
    Task<Result<GoogleUserInfo>> ValidateIdTokenAsync(string idToken, CancellationToken cancellationToken = default);
}
