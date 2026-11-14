using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Pragma.Application.Common;
using Pragma.Application.Contracts;
using Pragma.Application.DTOs.Auth;
using Pragma.Domain.Entities;
using Pragma.Infrastructure.Identity;

namespace Pragma.Infrastructure.Services;

public class TokenService : ITokenService
{
    private readonly IAppDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _configuration;

    public TokenService(
        IAppDbContext context,
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration)
    {
        _context = context;
        _userManager = userManager;
        _configuration = configuration;
    }

    private int GetPasswordlessExpiryMinutes()
    {
        // Google/OTP və token fırlanması (refresh) üçün təhlükəsizlik standartı: 15 dəqiqə
        if (int.TryParse(_configuration["Jwt:PasswordlessExpiryMinutes"], out var minutes) && minutes > 0)
        {
            return minutes;
        }

        return 15;
    }

    public async Task<TokenResultDto> GenerateTokensAsync(
        Guid userId,
        string email,
        string fullName,
        IEnumerable<string> roles,
        string? ipAddress,
        string? userAgent,
        Guid? existingFamilyId = null,
        int? customExpiryMinutes = null,
        CancellationToken cancellationToken = default)
    {
        var secretKey = _configuration["Jwt:SecretKey"] ?? "PragmaSuperSecretKeyForDevelopmentOnly1234567890!@#$";
        var issuer = _configuration["Jwt:Issuer"] ?? "PragmaAPI";
        var audience = _configuration["Jwt:Audience"] ?? "PragmaClient";

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(ClaimTypes.Email, email),
            new(JwtRegisteredClaimNames.Email, email),
            new(ClaimTypes.Name, fullName),
            new(JwtRegisteredClaimNames.Name, fullName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var expiryMinutes = customExpiryMinutes ?? GetPasswordlessExpiryMinutes();
        var expiresUtc = DateTime.UtcNow.AddMinutes(expiryMinutes);
        var jwt = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expiresUtc,
            signingCredentials: creds);

        var accessToken = new JwtSecurityTokenHandler().WriteToken(jwt);

        var rawRefreshToken = GenerateSecureTokenString();
        var tokenHash = HashToken(rawRefreshToken);
        var familyId = existingFamilyId ?? Guid.NewGuid();

        var refreshTokenEntity = new RefreshToken
        {
            UserId = userId,
            TokenHash = tokenHash,
            FamilyId = familyId,
            ExpiresUtc = DateTime.UtcNow.AddDays(14),
            CreatedByIp = ipAddress,
            UserAgent = userAgent
        };

        _context.RefreshTokens.Add(refreshTokenEntity);
        await _context.SaveChangesAsync(cancellationToken);

        var nameParts = fullName.Split(' ', 2);
        var userDto = new UserDto(
            Id: userId,
            Email: email,
            FirstName: nameParts[0],
            LastName: nameParts.Length > 1 ? nameParts[1] : string.Empty,
            FullName: fullName,
            ProfilePictureUrl: null,
            Roles: roles);

        return new TokenResultDto(
            AccessToken: accessToken,
            RefreshToken: rawRefreshToken,
            ExpiresInMinutes: expiryMinutes,
            ExpiresUtc: expiresUtc,
            User: userDto);
    }

    public async Task<Result<TokenResultDto>> RotateRefreshTokenAsync(
        string rawRefreshToken,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawRefreshToken))
        {
            return Result<TokenResultDto>.Failure(Error.Validation("Auth.RefreshTokenRequired", "Refresh token is required."));
        }

        var incomingHash = HashToken(rawRefreshToken);
        var storedToken = await _context.RefreshTokens
            .FirstOrDefaultAsync(r => r.TokenHash == incomingHash, cancellationToken);

        if (storedToken == null)
        {
            return Result<TokenResultDto>.Failure(Error.Unauthorized("Auth.InvalidRefreshToken", "Invalid refresh token."));
        }

        if (storedToken.RevokedUtc != null)
        {
            var familyTokens = await _context.RefreshTokens
                .Where(r => r.FamilyId == storedToken.FamilyId && r.RevokedUtc == null)
                .ToListAsync(cancellationToken);

            foreach (var t in familyTokens)
            {
                t.RevokedUtc = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync(cancellationToken);
            return Result<TokenResultDto>.Failure(Error.Unauthorized("Auth.CompromisedToken", "Compromised refresh token reused. All active sessions in family revoked."));
        }

        if (storedToken.ExpiresUtc < DateTime.UtcNow)
        {
            storedToken.RevokedUtc = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
            return Result<TokenResultDto>.Failure(Error.Unauthorized("Auth.ExpiredRefreshToken", "Refresh token expired."));
        }

        var user = await _userManager.FindByIdAsync(storedToken.UserId.ToString());
        if (user == null || !user.IsActive)
        {
            return Result<TokenResultDto>.Failure(Error.Unauthorized("Auth.UserDeactivated", "User not found or account deactivated."));
        }

        var newRawRefreshToken = GenerateSecureTokenString();
        var newHash = HashToken(newRawRefreshToken);

        storedToken.RevokedUtc = DateTime.UtcNow;
        storedToken.ReplacedByTokenHash = newHash;

        var newRefreshTokenEntity = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = newHash,
            FamilyId = storedToken.FamilyId,
            ExpiresUtc = DateTime.UtcNow.AddDays(14),
            CreatedByIp = ipAddress,
            UserAgent = userAgent
        };

        _context.RefreshTokens.Add(newRefreshTokenEntity);
        await _context.SaveChangesAsync(cancellationToken);

        var roles = await _userManager.GetRolesAsync(user);

        var secretKey = _configuration["Jwt:SecretKey"] ?? "PragmaSuperSecretKeyForDevelopmentOnly1234567890!@#$";
        var issuer = _configuration["Jwt:Issuer"] ?? "PragmaAPI";
        var audience = _configuration["Jwt:Audience"] ?? "PragmaClient";

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(ClaimTypes.Name, user.FullName),
            new(JwtRegisteredClaimNames.Name, user.FullName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var expiryMinutes = GetPasswordlessExpiryMinutes();
        var expiresUtc = DateTime.UtcNow.AddMinutes(expiryMinutes);
        var jwt = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expiresUtc,
            signingCredentials: creds);

        var newAccessToken = new JwtSecurityTokenHandler().WriteToken(jwt);

        var userDto = new UserDto(
            Id: user.Id,
            Email: user.Email ?? string.Empty,
            FirstName: user.FirstName,
            LastName: user.LastName,
            FullName: user.FullName,
            ProfilePictureUrl: user.ProfilePictureUrl,
            Roles: roles);

        return Result<TokenResultDto>.Success(new TokenResultDto(
            AccessToken: newAccessToken,
            RefreshToken: newRawRefreshToken,
            ExpiresInMinutes: expiryMinutes,
            ExpiresUtc: expiresUtc,
            User: userDto));
    }

    public async Task<Result<bool>> RevokeRefreshTokenAsync(
        string rawRefreshToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawRefreshToken))
        {
            return Result<bool>.Success(true);
        }

        var tokenHash = HashToken(rawRefreshToken);
        var token = await _context.RefreshTokens
            .FirstOrDefaultAsync(r => r.TokenHash == tokenHash, cancellationToken);

        if (token != null && token.RevokedUtc == null)
        {
            token.RevokedUtc = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }

        return Result<bool>.Success(true);
    }

    public async Task<Result<bool>> RevokeAllUserTokensAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var tokens = await _context.RefreshTokens
            .Where(r => r.UserId == userId && r.RevokedUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var t in tokens)
        {
            t.RevokedUtc = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }

    private static string GenerateSecureTokenString()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    private static string HashToken(string rawToken)
    {
        var bytes = Encoding.UTF8.GetBytes(rawToken);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
