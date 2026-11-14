using System.Threading.RateLimiting;
using Asp.Versioning;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Pragma.API.Filters;
using Pragma.API.Middlewares;
using Pragma.Application;
using Pragma.Application.Common;
using Pragma.Domain.Entities;
using Pragma.Domain.Enums;
using Pragma.Infrastructure;
using Pragma.Infrastructure.Identity;
using Pragma.Infrastructure.Persistence;
using Scalar.AspNetCore;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/pragma-api-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog();

builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration, builder.Environment);

builder.Services.AddRouting(options => options.LowercaseUrls = true);

builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = ApiVersionReader.Combine(
        new UrlSegmentApiVersionReader(),
        new QueryStringApiVersionReader("api-version"),
        new HeaderApiVersionReader("X-Version"));
}).AddMvc().AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var problemDetails = new ValidationProblemDetails(context.ModelState)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Validation Error",
            Detail = "One or more validation errors occurred.",
            Instance = context.HttpContext.Request.Path
        };
        problemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
        return new BadRequestObjectResult(problemDetails);
    };
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = builder.Environment.IsEnvironment("Testing") ? 10_000 : 10,
                Window = TimeSpan.FromMinutes(1)
            }));
    options.AddPolicy("ai", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.User.Identity?.Name ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1)
            }));
    options.AddPolicy("verify", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(1)
            }));
});

builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>();

builder.Services.AddControllers(options =>
{
    options.Filters.Add<ValidationFilter>();
});

builder.Services.AddAuthorization(options =>
{
    // 1. Admin Policies
    options.AddPolicy(AuthorizationPolicies.RequireAdmin, policy =>
        policy.RequireRole(UserRoles.Admin));

    options.AddPolicy(AuthorizationPolicies.CanManageUsers, policy =>
        policy.RequireRole(UserRoles.Admin));

    options.AddPolicy(AuthorizationPolicies.CanManageRoles, policy =>
        policy.RequireRole(UserRoles.Admin));

    options.AddPolicy(AuthorizationPolicies.CanAccessAdminPanel, policy =>
        policy.RequireRole(UserRoles.Admin));

    options.AddPolicy(AuthorizationPolicies.CanAdjustGrades, policy =>
        policy.RequireRole(UserRoles.Admin));

    options.AddPolicy(AuthorizationPolicies.CanViewSystemStatistics, policy =>
        policy.RequireRole(UserRoles.Admin));

    options.AddPolicy(AuthorizationPolicies.CanViewAuditLogs, policy =>
        policy.RequireRole(UserRoles.Admin));

    // 2. Instructor & Content Management Policies
    options.AddPolicy(AuthorizationPolicies.CanManageCourses, policy =>
        policy.RequireRole(UserRoles.Admin, UserRoles.Instructor, UserRoles.Moderator));

    options.AddPolicy(AuthorizationPolicies.CanManageEnrollments, policy =>
        policy.RequireRole(UserRoles.Admin, UserRoles.Instructor, UserRoles.Moderator));

    options.AddPolicy(AuthorizationPolicies.CanManageCohorts, policy =>
        policy.RequireRole(UserRoles.Admin, UserRoles.Instructor, UserRoles.Moderator));

    options.AddPolicy(AuthorizationPolicies.CanManageQuiz, policy =>
        policy.RequireRole(UserRoles.Admin, UserRoles.Instructor, UserRoles.Teacher));

    options.AddPolicy(AuthorizationPolicies.CanViewGroups, policy =>
        policy.RequireRole(UserRoles.Admin, UserRoles.Moderator, UserRoles.Instructor, UserRoles.Teacher));

    options.AddPolicy(AuthorizationPolicies.CanGradeStudents, policy =>
        policy.RequireRole(UserRoles.Admin, UserRoles.Instructor, UserRoles.Teacher));

    options.AddPolicy(AuthorizationPolicies.CanViewResults, policy =>
        policy.RequireRole(UserRoles.Admin, UserRoles.Moderator, UserRoles.Instructor, UserRoles.Teacher));

    options.AddPolicy(AuthorizationPolicies.CanViewAssignedCourseContent, policy =>
        policy.RequireRole(UserRoles.Admin, UserRoles.Moderator, UserRoles.Instructor, UserRoles.Teacher, UserRoles.Student));

    // 3. Student Policies (Yalnız Student rolu üçündür)
    options.AddPolicy(AuthorizationPolicies.RequireStudent, policy =>
        policy.RequireRole(UserRoles.Student));

    options.AddPolicy(AuthorizationPolicies.CanViewOwnCourses, policy =>
        policy.RequireRole(UserRoles.Student));

    options.AddPolicy(AuthorizationPolicies.CanTrackProgress, policy =>
        policy.RequireRole(UserRoles.Student));

    options.AddPolicy(AuthorizationPolicies.CanTakeQuiz, policy =>
        policy.RequireRole(UserRoles.Student));

    options.AddPolicy(AuthorizationPolicies.CanViewOwnGrades, policy =>
        policy.RequireRole(UserRoles.Student));

    options.AddPolicy(AuthorizationPolicies.CanManageProfile, policy =>
        policy.RequireAuthenticatedUser());
});

var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        if (builder.Environment.IsDevelopment())
        {
            policy.AllowAnyOrigin()
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        }
        else
        {
            if (allowedOrigins.Length > 0)
            {
                policy.WithOrigins(allowedOrigins)
                      .AllowAnyHeader()
                      .AllowAnyMethod()
                      .AllowCredentials();
            }
            else
            {
                policy.SetIsOriginAllowed(_ => false);
            }
        }
    });
});

builder.Services.AddOpenApi();

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseRateLimiter();

app.MapHealthChecks("/health");

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.WithTitle("Pragma LMS API Documentation")
               .WithTheme(ScalarTheme.DeepSpace)
               .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);

        options.OperationTitleSource = OperationTitleSource.Path;
    });

    app.MapGet("/scalar", () => Results.Redirect("/scalar/v1"));
}

app.MapGet("/api/config", (IConfiguration configuration, IWebHostEnvironment environment) =>
{
    var adminUrl = configuration["AdminPanel:Url"] ?? "http://localhost:5089";
    var isDev = environment.IsDevelopment();
    return Results.Ok(new
    {
        adminPanelUrl = adminUrl,
        isDevelopment = isDev
    });
}).AllowAnonymous();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/", () => Results.Redirect("/portal/index.html"));
app.MapGet("/portal", () => Results.Redirect("/portal/index.html"));
app.MapGet("/app", () => Results.Redirect("/portal/index.html"));
app.MapGet("/login", () => Results.Redirect("/portal/login.html"));
app.MapGet("/auth/callback", () => Results.Redirect("/portal/callback.html"));

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCors("AllowAll");

app.Use(async (context, next) =>
{
    if (!context.Request.Headers.ContainsKey("Authorization"))
    {
        if (context.Request.Headers.TryGetValue("bearer", out var bearerHeader) ||
            context.Request.Headers.TryGetValue("token", out bearerHeader))
        {
            var val = bearerHeader.ToString().Trim();
            if (!val.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                val = "Bearer " + val;
            context.Request.Headers["Authorization"] = val;
        }
    }
    else
    {
        var auth = context.Request.Headers["Authorization"].ToString().Trim();
        if (!string.IsNullOrEmpty(auth) && !auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            context.Request.Headers["Authorization"] = "Bearer " + auth;
        }
    }

    await next();
});

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    var context = services.GetRequiredService<AppDbContext>();
    var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
    var roleManager = services.GetRequiredService<RoleManager<ApplicationRole>>();

    if (context.Database.IsInMemory())
    {
        await context.Database.EnsureCreatedAsync();
    }
    else if (context.Database.IsRelational())
    {
        await context.Database.MigrateAsync();
    }

    await PragmaDbSeeder.SeedAsync(context, userManager, roleManager, logger, app.Configuration, app.Environment);
}

app.Run();

public partial class Program { }
