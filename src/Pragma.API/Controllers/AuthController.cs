using Asp.Versioning;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Pragma.Application.Common;
using Pragma.Application.Contracts;
using Pragma.Application.DTOs.Auth;
using Pragma.Domain.Enums;
using Pragma.Infrastructure.Identity;

namespace Pragma.API.Controllers;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/auth")]
[Route("api/auth")]
[ApiController]
public class AuthController : BaseApiController
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly IGoogleAuthService _googleAuthService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuthService _authService;
    private readonly ITokenService _tokenService;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;
    private readonly IValidator<RegisterRequestDto> _registerValidator;
    private readonly IValidator<LoginRequestDto> _loginValidator;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        IGoogleAuthService googleAuthService,
        ICurrentUserService currentUserService,
        IAuthService authService,
        ITokenService tokenService,
        IConfiguration configuration,
        IWebHostEnvironment environment,
        IValidator<RegisterRequestDto> registerValidator,
        IValidator<LoginRequestDto> loginValidator)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _googleAuthService = googleAuthService;
        _currentUserService = currentUserService;
        _authService = authService;
        _tokenService = tokenService;
        _configuration = configuration;
        _environment = environment;
        _registerValidator = registerValidator;
        _loginValidator = loginValidator;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponseDto>> Register([FromBody] RegisterRequestDto request)
    {
        var validationResult = await _registerValidator.ValidateAsync(request);
        if (!validationResult.IsValid)
        {
            var errors = string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage));
            return HandleResult(Result<AuthResponseDto>.Failure(Error.Validation("Auth.ValidationFailed", errors)));
        }

        var existingUser = await _userManager.FindByEmailAsync(request.Email);
        if (existingUser != null)
        {
            return HandleResult(Result<AuthResponseDto>.Failure(Error.Conflict("Auth.EmailExists", "Bu email ünvanı ilə artıq qeydiyyatdan keçilib.")));
        }

        var user = new ApplicationUser
        {
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Email = request.Email.Trim().ToLowerInvariant(),
            UserName = request.Email.Trim().ToLowerInvariant(),
            EmailConfirmed = true,
            IsActive = true
        };

        var createResult = await _userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            var errors = string.Join("; ", createResult.Errors.Select(e => e.Description));
            return HandleResult(Result<AuthResponseDto>.Failure(Error.Failure("Auth.CreationFailed", errors)));
        }

        var role = request.Role == UserRoles.Instructor ? UserRoles.Instructor : UserRoles.Student;
        if (!await _roleManager.RoleExistsAsync(role))
        {
            await _roleManager.CreateAsync(new ApplicationRole(role));
        }

        await _userManager.AddToRoleAsync(user, role);

        var roles = new[] { role };
        var passwordExpiryMinutes = int.TryParse(_configuration["Jwt:PasswordLoginExpiryMinutes"], out var pMinutes) && pMinutes > 0
            ? pMinutes
            : (int.TryParse(_configuration["Jwt:ExpiryMinutes"], out var oldM) ? oldM : 60);

        var tokenResult = await _tokenService.GenerateTokensAsync(
            user.Id,
            user.Email!,
            user.FullName,
            roles,
            GetIpAddress(),
            GetUserAgent(),
            customExpiryMinutes: passwordExpiryMinutes,
            cancellationToken: HttpContext.RequestAborted);

        SetRefreshTokenCookie(tokenResult.RefreshToken, tokenResult.ExpiresUtc);

        var response = new AuthResponseDto(
            AccessToken: tokenResult.AccessToken,
            ExpiresInMinutes: tokenResult.ExpiresInMinutes,
            User: new UserDto(user.Id, user.Email!, user.FirstName, user.LastName, user.FullName, user.ProfilePictureUrl, roles),
            RefreshToken: tokenResult.RefreshToken
        );

        return Ok(response);
    }

    [EnableRateLimiting("auth")]
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponseDto>> Login([FromBody] LoginRequestDto request)
    {
        var validationResult = await _loginValidator.ValidateAsync(request);
        if (!validationResult.IsValid)
        {
            var errors = string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage));
            return HandleResult(Result<AuthResponseDto>.Failure(Error.Validation("Auth.ValidationFailed", errors)));
        }

        var user = await _userManager.FindByEmailAsync(request.Email.Trim().ToLowerInvariant());
        if (user == null || user.IsDeleted)
        {
            return HandleResult(Result<AuthResponseDto>.Failure(Error.Unauthorized("Auth.InvalidCredentials", "Email və ya şifrə yanlışdır.")));
        }

        if (!user.IsActive)
        {
            return HandleResult(Result<AuthResponseDto>.Failure(Error.Forbidden("Auth.Deactivated", "Hesab deaktiv edilib.")));
        }

        var isPasswordValid = await _userManager.CheckPasswordAsync(user, request.Password);
        if (!isPasswordValid)
        {
            return HandleResult(Result<AuthResponseDto>.Failure(Error.Unauthorized("Auth.InvalidCredentials", "Email və ya şifrə yanlışdır.")));
        }

        var roles = await _userManager.GetRolesAsync(user);
        var passwordExpiryMinutes = int.TryParse(_configuration["Jwt:PasswordLoginExpiryMinutes"], out var loginPMinutes) && loginPMinutes > 0
            ? loginPMinutes
            : (int.TryParse(_configuration["Jwt:ExpiryMinutes"], out var oldLoginM) ? oldLoginM : 60);

        var tokenResult = await _tokenService.GenerateTokensAsync(
            user.Id,
            user.Email!,
            user.FullName,
            roles,
            GetIpAddress(),
            GetUserAgent(),
            customExpiryMinutes: passwordExpiryMinutes,
            cancellationToken: HttpContext.RequestAborted);

        SetRefreshTokenCookie(tokenResult.RefreshToken, tokenResult.ExpiresUtc);

        var response = new AuthResponseDto(
            AccessToken: tokenResult.AccessToken,
            ExpiresInMinutes: tokenResult.ExpiresInMinutes,
            User: new UserDto(user.Id, user.Email!, user.FirstName, user.LastName, user.FullName, user.ProfilePictureUrl, roles),
            RefreshToken: tokenResult.RefreshToken
        );

        return Ok(response);
    }

    [EnableRateLimiting("auth")]
    [HttpPost("google")]
    [HttpPost("google-login")]
    public async Task<IActionResult> GoogleLogin(
        [FromBody] GoogleLoginRequestDto request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.IdToken))
        {
            return BadRequest(new { title = "Validation Error", detail = "Google IdToken is required." });
        }

        var result = await _authService.GoogleLoginAsync(request.IdToken, GetIpAddress(), GetUserAgent(), ct);
        if (result.IsFailure)
        {
            return BadRequest(new { title = "Authentication Failed", detail = result.Error.Message });
        }

        SetRefreshTokenCookie(result.Value.RefreshToken, result.Value.ExpiresUtc);
        return Ok(result.Value);
    }

    [EnableRateLimiting("auth")]
    [HttpPost("passwordless/request")]
    public async Task<IActionResult> PasswordlessRequest(
        [FromBody] PasswordlessRequestDto dto,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Email))
        {
            return BadRequest(new { title = "Validation Error", detail = "Email is required." });
        }

        await _authService.RequestPasswordlessCodeAsync(dto.Email, GetIpAddress(), ct);

        return Accepted(new
        {
            message = "If the email address is valid, a 6-digit verification code has been dispatched."
        });
    }

    [EnableRateLimiting("auth")]
    [HttpPost("passwordless/verify")]
    public async Task<IActionResult> PasswordlessVerify(
        [FromBody] PasswordlessVerifyDto dto,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Code))
        {
            return BadRequest(new { title = "Validation Error", detail = "Email and 6-digit code are required." });
        }

        var result = await _authService.VerifyPasswordlessCodeAsync(dto.Email, dto.Code, GetIpAddress(), GetUserAgent(), ct);
        if (result.IsFailure)
        {
            return BadRequest(new { title = "Verification Failed", detail = result.Error.Message });
        }

        SetRefreshTokenCookie(result.Value.RefreshToken, result.Value.ExpiresUtc);
        return Ok(result.Value);
    }

    [EnableRateLimiting("auth")]
    [HttpPost("refresh")]
    public async Task<IActionResult> RefreshToken(
        [FromBody] RefreshTokenRequestDto? dto,
        CancellationToken ct)
    {
        var rawToken = dto?.RefreshToken;
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            Request.Cookies.TryGetValue("pragma_rt", out rawToken);
        }

        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return Unauthorized(new { title = "Unauthorized", detail = "Refresh token is missing." });
        }

        var result = await _tokenService.RotateRefreshTokenAsync(rawToken, GetIpAddress(), GetUserAgent(), ct);
        if (result.IsFailure)
        {
            ClearRefreshTokenCookie();
            return Unauthorized(new { title = "Unauthorized", detail = result.Error.Message });
        }

        SetRefreshTokenCookie(result.Value.RefreshToken, result.Value.ExpiresUtc);
        return Ok(result.Value);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(
        [FromBody] RefreshTokenRequestDto? dto,
        CancellationToken ct)
    {
        var rawToken = dto?.RefreshToken;
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            Request.Cookies.TryGetValue("pragma_rt", out rawToken);
        }

        if (!string.IsNullOrWhiteSpace(rawToken))
        {
            await _tokenService.RevokeRefreshTokenAsync(rawToken, ct);
        }

        ClearRefreshTokenCookie();
        return Ok(new { message = "Logged out successfully." });
    }

    [Authorize]
    [HttpPost("logout-all")]
    public async Task<IActionResult> LogoutAll(CancellationToken ct)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId.HasValue)
        {
            await _tokenService.RevokeAllUserTokensAsync(currentUserId.Value, ct);
        }

        ClearRefreshTokenCookie();
        return Ok(new { message = "All active sessions revoked successfully." });
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentUser(CancellationToken ct)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue)
        {
            return Unauthorized(new { title = "Unauthorized", detail = "User is not logged in." });
        }

        var result = await _authService.GetCurrentUserAsync(currentUserId.Value, ct);
        if (result.IsFailure)
        {
            return NotFound(new { title = "Not Found", detail = result.Error.Message });
        }

        return Ok(result.Value);
    }

    private void SetRefreshTokenCookie(string refreshToken, DateTime expiresUtc)
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps || _environment.IsProduction(),
            SameSite = SameSiteMode.Strict,
            Expires = expiresUtc,
            Path = "/"
        };
        Response.Cookies.Append("pragma_rt", refreshToken, cookieOptions);
    }

    private void ClearRefreshTokenCookie()
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps || _environment.IsProduction(),
            SameSite = SameSiteMode.Strict,
            Path = "/"
        };
        Response.Cookies.Delete("pragma_rt", cookieOptions);
    }

    private string? GetIpAddress() => HttpContext.Connection.RemoteIpAddress?.ToString();

    private string? GetUserAgent() => Request.Headers.UserAgent.ToString();
}
