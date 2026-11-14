using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Pragma.Application.Contracts;
using Pragma.Application.DTOs.Auth;
using Xunit;

namespace Pragma.IntegrationTests;

public class TestEmailSender : IEmailSender
{
    public static readonly List<(string To, string Subject, string Body)> SentEmails = new();

    public Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        lock (SentEmails)
        {
            SentEmails.Add((to, subject, htmlBody));
        }
        return Task.CompletedTask;
    }
}

public class TestGoogleTokenValidator : IGoogleTokenValidator
{
    public Task<GoogleIdentityDto?> ValidateAsync(string idToken, CancellationToken cancellationToken = default)
    {
        if (idToken == "valid-google-token")
        {
            return Task.FromResult<GoogleIdentityDto?>(new GoogleIdentityDto(
                Subject: "google-sub-12345",
                Email: "googleuser@pragma.az",
                EmailVerified: true,
                Name: "Google Student",
                PictureUrl: "https://lh3.googleusercontent.com/photo.jpg"));
        }

        if (idToken == "unverified-google-token")
        {
            return Task.FromResult<GoogleIdentityDto?>(new GoogleIdentityDto(
                Subject: "google-sub-unverified",
                Email: "unverified@pragma.az",
                EmailVerified: false,
                Name: "Unverified User",
                PictureUrl: null));
        }

        return Task.FromResult<GoogleIdentityDto?>(null);
    }
}

public class AuthIntegrationTestsFactory : CustomWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            services.AddSingleton<IEmailSender, TestEmailSender>();
            services.AddSingleton<IGoogleTokenValidator, TestGoogleTokenValidator>();
        });
    }
}

public class AuthIntegrationTests : IClassFixture<AuthIntegrationTestsFactory>
{
    private readonly HttpClient _client;

    public AuthIntegrationTests(AuthIntegrationTestsFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PasswordlessFlow_Request_Then_Verify_Then_GetMe()
    {
        var testEmail = $"student_{Guid.NewGuid():N}@pragma.az";

        var requestResponse = await _client.PostAsJsonAsync("/api/v1/auth/passwordless/request", new PasswordlessRequestDto(testEmail));
        var content = await requestResponse.Content.ReadAsStringAsync();
        requestResponse.StatusCode.Should().Be(HttpStatusCode.Accepted, content);

        string? code = null;
        lock (TestEmailSender.SentEmails)
        {
            var sent = TestEmailSender.SentEmails.LastOrDefault(e => e.To == testEmail);
            sent.Should().NotBeNull();
            var match = Regex.Match(sent.Body, @">\s*(\d{6})\s*<");
            match.Success.Should().BeTrue();
            code = match.Groups[1].Value;
        }

        code.Should().NotBeNullOrEmpty();

        var verifyResponse = await _client.PostAsJsonAsync("/api/v1/auth/passwordless/verify", new PasswordlessVerifyDto(testEmail, code!));
        var verifyContent = await verifyResponse.Content.ReadAsStringAsync();
        verifyResponse.StatusCode.Should().Be(HttpStatusCode.OK, verifyContent);

        var tokenResult = await verifyResponse.Content.ReadFromJsonAsync<TokenResultDto>();
        tokenResult.Should().NotBeNull();
        tokenResult!.AccessToken.Should().NotBeNullOrEmpty();
        tokenResult.RefreshToken.Should().NotBeNullOrEmpty();

        var meRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        meRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenResult.AccessToken);
        var meResponse = await _client.SendAsync(meRequest);

        meResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var currentUser = await meResponse.Content.ReadFromJsonAsync<CurrentUserDto>();
        currentUser.Should().NotBeNull();
        currentUser!.Email.Should().Be(testEmail);
        currentUser.Roles.Should().Contain("Student");
    }

    [Fact]
    public async Task PasswordlessFlow_InvalidCode_ReturnsBadRequest()
    {
        var testEmail = $"fail_{Guid.NewGuid():N}@pragma.az";
        await _client.PostAsJsonAsync("/api/v1/auth/passwordless/request", new PasswordlessRequestDto(testEmail));

        var verifyResponse = await _client.PostAsJsonAsync("/api/v1/auth/passwordless/verify", new PasswordlessVerifyDto(testEmail, "000000"));
        verifyResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GoogleLogin_ValidToken_ReturnsTokens_And_AutoRegistersStudent()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/google", new GoogleLoginRequestDto("valid-google-token"));
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var tokenResult = await response.Content.ReadFromJsonAsync<TokenResultDto>();
        tokenResult.Should().NotBeNull();
        tokenResult!.User.Email.Should().Be("googleuser@pragma.az");
        tokenResult.User.FullName.Should().Be("Google Student");
        tokenResult.User.Roles.Should().Contain("Student");
    }

    [Fact]
    public async Task GoogleLogin_UnverifiedEmail_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/google", new GoogleLoginRequestDto("unverified-google-token"));
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RefreshToken_Rotation_Works_Correctly()
    {
        var testEmail = $"refresh_{Guid.NewGuid():N}@pragma.az";
        await _client.PostAsJsonAsync("/api/v1/auth/passwordless/request", new PasswordlessRequestDto(testEmail));

        string code;
        lock (TestEmailSender.SentEmails)
        {
            var sent = TestEmailSender.SentEmails.Last(e => e.To == testEmail);
            code = Regex.Match(sent.Body, @">\s*(\d{6})\s*<").Groups[1].Value;
        }

        var verifyResponse = await _client.PostAsJsonAsync("/api/v1/auth/passwordless/verify", new PasswordlessVerifyDto(testEmail, code));
        verifyResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var initialTokens = await verifyResponse.Content.ReadFromJsonAsync<TokenResultDto>();

        var refreshResponse = await _client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshTokenRequestDto(initialTokens!.RefreshToken));
        refreshResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var rotatedTokens = await refreshResponse.Content.ReadFromJsonAsync<TokenResultDto>();
        rotatedTokens.Should().NotBeNull();
        rotatedTokens!.AccessToken.Should().NotBeNullOrEmpty();
        rotatedTokens.RefreshToken.Should().NotBe(initialTokens.RefreshToken);
    }

    [Fact]
    public async Task PasswordLogin_ReturnsTokens_And_ClaimsMatchRequirements()
    {
        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequestDto("admin@pragma.edu.az", "PragmaAdmin2026!"));
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var authResult = await loginResponse.Content.ReadFromJsonAsync<AuthResponseDto>();
        authResult.Should().NotBeNull();
        authResult!.AccessToken.Should().NotBeNullOrEmpty();
        authResult.RefreshToken.Should().NotBeNullOrEmpty();
        authResult.ExpiresInMinutes.Should().BeGreaterThanOrEqualTo(60);
        authResult.User.Email.Should().Be("admin@pragma.edu.az");
        authResult.User.Roles.Should().Contain("Admin");

        // Parse JWT and verify claims
        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(authResult.AccessToken);
        jwt.Claims.Should().Contain(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier || c.Type == System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
        jwt.Claims.Should().Contain(c => c.Type == System.Security.Claims.ClaimTypes.Email || c.Type == System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Email);
        jwt.Claims.Should().Contain(c => c.Type == System.Security.Claims.ClaimTypes.Role && c.Value == "Admin");
    }

    [Fact]
    public async Task PasswordLogin_RefreshToken_CanBeRotated_AndOldTokenCannotBeReused()
    {
        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequestDto("student@pragma.edu.az", "Password123!"));
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var authResult = await loginResponse.Content.ReadFromJsonAsync<AuthResponseDto>();
        authResult.Should().NotBeNull();
        var oldRefreshToken = authResult!.RefreshToken!;

        // 1. Rotate refresh token
        var refreshResponse = await _client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshTokenRequestDto(oldRefreshToken));
        refreshResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var rotatedTokens = await refreshResponse.Content.ReadFromJsonAsync<TokenResultDto>();
        rotatedTokens.Should().NotBeNull();
        rotatedTokens!.AccessToken.Should().NotBeNullOrEmpty();
        rotatedTokens.RefreshToken.Should().NotBe(oldRefreshToken);

        // 2. Reusing used refresh token should be rejected (Theft detection -> 401 Unauthorized)
        var reuseResponse = await _client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshTokenRequestDto(oldRefreshToken));
        reuseResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
