using Google.Apis.Auth;
using Microsoft.Extensions.Configuration;
using Pragma.Application.Common;
using Pragma.Application.Contracts;

namespace Pragma.Infrastructure.Services;

/// <summary>
/// Google Identity Services (GIS) One-Tap token yoxlama servisi.
/// </summary>
public class GoogleAuthService : IGoogleAuthService
{
    private readonly IConfiguration _configuration;

    public GoogleAuthService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<Result<GoogleUserInfo>> ValidateIdTokenAsync(string idToken, CancellationToken cancellationToken = default)
    {
        try
        {
            var clientId = _configuration["Google:ClientId"];

            var settings = new GoogleJsonWebSignature.ValidationSettings();
            if (!string.IsNullOrWhiteSpace(clientId))
            {
                settings.Audience = new[] { clientId };
            }

            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, settings);
            if (payload == null)
            {
                return Result<GoogleUserInfo>.Failure(Error.Unauthorized("Auth.GoogleTokenInvalid", "Google token təsdiqlənmədi."));
            }

            var userInfo = new GoogleUserInfo(
                Email: payload.Email,
                FirstName: payload.GivenName ?? payload.Name ?? "İstifadəçi",
                LastName: payload.FamilyName ?? string.Empty,
                PictureUrl: payload.Picture
            );

            return Result<GoogleUserInfo>.Success(userInfo);
        }
        catch (InvalidJwtException ex)
        {
            return Result<GoogleUserInfo>.Failure(Error.Unauthorized("Auth.GoogleTokenInvalid", $"Google token etibarsızdır: {ex.Message}"));
        }
        catch (Exception ex)
        {
            return Result<GoogleUserInfo>.Failure(Error.Failure("Auth.GoogleAuthError", $"Google xidməti ilə əlaqə xətası: {ex.Message}"));
        }
    }
}
