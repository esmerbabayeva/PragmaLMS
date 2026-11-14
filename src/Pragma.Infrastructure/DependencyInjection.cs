using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Pragma.Application.Common;
using Pragma.Application.Contracts;
using Pragma.Infrastructure.Identity;
using Pragma.Infrastructure.Persistence;
using Pragma.Infrastructure.Services;

namespace Pragma.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment? environment = null)
    {
        // 1. Verilənlər Bazası konfiqurasiyası
        if (configuration.GetValue<bool>("UseInMemoryDatabase"))
        {
            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase(configuration["InMemoryDbName"] ?? "PragmaLMS_TestDb"));
        }
        else
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection") 
                ?? "Server=(localdb)\\mssqllocaldb;Database=PragmaLMS_Db;Trusted_Connection=True;MultipleActiveResultSets=true";

            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlServer(connectionString, sqlOptions =>
                {
                    sqlOptions.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
                    sqlOptions.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorNumbersToAdd: null);
                }));
        }

        services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<AppDbContext>());

        // 2. ASP.NET Identity (Guid əsaslı)
        services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
        {
            options.Password.RequireDigit = false;
            options.Password.RequireLowercase = false;
            options.Password.RequireUppercase = false;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequiredLength = 6;
            options.User.RequireUniqueEmail = true;
        })
        .AddEntityFrameworkStores<AppDbContext>()
        .AddDefaultTokenProviders();

        // 3. JWT Autentifikasiyası
        var envName = environment?.EnvironmentName ?? "Production";
        var secretKey = JwtKeyValidator.ValidateAndGetKey(configuration["Jwt:SecretKey"], envName);
        var issuer = configuration["Jwt:Issuer"] ?? "PragmaAPI";
        var audience = configuration["Jwt:Audience"] ?? "PragmaClient";

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.RequireHttpsMetadata = false;
            options.SaveToken = true;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
                ValidateIssuer = true,
                ValidIssuer = issuer,
                ValidateAudience = true,
                ValidAudience = audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };
        });

        // 4. HybridCache (L1 Memory + Stampede Qoruması)
        #pragma warning disable EXTEXP0018
        services.AddHybridCache();
        #pragma warning restore EXTEXP0018

        // 5. İnfrastruktur servisləri
        services.AddHttpContextAccessor();
        services.AddScoped<IGoogleAuthService, GoogleAuthService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IUserLookupService, UserLookupService>();
        services.AddScoped<IExcelExporter, ClosedXMLExcelExporter>();
        services.AddScoped<ICertificateGenerator, QuestPdfCertificateGenerator>();
        services.AddScoped<IFileStorage, LocalFileStorage>();
        services.AddScoped<IOtpService, OtpService>();
        services.AddScoped<IGoogleTokenValidator, GoogleTokenValidator>();
        services.AddScoped<IEmailSender, SmtpEmailSender>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<IAdminService, AdminService>();

        var aiSettings = new AiSettings
        {
            GeminiApiKey = configuration["Gemini:ApiKey"]
        };
        services.AddSingleton(aiSettings);

        return services;
    }
}
