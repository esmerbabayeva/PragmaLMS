using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pragma.Application.Common;
using Pragma.Application.Contracts;
using Pragma.Application.DTOs.Auth;
using Pragma.Domain.Entities;
using Pragma.Domain.Enums;
using Pragma.Infrastructure.Identity;

namespace Pragma.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly IAppDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IGoogleTokenValidator _googleTokenValidator;
    private readonly IOtpService _otpService;
    private readonly ITokenService _tokenService;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IAppDbContext context,
        UserManager<ApplicationUser> userManager,
        IGoogleTokenValidator googleTokenValidator,
        IOtpService otpService,
        ITokenService tokenService,
        IEmailSender emailSender,
        ILogger<AuthService> logger)
    {
        _context = context;
        _userManager = userManager;
        _googleTokenValidator = googleTokenValidator;
        _otpService = otpService;
        _tokenService = tokenService;
        _emailSender = emailSender;
        _logger = logger;
    }

    public async Task<Result<TokenResultDto>> GoogleLoginAsync(
        string idToken,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        var googleIdentity = await _googleTokenValidator.ValidateAsync(idToken, cancellationToken);
        if (googleIdentity == null)
        {
            return Result<TokenResultDto>.Failure(Error.Unauthorized("Auth.InvalidGoogleToken", "Invalid Google credentials."));
        }

        if (!googleIdentity.EmailVerified)
        {
            return Result<TokenResultDto>.Failure(Error.Unauthorized("Auth.UnverifiedEmail", "Google email address is not verified."));
        }

        var user = await _userManager.FindByLoginAsync("Google", googleIdentity.Subject);

        if (user == null)
        {
            user = await _userManager.FindByEmailAsync(googleIdentity.Email);
            if (user != null)
            {
                var addLoginResult = await _userManager.AddLoginAsync(user, new UserLoginInfo("Google", googleIdentity.Subject, "Google"));
                if (!addLoginResult.Succeeded)
                {
                    _logger.LogWarning("Failed to link Google login to existing user {Email}", user.Email);
                }
            }
            else
            {
                var displayName = !string.IsNullOrWhiteSpace(googleIdentity.Name)
                    ? googleIdentity.Name.Trim()
                    : googleIdentity.Email.Split('@')[0];
                var nameParts = displayName.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);

                user = new ApplicationUser
                {
                    Id = Guid.NewGuid(),
                    UserName = googleIdentity.Email,
                    Email = googleIdentity.Email,
                    EmailConfirmed = true,
                    FirstName = nameParts.Length > 0 ? nameParts[0] : displayName,
                    LastName = nameParts.Length > 1 ? nameParts[1] : string.Empty,
                    ProfilePictureUrl = googleIdentity.PictureUrl,
                    IsActive = true
                };

                var createResult = await _userManager.CreateAsync(user);
                if (!createResult.Succeeded)
                {
                    var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                    return Result<TokenResultDto>.Failure(Error.Failure("Auth.UserCreationFailed", $"Failed to create user: {errors}"));
                }

                await _userManager.AddToRoleAsync(user, UserRoles.Student);
                await _userManager.AddLoginAsync(user, new UserLoginInfo("Google", googleIdentity.Subject, "Google"));
            }
        }

        if (!user.IsActive)
        {
            return Result<TokenResultDto>.Failure(Error.Forbidden("Auth.AccountDeactivated", "Account is deactivated. Please contact support."));
        }

        var roles = await _userManager.GetRolesAsync(user);
        var tokens = await _tokenService.GenerateTokensAsync(
            user.Id,
            user.Email ?? googleIdentity.Email,
            user.FullName,
            roles,
            ipAddress,
            userAgent,
            cancellationToken: cancellationToken);

        return Result<TokenResultDto>.Success(tokens);
    }

    public async Task<Result<bool>> RequestPasswordlessCodeAsync(
        string email,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return Result<bool>.Failure(Error.Validation("Auth.EmailRequired", "Email is required."));
        }

        var normalizedEmail = email.Trim().ToUpperInvariant();

        var recentCount = await _context.AuthChallenges
            .CountAsync(c => c.EmailNormalized == normalizedEmail && c.CreatedAtUtc > DateTime.UtcNow.AddMinutes(-15), cancellationToken);

        if (recentCount >= 3)
        {
            _logger.LogWarning("Rate limit reached for passwordless email requests: {Email}", email);
            return Result<bool>.Success(true);
        }

        var existingChallenges = await _context.AuthChallenges
            .Where(c => c.EmailNormalized == normalizedEmail && c.ConsumedUtc == null && c.ExpiresUtc > DateTime.UtcNow)
            .ToListAsync(cancellationToken);

        foreach (var c in existingChallenges)
        {
            c.ConsumedUtc = DateTime.UtcNow;
        }

        var code = _otpService.GenerateCode();
        var codeHash = _otpService.HashCode(code);

        var challenge = new AuthChallenge
        {
            EmailNormalized = normalizedEmail,
            CodeHash = codeHash,
            ExpiresUtc = DateTime.UtcNow.AddMinutes(10),
            Attempts = 0,
            RequestIpHash = HashIp(ipAddress)
        };

        _context.AuthChallenges.Add(challenge);
        await _context.SaveChangesAsync(cancellationToken);

        var subject = "Pragma LMS — Giriş Kodunuz";
        var body = $"""
            <div style="font-family: Arial, sans-serif; max-width: 600px; margin: auto; padding: 20px; border: 1px solid #e0e0e0; border-radius: 8px;">
                <h2 style="color: #4F46E5; margin-bottom: 16px;">Pragma LMS Şifrəsiz Giriş</h2>
                <p>Salam,</p>
                <p>Sizin hesabınıza daxil olmaq üçün birdəfəlik təhlükəsizlik kodunuz:</p>
                <div style="font-size: 32px; font-weight: bold; letter-spacing: 6px; color: #111827; background-color: #F3F4F6; padding: 12px 24px; text-align: center; border-radius: 6px; margin: 20px 0;">
                    {code}
                </div>
                <p style="color: #6B7280; font-size: 14px;">Bu kod <strong>10 dəqiqə</strong> ərzində etibarlıdır və yalnız bir dəfə istifadə oluna bilər.</p>
                <p style="color: #9CA3AF; font-size: 12px; margin-top: 30px;">Əgər bu sorğunu siz etməmisinizsə, zəhmət olmasa bu məktubu nəzərə almayın.</p>
            </div>
            """;

        try
        {
            await _emailSender.SendAsync(email.Trim(), subject, body, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to dispatch login email to {Email}", email);
        }

        return Result<bool>.Success(true);
    }

    public async Task<Result<TokenResultDto>> VerifyPasswordlessCodeAsync(
        string email,
        string code,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(code))
        {
            return Result<TokenResultDto>.Failure(Error.Validation("Auth.ParametersRequired", "Email and code are required."));
        }

        var normalizedEmail = email.Trim().ToUpperInvariant();

        var challenge = await _context.AuthChallenges
            .Where(c => c.EmailNormalized == normalizedEmail && c.ConsumedUtc == null)
            .OrderByDescending(c => c.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (challenge == null || challenge.ExpiresUtc < DateTime.UtcNow)
        {
            return Result<TokenResultDto>.Failure(Error.Unauthorized("Auth.InvalidCode", "Invalid or expired verification code."));
        }

        challenge.Attempts++;

        if (challenge.Attempts > 5)
        {
            challenge.ConsumedUtc = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
            return Result<TokenResultDto>.Failure(Error.Unauthorized("Auth.TooManyAttempts", "Maximum verification attempts exceeded. Code invalidated."));
        }

        if (!_otpService.VerifyCode(code.Trim(), challenge.CodeHash))
        {
            await _context.SaveChangesAsync(cancellationToken);
            return Result<TokenResultDto>.Failure(Error.Unauthorized("Auth.InvalidCode", "Invalid verification code."));
        }

        challenge.ConsumedUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        var user = await _userManager.FindByEmailAsync(email.Trim());
        if (user == null)
        {
            var emailPrefix = email.Split('@')[0];
            user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = email.Trim(),
                Email = email.Trim(),
                EmailConfirmed = true,
                FirstName = emailPrefix,
                LastName = string.Empty,
                IsActive = true
            };

            var createResult = await _userManager.CreateAsync(user);
            if (!createResult.Succeeded)
            {
                var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                return Result<TokenResultDto>.Failure(Error.Failure("Auth.UserCreationFailed", $"Failed to create user: {errors}"));
            }

            await _userManager.AddToRoleAsync(user, UserRoles.Student);
        }
        else
        {
            if (!user.EmailConfirmed)
            {
                user.EmailConfirmed = true;
                await _userManager.UpdateAsync(user);
            }
        }

        if (!user.IsActive)
        {
            return Result<TokenResultDto>.Failure(Error.Forbidden("Auth.AccountDeactivated", "Account is deactivated. Please contact support."));
        }

        var roles = await _userManager.GetRolesAsync(user);
        var tokens = await _tokenService.GenerateTokensAsync(
            user.Id,
            user.Email ?? email.Trim(),
            user.FullName,
            roles,
            ipAddress,
            userAgent,
            cancellationToken: cancellationToken);

        return Result<TokenResultDto>.Success(tokens);
    }

    public async Task<Result<CurrentUserDto>> GetCurrentUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return Result<CurrentUserDto>.Failure(Error.NotFound("Auth.UserNotFound", "User not found."));
        }

        var roles = await _userManager.GetRolesAsync(user);

        var dto = new CurrentUserDto(
            Id: user.Id,
            Email: user.Email ?? string.Empty,
            FirstName: user.FirstName,
            LastName: user.LastName,
            FullName: user.FullName,
            ProfilePictureUrl: user.ProfilePictureUrl,
            AvatarUrl: user.AvatarUrl,
            Roles: roles,
            IsActive: user.IsActive);

        return Result<CurrentUserDto>.Success(dto);
    }

    private static string? HashIp(string? ip)
    {
        if (string.IsNullOrWhiteSpace(ip)) return null;
        var bytes = Encoding.UTF8.GetBytes(ip);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
