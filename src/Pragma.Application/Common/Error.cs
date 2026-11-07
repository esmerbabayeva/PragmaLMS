namespace Pragma.Application.Common;

/// <summary>
/// Biznes xətasının təsviri üçün dəyər tipi (Value Object).
/// </summary>
public record Error(string Code, string Message, ErrorType Type = ErrorType.Failure)
{
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.Failure);
    public static readonly Error NullValue = new("Error.NullValue", "Tələb olunan dəyər boşdur (null).", ErrorType.Validation);

    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);
    public static Error NotFound(string message) => new("General.NotFound", message, ErrorType.NotFound);

    public static Error Validation(string code, string message) => new(code, message, ErrorType.Validation);
    public static Error Validation(string message) => new("Validation.Error", message, ErrorType.Validation);

    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);
    public static Error Conflict(string message) => new("General.Conflict", message, ErrorType.Conflict);

    public static Error Unauthorized(string code, string message) => new(code, message, ErrorType.Unauthorized);
    public static Error Unauthorized(string message) => new("Auth.Unauthorized", message, ErrorType.Unauthorized);

    public static Error Forbidden(string code, string message) => new(code, message, ErrorType.Forbidden);
    public static Error Forbidden(string message) => new("Auth.Forbidden", message, ErrorType.Forbidden);

    public static Error Failure(string code, string message) => new(code, message, ErrorType.Failure);
    public static Error Failure(string message) => new("General.Failure", message, ErrorType.Failure);
}

public enum ErrorType
{
    Failure = 1,
    Validation = 2,
    NotFound = 3,
    Conflict = 4,
    Unauthorized = 5,
    Forbidden = 6
}
