namespace Pragma.Application.DTOs.Auth;

/// <summary>
/// Email və şifrə ilə daxilolma sorğu DTO-su.
/// </summary>
public record LoginRequestDto(
    string Email,
    string Password
);
