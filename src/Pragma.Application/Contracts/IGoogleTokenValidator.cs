namespace Pragma.Application.Contracts;

public record GoogleIdentityDto(
    string Subject,
    string Email,
    bool EmailVerified,
    string Name,
    string? PictureUrl);

public interface IGoogleTokenValidator
{
    Task<GoogleIdentityDto?> ValidateAsync(string idToken, CancellationToken cancellationToken = default);
}
