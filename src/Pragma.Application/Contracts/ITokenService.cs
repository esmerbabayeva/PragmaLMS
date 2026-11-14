using Pragma.Application.Common;
using Pragma.Application.DTOs.Auth;

namespace Pragma.Application.Contracts;

public interface ITokenService
{
    Task<TokenResultDto> GenerateTokensAsync(
        Guid userId,
        string email,
        string fullName,
        IEnumerable<string> roles,
        string? ipAddress,
        string? userAgent,
        Guid? existingFamilyId = null,
        int? customExpiryMinutes = null,
        CancellationToken cancellationToken = default);

    Task<Result<TokenResultDto>> RotateRefreshTokenAsync(
        string rawRefreshToken,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default);

    Task<Result<bool>> RevokeRefreshTokenAsync(
        string rawRefreshToken,
        CancellationToken cancellationToken = default);

    Task<Result<bool>> RevokeAllUserTokensAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
