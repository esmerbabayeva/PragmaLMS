namespace Pragma.Application.DTOs.Auth;

public record UserDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string FullName,
    string? ProfilePictureUrl,
    IEnumerable<string> Roles
);

public record AuthResponseDto(
    string AccessToken,
    int ExpiresInMinutes,
    UserDto User
);
