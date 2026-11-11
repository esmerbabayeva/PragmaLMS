using Pragma.Application.Common;
using Pragma.Application.DTOs.Auth;

namespace Pragma.Application.Contracts;

public interface IAuthService
{
    Task<Result<TokenResultDto>> GoogleLoginAsync(
        string idToken,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default);

    Task<Result<bool>> RequestPasswordlessCodeAsync(
        string email,
        string? ipAddress,
        CancellationToken cancellationToken = default);

    Task<Result<TokenResultDto>> VerifyPasswordlessCodeAsync(
        string email,
        string code,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default);

    Task<Result<CurrentUserDto>> GetCurrentUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
