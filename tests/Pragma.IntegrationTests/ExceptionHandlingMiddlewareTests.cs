using System.IO;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Pragma.API.Middlewares;
using Xunit;

namespace Pragma.IntegrationTests;

public class ExceptionHandlingMiddlewareTests
{
    private class FakeHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Pragma.API";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static (HttpContext Context, MemoryStream ResponseBody) CreateHttpContext()
    {
        var context = new DefaultHttpContext();
        var responseBody = new MemoryStream();
        context.Response.Body = responseBody;
        context.Request.Path = "/api/test-error";
        context.TraceIdentifier = "trace-test-123";
        return (context, responseBody);
    }

    [Fact]
    public async Task ExceptionHandlingMiddleware_DbUpdateConcurrencyException_Returns409Conflict()
    {
        // Arrange
        var (context, responseBody) = CreateHttpContext();
        var logger = NullLogger<ExceptionHandlingMiddleware>.Instance;
        var env = new FakeHostEnvironment { EnvironmentName = Environments.Development };

        RequestDelegate next = _ => throw new DbUpdateConcurrencyException("Concurrency conflict occurred");
        var middleware = new ExceptionHandlingMiddleware(next, logger, env);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        context.Response.ContentType.Should().Be("application/problem+json");

        responseBody.Seek(0, SeekOrigin.Begin);
        var problem = await JsonSerializer.DeserializeAsync<ProblemDetails>(responseBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(409);
        problem.Title.Should().Contain("Concurrency Conflict");
        problem.Detail.Should().Contain("DbUpdateConcurrencyException");
        problem.Extensions.Should().ContainKey("traceId");
    }

    [Fact]
    public async Task ExceptionHandlingMiddleware_DbUpdateException_Returns409Conflict()
    {
        // Arrange
        var (context, responseBody) = CreateHttpContext();
        var logger = NullLogger<ExceptionHandlingMiddleware>.Instance;
        var env = new FakeHostEnvironment { EnvironmentName = Environments.Development };

        RequestDelegate next = _ => throw new DbUpdateException("Duplicate key unique index violation");
        var middleware = new ExceptionHandlingMiddleware(next, logger, env);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        context.Response.ContentType.Should().Be("application/problem+json");

        responseBody.Seek(0, SeekOrigin.Begin);
        var problem = await JsonSerializer.DeserializeAsync<ProblemDetails>(responseBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(409);
        problem.Title.Should().Contain("Unikal indeks və ya məhdudiyyət pozuntusu");
        problem.Detail.Should().Contain("DbUpdateException");
    }

    [Fact]
    public async Task ExceptionHandlingMiddleware_InProduction_HidesInternalExceptionDetails()
    {
        // Arrange
        var (context, responseBody) = CreateHttpContext();
        var logger = NullLogger<ExceptionHandlingMiddleware>.Instance;
        var env = new FakeHostEnvironment { EnvironmentName = Environments.Production }; // Production mühiti

        RequestDelegate next = _ => throw new DbUpdateException("Internal SQL query failed at Line 42 with table schema dbo.SecretData");
        var middleware = new ExceptionHandlingMiddleware(next, logger, env);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status409Conflict);

        responseBody.Seek(0, SeekOrigin.Begin);
        var problem = await JsonSerializer.DeserializeAsync<ProblemDetails>(responseBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(409);

        // Production-da daxili stack trace və ya daxili sql mesajı görünməməlidir
        problem.Detail.Should().NotContain("Internal SQL query");
        problem.Detail.Should().NotContain("dbo.SecretData");
        problem.Detail.Should().NotContain("StackTrace");
        problem.Detail.Should().Be("Daxil edilən məlumat sistemdəki mövcud unikal qeydlərlə ziddiyyət təşkil edir.");
    }
}
