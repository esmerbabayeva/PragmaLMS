using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace Pragma.API.Middlewares;

/// <summary>
/// Gözlənilməz sistem xətalarını mərkəzləşdirilmiş şəkildə tutan və 
/// RFC 7807 ProblemDetails formatında təqdim edən middleware.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "İcra olunmamış sistem xətası: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex);
        }
    }

    private Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

        var traceId = Activity.Current?.Id ?? context.TraceIdentifier;

        var problemDetails = new ProblemDetails
        {
            Status = (int)HttpStatusCode.InternalServerError,
            Title = "Serverdə daxili xəta baş verdi.",
            Type = "https://httpstatuses.com/500",
            Detail = _env.IsDevelopment() ? exception.ToString() : "Gözlənilməz sistem xətası yarandı. Zəhmət olmasa bir qədər sonra yenidən yoxlayın.",
            Instance = context.Request.Path
        };

        problemDetails.Extensions["traceId"] = traceId;

        var responseJson = JsonSerializer.Serialize(problemDetails);
        return context.Response.WriteAsync(responseJson);
    }
}
