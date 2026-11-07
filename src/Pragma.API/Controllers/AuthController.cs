using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Pragma.Application.Common;
using Pragma.Application.Contracts;
using Pragma.Application.DTOs.Auth;
using Pragma.Domain.Enums;
using Pragma.Infrastructure.Identity;

namespace Pragma.API.Controllers;

/// <summary>
/// İstifadəçi qeydiyyatı, giriş və autentifikasiya əməliyyatları.
/// </summary>
[Route("api/auth")]
public class AuthController : BaseApiController
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IGoogleAuthService _googleAuthService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IValidator<RegisterRequestDto> _registerValidator;
    private readonly IValidator<LoginRequestDto> _loginValidator;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        IJwtTokenGenerator jwtTokenGenerator,
        IGoogleAuthService googleAuthService,
        ICurrentUserService currentUserService,
        IValidator<RegisterRequestDto> registerValidator,
        IValidator<LoginRequestDto> loginValidator)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _jwtTokenGenerator = jwtTokenGenerator;
        _googleAuthService = googleAuthService;
        _currentUserService = currentUserService;
        _registerValidator = registerValidator;
        _loginValidator = loginValidator;
    }

    /// <summary>
    /// Email və şifrə ilə yeni tələbə və ya müəllim qeydiyyatı.
    /// </summary>
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
            EmailConfirmed = true
        };

        var createResult = await _userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            var errors = string.Join("; ", createResult.Errors.Select(e => e.Description));
            return HandleResult(Result<AuthResponseDto>.Failure(Error.Failure("Auth.CreationFailed", errors)));
        }

        // Rol təyini
        var role = request.Role == UserRoles.Instructor ? UserRoles.Instructor : UserRoles.Student;
        if (!await _roleManager.RoleExistsAsync(role))
        {
            await _roleManager.CreateAsync(new ApplicationRole(role));
        }

        await _userManager.AddToRoleAsync(user, role);

        var roles = new[] { role };
        var token = _jwtTokenGenerator.GenerateAccessToken(user.Id, user.Email!, user.FullName, roles);

        var response = new AuthResponseDto(
            AccessToken: token,
            ExpiresInMinutes: 60,
            User: new UserDto(user.Id, user.Email!, user.FirstName, user.LastName, user.FullName, user.ProfilePictureUrl, roles)
        );

        return Ok(response);
    }

    /// <summary>
    /// Email və şifrə ilə sistemə daxilolma və JWT token əldə edilməsi.
    /// </summary>
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

        var isPasswordValid = await _userManager.CheckPasswordAsync(user, request.Password);
        if (!isPasswordValid)
        {
            return HandleResult(Result<AuthResponseDto>.Failure(Error.Unauthorized("Auth.InvalidCredentials", "Email və ya şifrə yanlışdır.")));
        }

        var roles = await _userManager.GetRolesAsync(user);
        var token = _jwtTokenGenerator.GenerateAccessToken(user.Id, user.Email!, user.FullName, roles);

        var response = new AuthResponseDto(
            AccessToken: token,
            ExpiresInMinutes: 60,
            User: new UserDto(user.Id, user.Email!, user.FirstName, user.LastName, user.FullName, user.ProfilePictureUrl, roles)
        );

        return Ok(response);
    }

    /// <summary>
    /// Google One-Tap id_token ilə 1-kliklə ani daxilolma və ya avtomatik qeydiyyat.
    /// </summary>
    [HttpPost("google-login")]
    public async Task<ActionResult<AuthResponseDto>> GoogleLogin([FromBody] GoogleLoginRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.IdToken))
        {
            return HandleResult(Result<AuthResponseDto>.Failure(Error.Validation("Auth.TokenRequired", "Google IdToken boş ola bilməz.")));
        }

        var validateResult = await _googleAuthService.ValidateIdTokenAsync(request.IdToken);
        if (validateResult.IsFailure)
        {
            return HandleResult(Result<AuthResponseDto>.Failure(validateResult.Error));
        }

        var googleUser = validateResult.Value;
        var user = await _userManager.FindByEmailAsync(googleUser.Email.ToLowerInvariant());

        if (user == null)
        {
            // Yeni tələbə kimi avtomatik yaradılır
            user = new ApplicationUser
            {
                FirstName = googleUser.FirstName,
                LastName = googleUser.LastName,
                Email = googleUser.Email.ToLowerInvariant(),
                UserName = googleUser.Email.ToLowerInvariant(),
                ProfilePictureUrl = googleUser.PictureUrl,
                EmailConfirmed = true
            };

            var createResult = await _userManager.CreateAsync(user);
            if (!createResult.Succeeded)
            {
                var errors = string.Join("; ", createResult.Errors.Select(e => e.Description));
                return HandleResult(Result<AuthResponseDto>.Failure(Error.Failure("Auth.GoogleCreationFailed", errors)));
            }

            if (!await _roleManager.RoleExistsAsync(UserRoles.Student))
            {
                await _roleManager.CreateAsync(new ApplicationRole(UserRoles.Student));
            }

            await _userManager.AddToRoleAsync(user, UserRoles.Student);
        }

        var roles = await _userManager.GetRolesAsync(user);
        var token = _jwtTokenGenerator.GenerateAccessToken(user.Id, user.Email!, user.FullName, roles);

        var response = new AuthResponseDto(
            AccessToken: token,
            ExpiresInMinutes: 60,
            User: new UserDto(user.Id, user.Email!, user.FirstName, user.LastName, user.FullName, user.ProfilePictureUrl, roles)
        );

        return Ok(response);
    }

    /// <summary>
    /// Cari autentifikasiyadan keçmiş istifadəçinin profil məlumatları.
    /// </summary>
    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserDto>> GetCurrentUser()
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue)
        {
            return HandleResult(Result<UserDto>.Failure(Error.Unauthorized("Auth.Unauthorized", "İstifadəçi daxil olmayıb.")));
        }

        var user = await _userManager.FindByIdAsync(currentUserId.Value.ToString());
        if (user == null || user.IsDeleted)
        {
            return HandleResult(Result<UserDto>.Failure(Error.NotFound("Auth.UserNotFound", "İstifadəçi tapılmadı.")));
        }

        var roles = await _userManager.GetRolesAsync(user);
        var userDto = new UserDto(user.Id, user.Email!, user.FirstName, user.LastName, user.FullName, user.ProfilePictureUrl, roles);

        return Ok(userDto);
    }
}
