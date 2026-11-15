using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Pragma.Application.DTOs.Certificates;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Pragma.IntegrationTests;

public class ApiIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ApiIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetHealth_ReturnsOkStatus()
    {
        var response = await _client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync($"/api/lessons/{Guid.NewGuid()}/notes");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task MalformedRequestBody_ReturnsBadRequestProblemDetails()
    {
        var content = new StringContent("{ invalid_json: ", System.Text.Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/api/auth/login", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var responseString = await response.Content.ReadAsStringAsync();
        responseString.Should().Contain("traceId");
    }

    [Fact]
    public async Task Request_IncludesCorrelationId_InHeaders()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("X-Correlation-Id", "test-corr-12345");

        var response = await _client.SendAsync(request);

        response.Headers.Should().ContainKey("X-Correlation-Id");
        response.Headers.GetValues("X-Correlation-Id").First().Should().Be("test-corr-12345");
    }

    [Fact]
    public async Task Response_IncludesSecurityHeaders()
    {
        var response = await _client.GetAsync("/health");

        response.Headers.Should().ContainKey("X-Content-Type-Options");
        response.Headers.GetValues("X-Content-Type-Options").First().Should().Be("nosniff");

        response.Headers.Should().ContainKey("X-Frame-Options");
        response.Headers.GetValues("X-Frame-Options").First().Should().Be("SAMEORIGIN");

        response.Headers.Should().ContainKey("Referrer-Policy");
    }

    [Fact]
    public async Task VerifyCertificate_WhenCodeDoesNotExist_ReturnsSuccessDtoWithFalseStatus()
    {
        var response = await _client.GetAsync("/api/certificates/verify/NON-EXISTENT-CODE");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<CertificateVerificationDto>();
        dto.Should().NotBeNull();
        dto!.IsValid.Should().BeFalse();
        dto.IsRevoked.Should().BeFalse();
        dto.Message.Should().Contain("tapılmadı");
    }

    [Fact]
    public async Task VerifyRoute_RedirectsToPortalVerifyPage()
    {
        var client = new CustomWebApplicationFactory().CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/verify/PRAGMA-DIP-2026-TEST");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().Be("/portal/verify.html?code=PRAGMA-DIP-2026-TEST");
    }
}
