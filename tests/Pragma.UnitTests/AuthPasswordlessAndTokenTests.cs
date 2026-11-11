using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Pragma.Domain.Entities;
using Pragma.Domain.Enums;
using Pragma.Infrastructure.Identity;
using Pragma.Infrastructure.Persistence;
using Pragma.Infrastructure.Services;
using Xunit;

namespace Pragma.UnitTests;

public class AuthPasswordlessAndTokenTests
{
    private readonly IConfiguration _config;

    public AuthPasswordlessAndTokenTests()
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            ["Jwt:SecretKey"] = "SuperSecretKeyForTestingPurposes1234567890!@#$",
            ["Jwt:Issuer"] = "PragmaTestIssuer",
            ["Jwt:Audience"] = "PragmaTestAudience",
            ["Otp:Pepper"] = "TestPepperValue123"
        };

        _config = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();
    }

    [Fact]
    public void OtpService_Generates_6Digit_Code()
    {
        var otpService = new OtpService(_config);

        var code = otpService.GenerateCode();

        Assert.NotNull(code);
        Assert.Equal(6, code.Length);
        Assert.True(int.TryParse(code, out _));
    }

    [Fact]
    public void OtpService_Hashes_And_Verifies_Correctly()
    {
        var otpService = new OtpService(_config);
        var code = "123456";

        var hash = otpService.HashCode(code);

        Assert.True(otpService.VerifyCode("123456", hash));
        Assert.False(otpService.VerifyCode("654321", hash));
        Assert.False(otpService.VerifyCode("", hash));
    }

    [Fact]
    public async Task TokenService_Generates_Tokens_And_Stores_Hashed_RefreshToken()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        await using var context = new AppDbContext(options);
        var mockUserManager = CreateMockUserManager();

        var tokenService = new TokenService(context, mockUserManager.Object, _config);

        var userId = Guid.NewGuid();
        var result = await tokenService.GenerateTokensAsync(
            userId: userId,
            email: "student@pragma.az",
            fullName: "Elmir Aliyev",
            roles: new[] { UserRoles.Student },
            ipAddress: "127.0.0.1",
            userAgent: "TestRunner");

        Assert.NotNull(result);
        Assert.NotEmpty(result.AccessToken);
        Assert.NotEmpty(result.RefreshToken);

        var stored = await context.RefreshTokens.FirstOrDefaultAsync(r => r.UserId == userId);
        Assert.NotNull(stored);
        Assert.NotEqual(result.RefreshToken, stored.TokenHash);
        Assert.Null(stored.RevokedUtc);
    }

    [Fact]
    public async Task TokenService_RotateRefreshToken_RevokesOld_And_IssuesNew()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        await using var context = new AppDbContext(options);

        var testUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "test@pragma.az",
            Email = "test@pragma.az",
            FirstName = "Test",
            LastName = "User",
            IsActive = true
        };

        var mockUserManager = CreateMockUserManager();
        mockUserManager.Setup(m => m.FindByIdAsync(testUser.Id.ToString()))
            .ReturnsAsync(testUser);
        mockUserManager.Setup(m => m.GetRolesAsync(testUser))
            .ReturnsAsync(new List<string> { UserRoles.Student });

        var tokenService = new TokenService(context, mockUserManager.Object, _config);

        var initialTokens = await tokenService.GenerateTokensAsync(
            testUser.Id,
            testUser.Email,
            testUser.FullName,
            new[] { UserRoles.Student },
            "127.0.0.1",
            "TestRunner");

        var rotateResult = await tokenService.RotateRefreshTokenAsync(
            initialTokens.RefreshToken,
            "127.0.0.1",
            "TestRunner");

        Assert.True(rotateResult.IsSuccess);
        Assert.NotEqual(initialTokens.RefreshToken, rotateResult.Value.RefreshToken);

        var tokens = await context.RefreshTokens.Where(r => r.UserId == testUser.Id).ToListAsync();
        Assert.Equal(2, tokens.Count);

        var oldToken = tokens.First(t => t.RevokedUtc != null);
        Assert.NotNull(oldToken.ReplacedByTokenHash);
    }

    [Fact]
    public async Task TokenService_Reusing_RevokedToken_Triggers_Theft_Detection_Revoking_Entire_Family()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        await using var context = new AppDbContext(options);

        var testUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "hacked@pragma.az",
            Email = "hacked@pragma.az",
            FirstName = "Hacked",
            LastName = "User",
            IsActive = true
        };

        var mockUserManager = CreateMockUserManager();
        mockUserManager.Setup(m => m.FindByIdAsync(testUser.Id.ToString()))
            .ReturnsAsync(testUser);
        mockUserManager.Setup(m => m.GetRolesAsync(testUser))
            .ReturnsAsync(new List<string> { UserRoles.Student });

        var tokenService = new TokenService(context, mockUserManager.Object, _config);

        var initialTokens = await tokenService.GenerateTokensAsync(
            testUser.Id,
            testUser.Email,
            testUser.FullName,
            new[] { UserRoles.Student },
            "127.0.0.1",
            "TestRunner");

        var rotateResult = await tokenService.RotateRefreshTokenAsync(
            initialTokens.RefreshToken,
            "127.0.0.1",
            "TestRunner");

        Assert.True(rotateResult.IsSuccess);

        var replayOldTokenResult = await tokenService.RotateRefreshTokenAsync(
            initialTokens.RefreshToken,
            "127.0.0.1",
            "Attacker");

        Assert.True(replayOldTokenResult.IsFailure);
        Assert.Contains("Compromised", replayOldTokenResult.Error.Message);

        var activeTokens = await context.RefreshTokens
            .Where(r => r.FamilyId == context.RefreshTokens.First().FamilyId && r.RevokedUtc == null)
            .ToListAsync();

        Assert.Empty(activeTokens);
    }

    private static Mock<UserManager<ApplicationUser>> CreateMockUserManager()
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        var options = new Mock<IOptions<IdentityOptions>>();
        var passwordHasher = new Mock<IPasswordHasher<ApplicationUser>>();
        var userValidators = new List<IUserValidator<ApplicationUser>>();
        var passwordValidators = new List<IPasswordValidator<ApplicationUser>>();
        var keyNormalizer = new Mock<ILookupNormalizer>();
        var errors = new Mock<IdentityErrorDescriber>();
        var services = new Mock<IServiceProvider>();
        var logger = new Mock<ILogger<UserManager<ApplicationUser>>>();

        return new Mock<UserManager<ApplicationUser>>(
            store.Object,
            options.Object,
            passwordHasher.Object,
            userValidators,
            passwordValidators,
            keyNormalizer.Object,
            errors.Object,
            services.Object,
            logger.Object);
    }
}
