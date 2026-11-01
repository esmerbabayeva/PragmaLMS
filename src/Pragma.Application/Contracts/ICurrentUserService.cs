namespace Pragma.Application.Contracts;

/// <summary>
/// HttpContext üzərindən cari daxil olmuş istifadəçinin Claim-lərini təqdim edən servis interfeysi.
/// </summary>
public interface ICurrentUserService
{
    Guid? UserId { get; }
    string? Email { get; }
    bool IsAuthenticated { get; }
    bool IsInRole(string role);
}
