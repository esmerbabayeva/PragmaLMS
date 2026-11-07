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
});

// Rahatlıq üçün ana səhifəni və /scalar ünvanını birbaşa /scalar/v1 səhifəsinə yönləndiririk
app.MapGet("/", () => Results.Redirect("/scalar/v1"));
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

// 7. İlkin Məlumatların (Roles, Admin və Kateqoriyalar) Bazaya Doldurulması (Data Seeder)
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<AppDbContext>();
        
        // Bazanın yaradılmasını təmin edirik (Code-First)
        await context.Database.EnsureCreatedAsync();

        var roleManager = services.GetRequiredService<RoleManager<ApplicationRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        string[] roles = [UserRoles.Admin, UserRoles.Instructor, UserRoles.Student];
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new ApplicationRole(role));
            }
        }

        // İlkin Admin istifadəçisi
        const string adminEmail = "admin@pragma.edu.az";
        var adminUser = await userManager.FindByEmailAsync(adminEmail);
        if (adminUser == null)
        {
            adminUser = new ApplicationUser
            {
                FirstName = "Pragma",
                LastName = "SuperAdmin",
                Email = adminEmail,
                UserName = adminEmail,
                EmailConfirmed = true
            };

            await userManager.CreateAsync(adminUser, "PragmaAdmin2026!");
            await userManager.AddToRoleAsync(adminUser, UserRoles.Admin);
        }

        // İlkin Kateqoriyalar
        if (!await context.Categories.AnyAsync())
        {
            context.Categories.AddRange(
                new Category { Name = "Backend Proqramlaşdırma", Slug = "backend-proqramlasdirma", Description = "C#, .NET 10, SQL Server və mikroservis arxitekturaları", IconName = "code-bracket", SortOrder = 1 },
                new Category { Name = "Frontend & UI/UX", Slug = "frontend-ui-ux", Description = "HTML5, Tailwind CSS, Alpine.js və müasir veb texnologiyaları", IconName = "paint-brush", SortOrder = 2 },
                new Category { Name = "Süni İntellekt & Data", Slug = "suni-intellekt-data", Description = "Google Gemini AI, LLM-lər və böyük məlumat analitikası", IconName = "sparkles", SortOrder = 3 }
            );
            await context.SaveChangesAsync();
        }
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Baza ilkin doldurularkən xəta baş verdi: {Message}", ex.Message);
    }
}

app.Run();
