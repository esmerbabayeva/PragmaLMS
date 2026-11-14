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

        var (statusCode, title, defaultDetail) = exception switch
        {
            KeyNotFoundException => (HttpStatusCode.NotFound, "Axtarılan resurs tapılmadı.", "Müraciət olunan resurs sistemdə mövcud deyil."),
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized, "Giriş icazəsi yoxdur.", "Bu əməliyyatı icra etmək üçün icazəniz yoxdur."),
            ArgumentException => (HttpStatusCode.BadRequest, "Daxil edilən parametr yanlışdır.", "Daxil edilən parametr tələblərə uyğun deyil."),
            DbUpdateConcurrencyException => (HttpStatusCode.Conflict, "Məlumat ziddiyyəti (Concurrency Conflict)", "Məlumat başqa bir istifadəçi tərəfindən yenilənib. Zəhmət olmasa səhifəni yeniləyib təkrar cəhd edin."),
            DbUpdateException => (HttpStatusCode.Conflict, "Məlumat ziddiyyəti (Unikal indeks və ya məhdudiyyət pozuntusu)", "Daxil edilən məlumat sistemdəki mövcud unikal qeydlərlə ziddiyyət təşkil edir."),
            _ => (HttpStatusCode.InternalServerError, "Serverdə daxili xəta baş verdi.", "Sistem xətası qeydə alındı. Zəhmət olmasa bir qədər sonra yenidən yoxlayın.")
        };

        context.Response.StatusCode = (int)statusCode;
        var traceId = Activity.Current?.Id ?? context.TraceIdentifier;

        var isDev = _env.IsDevelopment();
        var detail = isDev
            ? $"{exception.GetType().Name}: {exception.Message}\n{exception.StackTrace}"
            : defaultDetail;

        var problemDetails = new ProblemDetails
        {
            Status = (int)statusCode,
            Title = title,
            Type = $"https://httpstatuses.com/{(int)statusCode}",
            Detail = detail,
            Instance = context.Request.Path
        };

        problemDetails.Extensions["traceId"] = traceId;

        var responseJson = JsonSerializer.Serialize(problemDetails);
        return context.Response.WriteAsync(responseJson);
    }
}
