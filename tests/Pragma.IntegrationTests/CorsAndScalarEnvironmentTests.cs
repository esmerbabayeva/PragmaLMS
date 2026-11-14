using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Pragma.IntegrationTests;

public class ProductionWebApplicationFactory : CustomWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseEnvironment("Production");
    }
}

public class CorsAndScalarEnvironmentTests : IClassFixture<ProductionWebApplicationFactory>
{
    private readonly HttpClient _prodClient;

    public CorsAndScalarEnvironmentTests(ProductionWebApplicationFactory prodFactory)
    {
        _prodClient = prodFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task Scalar_InProduction_Returns404NotFound()
    {
        var response = await _prodClient.GetAsync("/scalar");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task OpenApi_InProduction_Returns404NotFound()
    {
        var response = await _prodClient.GetAsync("/openapi/v1.json");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Cors_InProduction_AllowsAllowedOriginFromAppSettings()
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/cohorts");
        request.Headers.Add("Origin", "http://localhost:5089");
        request.Headers.Add("Access-Control-Request-Method", "GET");

        var response = await _prodClient.SendAsync(request);

        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeTrue();
        response.Headers.GetValues("Access-Control-Allow-Origin").Should().Contain("http://localhost:5089");
    }

    [Fact]
    public async Task Cors_InProduction_RejectsUnknownOrigin()
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/cohorts");
        request.Headers.Add("Origin", "https://unauthorized-malicious-site.com");
        request.Headers.Add("Access-Control-Request-Method", "GET");

        var response = await _prodClient.SendAsync(request);

        // İcazə verilməyən origin üçün Access-Control-Allow-Origin başlığı qayıtmamalıdır
        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
    }
}
