using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Pragma.API.Middlewares;
using Pragma.Application;
using Pragma.Domain.Enums;
using Pragma.Infrastructure;
using Pragma.Infrastructure.Identity;
using Pragma.Infrastructure.Persistence;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// 1. Tətbiq və İnfrastruktur servisləri (Onion DI)
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

// 2. Controller və JSON konfiqurasiyası
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

app.UseHttpsRedirection();

app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// 7. İlkin Məlumatların (Roles və Admin) Bazaya Doldurulması (Data Seeder)
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
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Baza ilkin doldurularkən xəta baş verdi: {Message}", ex.Message);
    }
}

app.Run();
