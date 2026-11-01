namespace Pragma.Application.DTOs.Auth;

/// <summary>
/// Yeni istifadəçi qeydiyyatı sorğu DTO-su.
/// </summary>
public record RegisterRequestDto(
    string FirstName,
    string LastName,
    string Email,
    string Password,
    string ConfirmPassword,
    string Role // "Student" və ya "Instructor"
);
