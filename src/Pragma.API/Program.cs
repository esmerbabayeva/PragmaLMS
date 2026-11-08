using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Pragma.API.Middlewares;
using Pragma.Application;
using Pragma.Domain.Entities;
using Pragma.Domain.Enums;
using Pragma.Infrastructure;
using Pragma.Infrastructure.Identity;
using Pragma.Infrastructure.Persistence;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// 1. Tətbiq və İnfrastruktur servisləri (Onion DI)
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

// 2. Qlobal Lowercase Routing konfiqurasiyası (RESTful standartı)
builder.Services.AddRouting(options => options.LowercaseUrls = true);

// 3. Controller və JSON konfiqurasiyası
builder.Services.AddControllers();

// 3. CORS Siyasəti (Frontend və Admin MVC üçün)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// 4. OpenAPI sənədləşməsi (Scalar dəstəyi ilə)
builder.Services.AddOpenApi();

var app = builder.Build();

// 5. Mərkəzləşdirilmiş Xəta İdarəsi (RFC 7807 ProblemDetails)
app.UseMiddleware<ExceptionHandlingMiddleware>();

// 6. OpenAPI və Scalar API Sənədləşməsi
app.MapOpenApi();
app.MapScalarApiReference(options =>
{
    options.WithTitle("Pragma LMS API Documentation")
           .WithTheme(ScalarTheme.DeepSpace)
           .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);

    // Sol menyuda (sidebar) başlıq olaraq cümlə əvəzinə dəqiq endpoint yolunu (path) göstərir
    options.OperationTitleSource = OperationTitleSource.Path;
});

// Statik fayllar (Student Web UI və Cinema Video Player üçün)
app.UseDefaultFiles();
app.UseStaticFiles();

// Rahatlıq üçün yönləndirmələr:
// Əsas ana səhifə (/) birbaşa Tələbə Portalı və Video Pleyeri açır
app.MapGet("/", () => Results.Redirect("/portal/index.html"));
app.MapGet("/portal", () => Results.Redirect("/portal/index.html"));
app.MapGet("/app", () => Results.Redirect("/portal/index.html"));
app.MapGet("/scalar", () => Results.Redirect("/scalar/v1"));

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCors("AllowAll");

// Scalar və HTTP testləri üçün token tolerantlığı:
// İstifadəçi 'bearer: <token>' və ya 'Authorization: <token>' (Bearer sözü olmadan) yazsa belə avtomatik düzəldilir
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

// 7. İlkin Məlumatların (Roles, İstifadəçilər, Kurslar, Quizlər və Sertifikatlar) Bazaya Doldurulması (Data Seeder)
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    var context = services.GetRequiredService<AppDbContext>();
    var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
    var roleManager = services.GetRequiredService<RoleManager<ApplicationRole>>();

    await PragmaDbSeeder.SeedAsync(context, userManager, roleManager, logger);
}

app.Run();
