using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Pragma.API.Middlewares;

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

        var (statusCode, title) = exception switch
        {
            KeyNotFoundException => (HttpStatusCode.NotFound, "Axtarılan resurs tapılmadı."),
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized, "Giriş icazəsi yoxdur."),
            ArgumentException => (HttpStatusCode.BadRequest, "Daxil edilən parametr yanlışdır."),
            DbUpdateConcurrencyException => (HttpStatusCode.Conflict, "Məlumat başqa bir istifadəçi tərəfindən yenilənib. Zəhmət olmasa səhifəni yeniləyib təkrar cəhd edin."),
            _ => (HttpStatusCode.InternalServerError, "Serverdə daxili xəta baş verdi.")
        };

        context.Response.StatusCode = (int)statusCode;
        var traceId = Activity.Current?.Id ?? context.TraceIdentifier;

        var problemDetails = new ProblemDetails
        {
            Status = (int)statusCode,
            Title = title,
            Type = $"https://httpstatuses.com/{(int)statusCode}",
            Detail = (_env.IsDevelopment() || _env.IsEnvironment("Testing")) ? $"{exception.GetType().Name}: {exception.Message}\n{exception.StackTrace}" : "Sistem xətası qeydə alındı. Zəhmət olmasa bir qədər sonra yenidən yoxlayın.",
            Instance = context.Request.Path
        };

        problemDetails.Extensions["traceId"] = traceId;

        var responseJson = JsonSerializer.Serialize(problemDetails);
        return context.Response.WriteAsync(responseJson);
    }
}
