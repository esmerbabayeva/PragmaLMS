namespace Pragma.Application.DTOs.Auth;

/// <summary>
/// Google One-Tap id_token ilə 1 kliklə daxilolma sorğusu.
/// </summary>
public record GoogleLoginRequestDto(
    string IdToken
);
