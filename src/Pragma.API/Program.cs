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

        // İlkin Tələbə istifadəçisi
        const string studentEmail = "student@pragma.edu.az";
        var studentUser = await userManager.FindByEmailAsync(studentEmail);
        if (studentUser == null)
        {
            studentUser = new ApplicationUser
            {
                FirstName = "Əli",
                LastName = "Məmmədov",
                Email = studentEmail,
                UserName = studentEmail,
                EmailConfirmed = true
            };

            await userManager.CreateAsync(studentUser, "PragmaStudent2026!");
            await userManager.AddToRoleAsync(studentUser, UserRoles.Student);
        }

        // İlkin Təlimçi istifadəçisi
        const string instructorEmail = "instructor@pragma.edu.az";
        var instructorUser = await userManager.FindByEmailAsync(instructorEmail);
        if (instructorUser == null)
        {
            instructorUser = new ApplicationUser
            {
                FirstName = "Rəşad",
                LastName = "Əliyev",
                Email = instructorEmail,
                UserName = instructorEmail,
                EmailConfirmed = true
            };

            await userManager.CreateAsync(instructorUser, "PragmaInstructor2026!");
            await userManager.AddToRoleAsync(instructorUser, UserRoles.Instructor);
        }

        // İlkin Kateqoriyalar
        Category backendCat;
        if (!await context.Categories.AnyAsync())
        {
            backendCat = new Category { Name = "Backend Proqramlaşdırma", Slug = "backend-proqramlasdirma", Description = "C#, .NET 10, SQL Server və mikroservis arxitekturaları", IconName = "code-bracket", SortOrder = 1 };
            context.Categories.AddRange(
                backendCat,
                new Category { Name = "Frontend & UI/UX", Slug = "frontend-ui-ux", Description = "HTML5, Tailwind CSS, Alpine.js və müasir veb texnologiyaları", IconName = "paint-brush", SortOrder = 2 },
                new Category { Name = "Süni İntellekt & Data", Slug = "suni-intellekt-data", Description = "Google Gemini AI, LLM-lər və böyük məlumat analitikası", IconName = "sparkles", SortOrder = 3 }
            );
            await context.SaveChangesAsync();
        }
        else
        {
            backendCat = await context.Categories.FirstAsync();
        }

        // İlkin Nümunəvi Kurs və Dərslər
        if (!await context.Courses.AnyAsync())
        {
            var course = new Course
            {
                Title = "C# 13 və .NET 10 ilə Müasir Onion Architecture",
                Slug = "csharp-13-net-10-onion-architecture",
                ShortDescription = "Clean Architecture, DDD, CQRS prinsipləri və SQL Server ilə irimiqyaslı tədris idarəetmə sistemi (LMS) hazırlığı.",
                Description = "Bu kursda siz .NET 10 ekosisteminin ən qabaqcıl texnologiyaları ilə tanış olacaq, Onion Architecture, JWT təhlükəsizliyi, QuestPDF və ClosedXML hesabat modullarını sıfırdan quraşdıracaqsınız.",
                ThumbnailUrl = "https://images.unsplash.com/photo-1517694712202-14dd9538aa97?w=800&q=80",
                Price = 0m,
                IsFree = true,
                Level = CourseLevel.Beginner,
                Status = CourseStatus.Published,
                InstructorId = instructorUser.Id,
                CategoryId = backendCat.Id,
                ApprovedAtUtc = DateTime.UtcNow
            };

            var mod1 = new Module
            {
                Title = "Modul 1: Giriş və Arxitektura Əsasları",
                Description = "Onion və Clean Architecture prinsiplərinin müqayisəsi",
                OrderIndex = 1
            };

            var lesson1 = new Lesson
            {
                Title = "1. Onion Architecture qatları və asılılıqların idarəsi",
                OrderIndex = 1,
                Type = LessonType.Video,
                VideoUrl = "https://www.youtube.com/watch?v=yF_04-o32Ww",
                DurationSeconds = 600,
                IsPreview = true,
                Content = "Onion Architecture qatları: Domain, Application, Infrastructure və Presentation (API)."
            };

            var lesson2 = new Lesson
            {
                Title = "2. .NET 10 Yenilikləri və Dependency Injection qaydaları",
                OrderIndex = 2,
                Type = LessonType.Video,
                VideoUrl = "https://www.youtube.com/watch?v=2yJgwwDcgV8",
                DurationSeconds = 720,
                IsPreview = false,
                Content = "Dependency Injection ilə servislərin qeydiyyatı və zəncirvari zənglər."
            };

            mod1.Lessons.Add(lesson1);
            mod1.Lessons.Add(lesson2);

            var mod2 = new Module
            {
                Title = "Modul 2: Hesabat və Sənədləşmə Mühərrikləri",
                Description = "ClosedXML və QuestPDF ilə dinamik ixracat",
                OrderIndex = 2
            };

            var lesson3 = new Lesson
            {
                Title = "3. QuestPDF ilə A4 Sertifikat və ClosedXML ilə 3-Vərəqli Excel",
                OrderIndex = 1,
                Type = LessonType.Video,
                VideoUrl = "https://www.youtube.com/watch?v=k3Z3B_e9r4c",
                DurationSeconds = 900,
                IsPreview = false,
                Content = "Rəsmi sənəd şablonlarının tətbiqi və dinamik QR kod generasiyası."
            };

            mod2.Lessons.Add(lesson3);
            course.Modules.Add(mod1);
            course.Modules.Add(mod2);

            context.Courses.Add(course);
            await context.SaveChangesAsync();

            // Tələbəni bu kursa avtomatik qeydiyyatdan keçiririk
            var enrollment = new Enrollment
            {
                CourseId = course.Id,
                StudentId = studentUser.Id,
                EnrolledAtUtc = DateTime.UtcNow,
                ProgressPercentage = 33.3,
                IsCompleted = false
            };
            context.Enrollments.Add(enrollment);

            // İlkin Dərs Qeydi (Timestamp Note) nümunəsi
            context.LessonNotes.Add(new LessonNote
            {
                LessonId = lesson1.Id,
                StudentId = studentUser.Id,
                PositionSeconds = 45,
                NoteText = "Domain qatı heç bir xarici paketdən asılı olmamalıdır (POCO qaydası)."
            });

            // Tələbə üçün ilkin Streak
            context.UserStreaks.Add(new UserStreak
            {
                UserId = studentUser.Id,
                CurrentStreak = 1,
                LongestStreak = 1,
                LastActivityDateUtc = DateTime.UtcNow,
                TotalXp = 50
            });

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
