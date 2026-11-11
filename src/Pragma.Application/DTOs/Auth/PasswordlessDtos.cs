namespace Pragma.Application.DTOs.Auth;

public record PasswordlessRequestDto(string Email);

public record PasswordlessVerifyDto(string Email, string Code);

public record RefreshTokenRequestDto(string? RefreshToken);

public record CurrentUserDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string FullName,
    string? ProfilePictureUrl,
    string? AvatarUrl,
    IEnumerable<string> Roles,
    bool IsActive);

public record TokenResultDto(
    string AccessToken,
    string RefreshToken,
    int ExpiresInMinutes,
    DateTime ExpiresUtc,
    UserDto User);
