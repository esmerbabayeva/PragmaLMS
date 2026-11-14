using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pragma.Application.Common;
using Pragma.Domain.Entities;
using Pragma.Domain.Enums;
using Pragma.Infrastructure.Identity;

namespace Pragma.Infrastructure.Persistence;

/// <summary>
/// Mərhələ 6: Pragma LMS üçün zəngin və realistik ilkin verilənlər bazası təminatçısı (Production-grade Data Seeder).
/// </summary>
public static class PragmaDbSeeder
{
    private static readonly SemaphoreSlim SeedLock = new(1, 1);

    public static async Task SeedAsync(
        AppDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        ILogger logger)
    {
        await SeedLock.WaitAsync();
        try
        {
            await context.Database.EnsureCreatedAsync();

            // Ensure AuditLogs table exists for existing relational databases
            if (context.Database.IsRelational())
            {
                await context.Database.ExecuteSqlRawAsync(@"
                    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AuditLogs')
                    BEGIN
                        CREATE TABLE [AuditLogs] (
                            [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
                            [UserId] UNIQUEIDENTIFIER NULL,
                            [UserEmail] NVARCHAR(256) NULL,
                            [Action] NVARCHAR(100) NOT NULL,
                            [EntityName] NVARCHAR(100) NOT NULL,
                            [EntityId] NVARCHAR(100) NOT NULL,
                            [Details] NVARCHAR(MAX) NOT NULL,
                            [IpAddress] NVARCHAR(64) NULL,
                            [CreatedAtUtc] DATETIME2 NOT NULL,
                            [UpdatedAtUtc] DATETIME2 NULL,
                            [IsDeleted] BIT NOT NULL DEFAULT 0,
                            [RowVersion] ROWVERSION NOT NULL
                        );
                        CREATE INDEX [IX_AuditLogs_CreatedAtUtc] ON [AuditLogs] ([CreatedAtUtc]);
                        CREATE INDEX [IX_AuditLogs_UserId] ON [AuditLogs] ([UserId]);
                    END
                ");
            }

            // 1. Rolların yaradılması
            string[] roles = [UserRoles.Admin, UserRoles.Instructor, UserRoles.Teacher, UserRoles.Student, UserRoles.Moderator];
            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new ApplicationRole(role));
                }
            }

            // 2. İstifadəçilərin yaradılması (Admin, Müəllimlər və Tələbələr)
            var adminUser = await EnsureUserAsync(userManager, "admin@pragma.edu.az", "PragmaAdmin2026!", "Pragma", "SuperAdmin", UserRoles.Admin);
            var teacherUser = await EnsureUserAsync(userManager, "teacher@pragma.edu.az", "Password123!", "Rəşad", "Əliyev", UserRoles.Teacher);
            if (await userManager.IsInRoleAsync(teacherUser, UserRoles.Instructor))
            {
                await userManager.RemoveFromRoleAsync(teacherUser, UserRoles.Instructor);
            }
            if (!await userManager.IsInRoleAsync(teacherUser, UserRoles.Teacher))
            {
                await userManager.AddToRoleAsync(teacherUser, UserRoles.Teacher);
            }

            var instructorUser = await EnsureUserAsync(userManager, "instructor@pragma.edu.az", "Password123!", "Kənan", "Məmmədov", UserRoles.Instructor);
            if (await userManager.IsInRoleAsync(instructorUser, UserRoles.Teacher))
            {
                await userManager.RemoveFromRoleAsync(instructorUser, UserRoles.Teacher);
            }
            if (!await userManager.IsInRoleAsync(instructorUser, UserRoles.Instructor))
            {
                await userManager.AddToRoleAsync(instructorUser, UserRoles.Instructor);
            }

            // Remove any streaks/badges attached to non-student accounts (Admin, Instructor, Teacher)
            var nonStudentUserIds = new[] { adminUser.Id, teacherUser.Id, instructorUser.Id };
            var nonStudentStreaks = await context.UserStreaks.Where(s => nonStudentUserIds.Contains(s.UserId)).ToListAsync();
            if (nonStudentStreaks.Count > 0)
            {
                context.UserStreaks.RemoveRange(nonStudentStreaks);
            }
            var nonStudentBadges = await context.UserBadges.Where(b => nonStudentUserIds.Contains(b.UserId)).ToListAsync();
            if (nonStudentBadges.Count > 0)
            {
                context.UserBadges.RemoveRange(nonStudentBadges);
            }
            
            var studentEsmer = await EnsureUserAsync(userManager, "student@pragma.edu.az", "Password123!", "Əsmər", "Babayeva", UserRoles.Student);
            var studentLeyla = await EnsureUserAsync(userManager, "leyla@pragma.edu.az", "Password123!", "Leyla", "Qasımova", UserRoles.Student);
            var studentAli = await EnsureUserAsync(userManager, "ali@pragma.edu.az", "Password123!", "Əli", "Məmmədov", UserRoles.Student);

            // 3. Kateqoriyaların yaradılması
            var backendCat = await EnsureCategoryAsync(context, "Backend Proqramlaşdırma", "backend-proqramlasdirma", "C#, .NET 10, SQL Server və mikroservis arxitekturaları", "code-bracket", 1);
            var frontendCat = await EnsureCategoryAsync(context, "Frontend & UI/UX", "frontend-ui-ux", "HTML5, Tailwind CSS, Alpine.js və müasir veb texnologiyaları", "paint-brush", 2);
            var aiCat = await EnsureCategoryAsync(context, "Süni İntellekt & Data", "suni-intellekt-data", "Google Gemini AI, LLM-lər və böyük məlumat analitikası", "sparkles", 3);
            var devopsCat = await EnsureCategoryAsync(context, "Kiber Təhlükəsizlik & DevOps", "kiber-tehlukesizlik-devops", "Docker, Kubernetes, CI/CD və təhlükəsizlik qaydaları", "shield-check", 4);

            await context.SaveChangesAsync();

            // 4. Kursların, Modulların, Dərslərin və Quizlərin yaradılması
            var course1 = await EnsureCourseAsync(
                context,
                title: "C# 14 və .NET 10 ilə Müasir Onion Architecture",
                slug: "csharp-14-net-10-onion-architecture",
                shortDesc: "Clean Architecture, DDD, CQRS prinsipləri və SQL Server ilə irimiqyaslı tədris idarəetmə sistemi (LMS) hazırlığı.",
                desc: "Bu kursda siz .NET 10 ekosisteminin ən qabaqcıl texnologiyaları ilə tanış olacaq, Onion Architecture, JWT təhlükəsizliyi, QuestPDF və ClosedXML hesabat modullarını sıfırdan quraşdıracaqsınız.",
                thumbUrl: "https://images.unsplash.com/photo-1517694712202-14dd9538aa97?w=800&q=80",
                price: 0m,
                isFree: true,
                level: CourseLevel.Beginner,
                instructorId: instructorUser.Id,
                categoryId: backendCat.Id
            );

            var course2 = await EnsureCourseAsync(
                context,
                title: "Müasir Frontend: Alpine.js, Tailwind CSS & Reaktiv UI",
                slug: "modern-frontend-alpine-tailwind",
                shortDesc: "Müasir SPA dizaynı, komponent arxitekturası, reaktiv vəziyyət idarəsi və YouTube API inteqrasiyası.",
                desc: "Heavyweight SPA framework-lərinə alternativ olaraq minimalist, lakin güclü Alpine.js 3 və Tailwind CSS dizayn sistemi ilə Dark/Light rejimli interfeyslərin qurulması.",
                thumbUrl: "https://images.unsplash.com/photo-1507238691740-187a5b1d37b8?w=800&q=80",
                price: 0m,
                isFree: true,
                level: CourseLevel.Intermediate,
                instructorId: teacherUser.Id,
                categoryId: frontendCat.Id
            );

            var course3 = await EnsureCourseAsync(
                context,
                title: "Generative AI & LLM İnteqrasiyası: Google Gemini API",
                slug: "generative-ai-google-gemini-integration",
                shortDesc: "Google Gemini API, Sokratik Tədris metodları, Prompt Mühəndisliyi və LMS daxilində RAG sisteminin qurulması.",
                desc: "Süni intellekt köməkçisi və Sokratik bələdçi vasitəsilə tələbələrin suallarını dərhal və pedaqoji prinsiplərə uyğun cavablandıran ağıllı mentor modulu.",
                thumbUrl: "https://images.unsplash.com/photo-1618005182384-a83a8bd57fbe?w=800&q=80",
                price: 49.99m,
                isFree: false,
                level: CourseLevel.Advanced,
                instructorId: instructorUser.Id,
                categoryId: aiCat.Id
            );

            await context.SaveChangesAsync();

            // 5. Kurs 1 üçün Modullar, Dərslər və Quiz
            Lesson? quizLesson = null;
            if (!await context.Modules.AnyAsync(m => m.CourseId == course1.Id))
            {
                var mod1 = new Module
                {
                    CourseId = course1.Id,
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
                    CourseId = course1.Id,
                    Title = "Modul 2: Hesabat və İmtahan Modulu",
                    Description = "QuestPDF və ClosedXML hesabatları, eləcə də arxitektura testi",
                    OrderIndex = 2
                };

                var lesson3 = new Lesson
                {
                    Title = "3. QuestPDF ilə A4 Sertifikat və ClosedXML ilə Excel",
                    OrderIndex = 1,
                    Type = LessonType.Video,
                    VideoUrl = "https://www.youtube.com/watch?v=k3Z3B_e9r4c",
                    DurationSeconds = 900,
                    IsPreview = false,
                    Content = "Rəsmi sənəd şablonlarının tətbiqi və dinamik QR kod generasiyası."
                };

                quizLesson = new Lesson
                {
                    Title = "4. Bilik Yoxlama Sınağı: Onion Architecture & Clean Code",
                    OrderIndex = 2,
                    Type = LessonType.Markdown,
                    DurationSeconds = 900,
                    IsPreview = false,
                    Content = "Kurs üzrə əldə etdiyiniz bilikləri yoxlamaq üçün yekun arxitektura testi."
                };

                mod2.Lessons.Add(lesson3);
                mod2.Lessons.Add(quizLesson);

                context.Modules.AddRange(mod1, mod2);
                await context.SaveChangesAsync();

                // Quiz və Sualların doldurulması
                var quiz = new Quiz
                {
                    LessonId = quizLesson.Id,
                    Title = "Onion Architecture & Clean Code Yekun Sınağı",
                    Description = "Arxitektura, Dependency Injection və C# prinsipləri üzrə bilik testi.",
                    PassingScorePercentage = 70,
                    TimeLimitMinutes = 15,
                    MaxAttempts = 3
                };

                var q1 = new Question
                {
                    Text = "Onion Architecture-də Domain qatı hansı asılılığa malik olmalıdır?",
                    Points = 25,
                    OrderIndex = 1,
                    Explanation = "Təmiz arxitektura qaydasına görə Domain ən daxili dairədir və kənar heç bir framework və ya layihədən asılı olmamalıdır (Zero External Dependencies)."
                };
                q1.Options.Add(new AnswerOption { Text = "Heç bir xarici layihədən və ya framework-dən asılı olmamalıdır (Zero Dependencies).", IsCorrect = true });
                q1.Options.Add(new AnswerOption { Text = "Birbaşa Entity Framework və SQL Server-dən asılı olmalıdır.", IsCorrect = false });
                q1.Options.Add(new AnswerOption { Text = "Presentation qatına birbaşa asılılıq yaratmalıdır.", IsCorrect = false });
                q1.Options.Add(new AnswerOption { Text = "Yalnız ASP.NET Core Web API paketlərinə asılı olmalıdır.", IsCorrect = false });

                var q2 = new Question
                {
                    Text = "Dependency Injection konteynerində AddScoped yaşam dövrü necə işləyir?",
                    Points = 25,
                    OrderIndex = 2,
                    Explanation = "Scoped servislər cari HTTP request boyunca tək nüsxədə (instance) saxlanılır və request bitdikdə təmizlənir."
                };
                q2.Options.Add(new AnswerOption { Text = "Hər bir HTTP sorğusu üçün bir dəfə yaradılır və sorğu boyu eyni nüsxə istifadə olunur.", IsCorrect = true });
                q2.Options.Add(new AnswerOption { Text = "Hər dəfə servis istənildikdə yeni bir nüsxə yaradılır.", IsCorrect = false });
                q2.Options.Add(new AnswerOption { Text = "Tətbiq işə düşəndə bircə dəfə yaradılır və bütün istifadəçilər üçün ortaq qalır.", IsCorrect = false });
                q2.Options.Add(new AnswerOption { Text = "Yalnız statik metodlar tərəfindən çağırıla bilər.", IsCorrect = false });

                var q3 = new Question
                {
                    Text = "Entity Framework Core-da AsNoTracking() metodunun əsas məqsədi nədir?",
                    Points = 25,
                    OrderIndex = 3,
                    Explanation = "AsNoTracking() dəyişikliklərin izlənməsini (Change Tracking) söndürür, bu da yalnız oxuma məqsədli sorğularda yaddaş və CPU qənaəti təmin edir."
                };
                q3.Options.Add(new AnswerOption { Text = "Yalnız oxuma (Read-Only) sorğularında Change Tracker yükünü aradan qaldıraraq sürətə qənaət etmək.", IsCorrect = true });
                q3.Options.Add(new AnswerOption { Text = "Məlumatları verilənlər bazasından avtomatik silmək.", IsCorrect = false });
                q3.Options.Add(new AnswerOption { Text = "Bütün cədvəli tranzaksiya daxilində bloklamaq.", IsCorrect = false });
                q3.Options.Add(new AnswerOption { Text = "Yalnız yazma (Insert/Update) əməliyyatlarını sürətləndirmək.", IsCorrect = false });

                var q4 = new Question
                {
                    Text = "HybridCache kitabxanasının ən böyük üstünlüyü nədir?",
                    Points = 25,
                    OrderIndex = 4,
                    Explanation = "HybridCache iki səviyyəli keş (L1 Memory + L2 Distributed) və Cache Stampede probleminə qarşı avtomatik daxili kilidləmə mexanizminə malikdir."
                };
                q4.Options.Add(new AnswerOption { Text = "İki səviyyəli keş (L1 Memory + L2 Distributed) və Cache Stampede probleminə qarşı daxili kilidləmə təmin etməsi.", IsCorrect = true });
                q4.Options.Add(new AnswerOption { Text = "Yalnız lokal fayl sistemində keş saxlaması.", IsCorrect = false });
                q4.Options.Add(new AnswerOption { Text = "Bütün verilənlər bazasını tək bir keş faylına çevirməsi.", IsCorrect = false });
                q4.Options.Add(new AnswerOption { Text = "Heç bir asılılıq inyeksiyası tələb etməməsi.", IsCorrect = false });

                quiz.Questions.Add(q1);
                quiz.Questions.Add(q2);
                quiz.Questions.Add(q3);
                quiz.Questions.Add(q4);

                context.Quizzes.Add(quiz);
                await context.SaveChangesAsync();

                // Əsmər Babayevanın bu kviz üzrə 100%-lik cəhdinin qeydə alınması
                var attempt = new QuizAttempt
                {
                    StudentId = studentEsmer.Id,
                    QuizId = quiz.Id,
                    AttemptNumber = 1,
                    TotalQuestions = 4,
                    CorrectAnswers = 4,
                    EarnedPoints = 100,
                    TotalPoints = 100,
                    ScorePercentage = 100.0,
                    IsPassed = true,
                    Status = QuizAttemptStatus.Completed,
                    StartedAtUtc = DateTime.UtcNow.AddDays(-1),
                    CompletedAtUtc = DateTime.UtcNow.AddDays(-1).AddMinutes(12)
                };
                context.QuizAttempts.Add(attempt);
                await context.SaveChangesAsync();
            }

            // 6. Kurs 2 üçün Modul və Dərslər
            if (!await context.Modules.AnyAsync(m => m.CourseId == course2.Id))
            {
                var mod = new Module
                {
                    CourseId = course2.Id,
                    Title = "Modul 1: Alpine.js Reaktivliyi və Tailwind CSS",
                    Description = "Deklarativ DOM manipulyasiyası və dizayn sistemi",
                    OrderIndex = 1
                };

                mod.Lessons.Add(new Lesson
                {
                    Title = "1. Alpine.js 3 ilə deklarativ DOM manipulyasiyası",
                    OrderIndex = 1,
                    Type = LessonType.Video,
                    VideoUrl = "https://www.youtube.com/watch?v=rpe_4oKq6nI",
                    DurationSeconds = 750,
                    IsPreview = true,
                    Content = "x-data, x-init, x-show və x-model direktivləri ilə tək səhifəlik reaktiv təcrübə."
                });

                mod.Lessons.Add(new Lesson
                {
                    Title = "2. Tailwind CSS v3 ilə Dark & Light Mode Adaptasiyası",
                    OrderIndex = 2,
                    Type = LessonType.Video,
                    VideoUrl = "https://www.youtube.com/watch?v=oGq9o2BxL2I",
                    DurationSeconds = 800,
                    IsPreview = false,
                    Content = "Tailwind darkMode: 'class' konfiqurasiyası və FOUC qarşısının alınması."
                });

                context.Modules.Add(mod);
                await context.SaveChangesAsync();
            }

            // 7. Kurs 3 üçün Modul və Dərslər
            if (!await context.Modules.AnyAsync(m => m.CourseId == course3.Id))
            {
                var mod = new Module
                {
                    CourseId = course3.Id,
                    Title = "Modul 1: Google Gemini API və Sokratik Tədris",
                    Description = "Süni intellekt köməkçisi və RAG arxitekturası",
                    OrderIndex = 1
                };

                mod.Lessons.Add(new Lesson
                {
                    Title = "1. Gemini API ilə dialoq mühərriki və RAG arxitekturası",
                    OrderIndex = 1,
                    Type = LessonType.Video,
                    VideoUrl = "https://www.youtube.com/watch?v=2K37T7Wz0V8",
                    DurationSeconds = 850,
                    IsPreview = true,
                    Content = "Google Gemini API vasitəsilə dərslik kontekstinin LLM-ə ötürülməsi."
                });

                mod.Lessons.Add(new Lesson
                {
                    Title = "2. Sokratik Mentor və Tələbə Düşünmə Alqoritmləri",
                    OrderIndex = 2,
                    Type = LessonType.Video,
                    VideoUrl = "https://www.youtube.com/watch?v=30ph85Wc48o",
                    DurationSeconds = 920,
                    IsPreview = false,
                    Content = "Birbaşa cavab vermək əvəzinə tələbəyə istiqamətləndirici ipucu vermək sənəti."
                });

                context.Modules.Add(mod);
                await context.SaveChangesAsync();
            }

            // 8. Tələbələrin Qeydiyyatları (Enrollments)
            await EnsureEnrollmentAsync(context, course1.Id, studentEsmer.Id, 100.0, true);
            await EnsureEnrollmentAsync(context, course2.Id, studentEsmer.Id, 45.0, false);
            await EnsureEnrollmentAsync(context, course1.Id, studentLeyla.Id, 85.0, false);
            await EnsureEnrollmentAsync(context, course2.Id, studentLeyla.Id, 100.0, true);
            await EnsureEnrollmentAsync(context, course1.Id, studentAli.Id, 40.0, false);

            // 9. Dərs Qeydləri (Smart Timestamp Notes)
            var firstLesson = await context.Lessons.FirstOrDefaultAsync(l => l.Module!.CourseId == course1.Id);
            if (firstLesson != null)
            {
                if (!await context.LessonNotes.AnyAsync(n => n.StudentId == studentEsmer.Id && n.LessonId == firstLesson.Id))
                {
                    context.LessonNotes.AddRange(
                        new LessonNote
                        {
                            LessonId = firstLesson.Id,
                            StudentId = studentEsmer.Id,
                            PositionSeconds = 45,
                            NoteText = "Domain qatı heç bir xarici paketdən asılı olmamalıdır (POCO qaydası)."
                        },
                        new LessonNote
                        {
                            LessonId = firstLesson.Id,
                            StudentId = studentEsmer.Id,
                            PositionSeconds = 125,
                            NoteText = "Dependency Inversion prinsipinə görə yüksək səviyyəli modullar aşağı səviyyəli modullardan asılı olmamalıdır."
                        }
                    );
                }
            }

            // 10. Qamifikasiya: Streaks və XP
            await EnsureUserStreakAsync(context, studentLeyla.Id, streak: 7, longest: 7, xp: 210);
            await EnsureUserStreakAsync(context, studentEsmer.Id, streak: 5, longest: 5, xp: 140);
            await EnsureUserStreakAsync(context, studentAli.Id, streak: 3, longest: 3, xp: 75);

            // 11. Qamifikasiya: Nailiyyət Nişanları (Badges)
            await EnsureUserBadgeAsync(context, studentEsmer.Id, "FIRST_LESSON", "İlk Addım", "İlk dərsi uğurla tamamladınız", "rocket-launch");
            await EnsureUserBadgeAsync(context, studentEsmer.Id, "QUIZ_MASTER", "Quiz Ustası", "Arxitektura sınağını 100% nəticə ilə keçdiniz", "trophy");
            await EnsureUserBadgeAsync(context, studentEsmer.Id, "GRADUATE", "Rəsmi Məzun", "Kursu 100% bitirib təsdiqli diplom qazandınız", "academic-cap");

            await EnsureUserBadgeAsync(context, studentLeyla.Id, "FIRST_LESSON", "İlk Addım", "İlk dərsi uğurla tamamladınız", "rocket-launch");
            await EnsureUserBadgeAsync(context, studentLeyla.Id, "STREAK_7", "7 Gün Fasiləsiz", "7 gün ardıcıl olaraq dərs oxudunuz", "fire");

            await EnsureUserBadgeAsync(context, studentAli.Id, "FIRST_LESSON", "İlk Addım", "İlk dərsi uğurla tamamladınız", "rocket-launch");

            // 12. Rəsmi Sertifikat (Əsmər Babayeva üçün - 100% bitirdiyi üçün)
            const string certCode = "PRAGMA-2026-CSHR-7X9B1";
            if (!await context.Certificates.AnyAsync(c => c.CertificateCode == certCode))
            {
                context.Certificates.Add(new Certificate
                {
                    StudentId = studentEsmer.Id,
                    CourseId = course1.Id,
                    Type = Domain.Enums.CertificateType.Diploma,
                    CertificateCode = certCode,
                    VerificationCode = "c9a4498e6490439b8dc2edaaf6d5115d8ac43586",
                    StudentFullName = "Əsmər Babayeva",
                    CourseTitle = course1.Title,
                    GradePercentage = 100.0,
                    IssuedAtUtc = DateTime.UtcNow.AddDays(-1)
                });
            }

            // 13. Kohortların və Dərs Cədvəllərinin Seeding-i (Stage 2)
            await SeedCohortsAsync(context, teacherUser, studentEsmer, studentLeyla, course2, course1);

            // 14. Çoxdilli tərcümələrin doldurulması (Kateqoriyalar və Kurslar üçün AZ, EN, RU)
            await SeedMultilingualTranslationsAsync(context);

            await context.SaveChangesAsync();
            logger.LogInformation("PragmaDbSeeder: Verilənlər bazası bütün zəngin məlumatlarla (3 kurs, 5 istifadəçi, quiz, streak, sertifikat) uğurla təmin edildi.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "PragmaDbSeeder: Məlumatlar doldurularkən xəta baş verdi: {Message}", ex.Message);
        }
        finally
        {
            SeedLock.Release();
        }
    }

    private static async Task<ApplicationUser> EnsureUserAsync(
        UserManager<ApplicationUser> userManager,
        string email,
        string password,
        string firstName,
        string lastName,
        string role)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user == null)
        {
            user = new ApplicationUser
            {
                FirstName = firstName,
                LastName = lastName,
                Email = email,
                UserName = email,
                EmailConfirmed = true
            };

            await userManager.CreateAsync(user, password);
        }
        else
        {
            if (user.FirstName != firstName || user.LastName != lastName)
            {
                user.FirstName = firstName;
                user.LastName = lastName;
                await userManager.UpdateAsync(user);
            }

            if (!await userManager.CheckPasswordAsync(user, password))
            {
                var resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
                await userManager.ResetPasswordAsync(user, resetToken, password);
            }
        }

        if (!await userManager.IsInRoleAsync(user, role))
        {
            await userManager.AddToRoleAsync(user, role);
        }
        return user;
    }

    private static async Task<Category> EnsureCategoryAsync(
        AppDbContext context,
        string name,
        string slug,
        string description,
        string iconName,
        int sortOrder)
    {
        var category = await context.Categories.FirstOrDefaultAsync(c => c.Slug == slug);
        if (category == null)
        {
            category = new Category
            {
                Name = name,
                Slug = slug,
                Description = description,
                IconName = iconName,
                SortOrder = sortOrder
            };
            context.Categories.Add(category);
        }
        return category;
    }

    private static async Task<Course> EnsureCourseAsync(
        AppDbContext context,
        string title,
        string slug,
        string shortDesc,
        string desc,
        string thumbUrl,
        decimal price,
        bool isFree,
        CourseLevel level,
        Guid instructorId,
        Guid categoryId)
    {
        var course = await context.Courses.FirstOrDefaultAsync(c => c.Slug == slug);
        if (course == null)
        {
            course = new Course
            {
                Title = title,
                Slug = slug,
                ShortDescription = shortDesc,
                Description = desc,
                ThumbnailUrl = thumbUrl,
                Price = price,
                IsFree = isFree,
                Level = level,
                Status = CourseStatus.Published,
                InstructorId = instructorId,
                CategoryId = categoryId,
                ApprovedAtUtc = DateTime.UtcNow
            };
            context.Courses.Add(course);
        }
        else
        {
            if (course.InstructorId != instructorId)
            {
                course.InstructorId = instructorId;
            }
        }
        return course;
    }

    private static async Task EnsureEnrollmentAsync(
        AppDbContext context,
        Guid courseId,
        Guid studentId,
        double progressPercentage,
        bool isCompleted)
    {
        var enrollment = await context.Enrollments.FirstOrDefaultAsync(e => e.CourseId == courseId && e.StudentId == studentId);
        if (enrollment == null)
        {
            context.Enrollments.Add(new Enrollment
            {
                CourseId = courseId,
                StudentId = studentId,
                ProgressPercentage = progressPercentage,
                IsCompleted = isCompleted,
                EnrolledAtUtc = DateTime.UtcNow.AddDays(-3)
            });
        }
        else
        {
            enrollment.ProgressPercentage = progressPercentage;
            enrollment.IsCompleted = isCompleted;
        }
    }

    private static async Task EnsureUserStreakAsync(
        AppDbContext context,
        Guid userId,
        int streak,
        int longest,
        int xp)
    {
        var userStreak = await context.UserStreaks.FirstOrDefaultAsync(s => s.UserId == userId);
        if (userStreak == null)
        {
            context.UserStreaks.Add(new UserStreak
            {
                UserId = userId,
                CurrentStreak = streak,
                LongestStreak = longest,
                TotalXp = xp,
                LastActivityDateUtc = DateTime.UtcNow
            });
        }
        else
        {
            userStreak.CurrentStreak = streak;
            userStreak.LongestStreak = longest;
            userStreak.TotalXp = xp;
            userStreak.LastActivityDateUtc = DateTime.UtcNow;
        }
    }

    private static async Task EnsureUserBadgeAsync(
        AppDbContext context,
        Guid userId,
        string code,
        string name,
        string description,
        string icon)
    {
        var exists = await context.UserBadges.AnyAsync(b => b.UserId == userId && b.BadgeCode == code);
        if (!exists)
        {
            context.UserBadges.Add(new UserBadge
            {
                UserId = userId,
                BadgeCode = code,
                BadgeName = name,
                Description = description,
                IconName = icon,
                AwardedAtUtc = DateTime.UtcNow.AddDays(-1)
            });
        }
    }

    private static async Task SeedMultilingualTranslationsAsync(AppDbContext context)
    {
        // 1. Kategoriyalar üzrə tərcümələr
        var allCategories = await context.Categories.ToListAsync();
        foreach (var cat in allCategories)
        {
            // Mövcud məlumatı az kimi miqrasiya et
            await EnsureCategoryTranslationAsync(context, cat.Id, "az", cat.Name, cat.Description);

            if (cat.Slug.Equals("frontend-ui-ux", StringComparison.OrdinalIgnoreCase))
            {
                await EnsureCategoryTranslationAsync(context, cat.Id, "en", "Frontend & UI/UX", "HTML5, Tailwind CSS, Alpine.js and modern web technologies");
                await EnsureCategoryTranslationAsync(context, cat.Id, "ru", "Фронтенд и UI/UX", "HTML5, Tailwind CSS, Alpine.js и современные веб-технологии");
            }
            else if (cat.Slug.Equals("backend-proqramlasdirma", StringComparison.OrdinalIgnoreCase))
            {
                await EnsureCategoryTranslationAsync(context, cat.Id, "en", "Backend Development", "C#, .NET 10, SQL Server and microservices architecture");
                await EnsureCategoryTranslationAsync(context, cat.Id, "ru", "Бэкенд разработка", "C#, .NET 10, SQL Server и микросервисная архитектура");
            }
            else if (cat.Slug.Equals("suni-intellekt-data", StringComparison.OrdinalIgnoreCase))
            {
                await EnsureCategoryTranslationAsync(context, cat.Id, "en", "Artificial Intelligence & Data", "Google Gemini AI, LLMs and big data analytics");
                await EnsureCategoryTranslationAsync(context, cat.Id, "ru", "Искусственный интеллект и Данные", "Google Gemini AI, LLM и аналитика больших данных");
            }
            else if (cat.Slug.Equals("kiber-tehlukesizlik-devops", StringComparison.OrdinalIgnoreCase))
            {
                await EnsureCategoryTranslationAsync(context, cat.Id, "en", "Cybersecurity & DevOps", "Docker, Kubernetes, CI/CD and cloud security");
                await EnsureCategoryTranslationAsync(context, cat.Id, "ru", "Кибербезопасность и DevOps", "Docker, Kubernetes, CI/CD и облачная безопасность");
            }
        }

        // 2. Kurslar üzrə tərcümələr
        var allCourses = await context.Courses.ToListAsync();
        foreach (var c in allCourses)
        {
            if (!c.Slug.Equals("lkjhgfd", StringComparison.OrdinalIgnoreCase))
            {
                // Mövcud məlumatı az kimi miqrasiya et
                await EnsureCourseTranslationAsync(context, c.Id, "az", c.Title, c.ShortDescription, c.Description);
            }

            if (c.Slug.Equals("csharp-14-net-10-onion-architecture", StringComparison.OrdinalIgnoreCase))
            {
                await EnsureCourseTranslationAsync(context, c.Id, "en",
                    "Modern Onion Architecture with C# 14 & .NET 10",
                    "Clean Architecture, DDD, CQRS principles and enterprise LMS platform development with SQL Server.",
                    "In this course, you will explore the advanced capabilities of the .NET 10 ecosystem, mastering Onion Architecture, JWT security, QuestPDF and ClosedXML report generation from scratch.");

                await EnsureCourseTranslationAsync(context, c.Id, "ru",
                    "Современная Onion-архитектура с C# 14 и .NET 10",
                    "Принципы Clean Architecture, DDD, CQRS и разработка масштабной LMS платформы с SQL Server.",
                    "В этом курсе вы изучите самые передовые технологии экосистемы .NET 10, построив с нуля Onion Architecture, авторизацию JWT, генерацию отчетов QuestPDF и ClosedXML.");
            }
            else if (c.Slug.Equals("modern-frontend-alpine-tailwind", StringComparison.OrdinalIgnoreCase))
            {
                await EnsureCourseTranslationAsync(context, c.Id, "en",
                    "Modern Frontend: Alpine.js, Tailwind CSS & Reactive UI",
                    "Modern SPA design, component architecture, reactive state management, and YouTube API integration.",
                    "Build sleek, responsive Dark/Light mode interfaces using lightweight yet powerful Alpine.js 3 and Tailwind CSS as a modern alternative to heavyweight frameworks.");

                await EnsureCourseTranslationAsync(context, c.Id, "ru",
                    "Современный фронтенд: Alpine.js, Tailwind CSS и реактивный UI",
                    "Современный SPA дизайн, компонентная архитектура, реактивное состояние и интеграция с YouTube API.",
                    "Создание интерфейсов с поддержкой темной и светлой тем на базе легких и мощных Alpine.js 3 и Tailwind CSS в качестве практичной альтернативы тяжелым SPA-фреймворкам.");
            }
            else if (c.Slug.Equals("generative-ai-google-gemini-integration", StringComparison.OrdinalIgnoreCase))
            {
                await EnsureCourseTranslationAsync(context, c.Id, "en",
                    "Generative AI & LLM Integration: Google Gemini API",
                    "Google Gemini API, Socratic teaching methods, Prompt Engineering, and in-LMS RAG pipeline construction.",
                    "Build an intelligent mentor module that answers student inquiries instantly and adheres to pedagogical principles using Google Gemini AI and Socratic guidance.");

                await EnsureCourseTranslationAsync(context, c.Id, "ru",
                    "Генеративный ИИ и интеграция LLM: Google Gemini API",
                    "Google Gemini API, Сократовские методы обучения, Prompt Engineering и построение RAG-системы в LMS.",
                    "Умный модуль ИИ-ментора на базе Google Gemini API для мгновенных и педагогически выверенных ответов на вопросы студентов.");
            }
            else if (c.Slug.Equals("net-10-ve-clean-architecture", StringComparison.OrdinalIgnoreCase))
            {
                await EnsureCourseTranslationAsync(context, c.Id, "en",
                    ".NET 10 & Clean Architecture",
                    "Enterprise backend development, Clean Architecture patterns, EF Core 10, and high performance APIs.",
                    "A comprehensive guide to developing resilient enterprise backends with .NET 10, adhering to Clean Architecture principles, automated testing, and scalable data layers.");

                await EnsureCourseTranslationAsync(context, c.Id, "ru",
                    ".NET 10 и Clean Architecture",
                    "Корпоративная разработка бэкенда, паттерны Clean Architecture, EF Core 10 и высокопроизводительные API.",
                    "Полное руководство по разработке надежного бэкенда на .NET 10 с соблюдением принципов чистой архитектуры, автоматизированным тестированием и масштабируемым доступом к данным.");
            }
            else if (c.Slug.Equals("lkjhgfd", StringComparison.OrdinalIgnoreCase))
            {
                // Also update the AZ translation for lkjhgfd with realistic title
                await EnsureCourseTranslationAsync(context, c.Id, "az",
                    "DevOps və Bulud İnfrastrukturu Ustad Dərsi",
                    "Docker ilə konteynerləşdirmə, Kubernetes orkestrasiyası, CI/CD avtomatlaşdırma boru xətləri və infrastruktur kod kimi.",
                    "Peşəkar DevOps təcrübələri, konteyner təhlükəsizliyi, avtomatlaşdırılmış yerləşdirmə və miqyaslanan istehsal infrastrukturu.");

                await EnsureCourseTranslationAsync(context, c.Id, "en",
                    "DevOps & Cloud Infrastructure Masterclass",
                    "Containerization with Docker, Kubernetes orchestration, CI/CD automation pipelines, and infrastructure as code.",
                    "Learn professional DevOps practices, container security, automated deployments, and scalable production infrastructure.");

                await EnsureCourseTranslationAsync(context, c.Id, "ru",
                    "Мастер-класс по DevOps и облачной инфраструктуре",
                    "Контейнеризация с Docker, оркестрация в Kubernetes, CI/CD пайплайны и инфраструктура как код.",
                    "Изучите современные практики DevOps, безопасность контейнеров, автоматизированное развертывание и масштабируемую продакшн-инфраструктуру.");
            }
        }
    }

    private static async Task EnsureCategoryTranslationAsync(
        AppDbContext context,
        Guid categoryId,
        string language,
        string name,
        string? description)
    {
        var normLang = language.Trim().ToLowerInvariant();
        var trans = context.CategoryTranslations.Local.FirstOrDefault(t => t.CategoryId == categoryId && t.Language == normLang)
            ?? await context.CategoryTranslations.FirstOrDefaultAsync(t => t.CategoryId == categoryId && t.Language == normLang);
        if (trans == null)
        {
            context.CategoryTranslations.Add(new CategoryTranslation
            {
                CategoryId = categoryId,
                Language = normLang,
                Name = name,
                Description = description
            });
        }
        else
        {
            trans.Name = name;
            trans.Description = description;
        }
    }

    private static async Task EnsureCourseTranslationAsync(
        AppDbContext context,
        Guid courseId,
        string language,
        string title,
        string? shortDesc,
        string? desc)
    {
        var normLang = language.Trim().ToLowerInvariant();
        var trans = context.CourseTranslations.Local.FirstOrDefault(t => t.CourseId == courseId && t.Language == normLang)
            ?? await context.CourseTranslations.FirstOrDefaultAsync(t => t.CourseId == courseId && t.Language == normLang);
        if (trans == null)
        {
            context.CourseTranslations.Add(new CourseTranslation
            {
                CourseId = courseId,
                Language = normLang,
                Title = title,
                ShortDescription = shortDesc,
                Description = desc
            });
        }
        else
        {
            trans.Title = title;
            trans.ShortDescription = shortDesc;
            trans.Description = desc;
        }
    }

    private static async Task SeedCohortsAsync(
        AppDbContext context,
        ApplicationUser teacherUser,
        ApplicationUser studentEsmer,
        ApplicationUser studentLeyla,
        Course courseFrontend,
        Course courseBackend)
    {
        // 1. FE-101: Müasir Frontend
        var cohortFe = await context.Cohorts
            .Include(c => c.Schedules)
            .Include(c => c.Enrollments)
            .FirstOrDefaultAsync(c => c.CourseId == courseFrontend.Id && c.Name == "FE-101: Müasir Frontend");

        if (cohortFe == null)
        {
            cohortFe = new Cohort
            {
                Name = "FE-101: Müasir Frontend",
                CourseId = courseFrontend.Id,
                TeacherId = teacherUser.Id,
                StartDate = DateTime.UtcNow.AddMonths(-1),
                EndDate = DateTime.UtcNow.AddMonths(2),
                TotalHours = 80.0,
                CompletedHours = 24.0,
                QuizWeight = 30m,
                MiniProjectWeight = 30m,
                FinalWeight = 40m,
                MaxAbsenceLimit = 6,
                LateToAbsenceRatio = 3,
                Status = CohortStatus.Active
            };
            context.Cohorts.Add(cohortFe);
            await context.SaveChangesAsync();

            // Schedules (I, III, V günləri: Mon, Wed, Fri)
            context.CohortSchedules.AddRange(
                new CohortSchedule { CohortId = cohortFe.Id, DayOfWeek = DayOfWeek.Monday, StartTime = new TimeSpan(19, 0, 0), EndTime = new TimeSpan(21, 0, 0), RoomOrMeetingUrl = "Otaq 204 / Google Meet" },
                new CohortSchedule { CohortId = cohortFe.Id, DayOfWeek = DayOfWeek.Wednesday, StartTime = new TimeSpan(19, 0, 0), EndTime = new TimeSpan(21, 0, 0), RoomOrMeetingUrl = "Otaq 204 / Google Meet" },
                new CohortSchedule { CohortId = cohortFe.Id, DayOfWeek = DayOfWeek.Friday, StartTime = new TimeSpan(19, 0, 0), EndTime = new TimeSpan(21, 0, 0), RoomOrMeetingUrl = "Otaq 204 / Google Meet" }
            );

            // Enrollments
            context.CohortEnrollments.AddRange(
                new CohortEnrollment { CohortId = cohortFe.Id, StudentId = studentEsmer.Id, Status = CohortEnrollmentStatus.Enrolled, EnrolledAtUtc = DateTime.UtcNow.AddMonths(-1) },
                new CohortEnrollment { CohortId = cohortFe.Id, StudentId = studentLeyla.Id, Status = CohortEnrollmentStatus.Enrolled, EnrolledAtUtc = DateTime.UtcNow.AddMonths(-1) }
            );

            await context.SaveChangesAsync();

            // Attendance records for recent schedule day
            var pastDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2));
            while (pastDate.DayOfWeek != DayOfWeek.Monday && pastDate.DayOfWeek != DayOfWeek.Wednesday && pastDate.DayOfWeek != DayOfWeek.Friday)
            {
                pastDate = pastDate.AddDays(-1);
            }

            context.AttendanceRecords.AddRange(
                new AttendanceRecord { CohortId = cohortFe.Id, StudentId = studentEsmer.Id, Date = pastDate, Status = AttendanceStatus.Present, RecordedByUserId = teacherUser.Id },
                new AttendanceRecord { CohortId = cohortFe.Id, StudentId = studentLeyla.Id, Date = pastDate, Status = AttendanceStatus.Present, RecordedByUserId = teacherUser.Id }
            );

            await context.SaveChangesAsync();
        }

        // 2. BE-102: .NET 10 Clean Architecture
        var cohortBe = await context.Cohorts
            .Include(c => c.Schedules)
            .Include(c => c.Enrollments)
            .FirstOrDefaultAsync(c => c.CourseId == courseBackend.Id && c.Name == "BE-102: .NET 10 Clean Architecture");

        if (cohortBe == null)
        {
            cohortBe = new Cohort
            {
                Name = "BE-102: .NET 10 Clean Architecture",
                CourseId = courseBackend.Id,
                TeacherId = teacherUser.Id,
                StartDate = DateTime.UtcNow.AddDays(-14),
                EndDate = DateTime.UtcNow.AddMonths(3),
                TotalHours = 100.0,
                CompletedHours = 12.0,
                QuizWeight = 30m,
                MiniProjectWeight = 30m,
                FinalWeight = 40m,
                MaxAbsenceLimit = 6,
                LateToAbsenceRatio = 3,
                Status = CohortStatus.Active
            };
            context.Cohorts.Add(cohortBe);
            await context.SaveChangesAsync();

            // Schedules (II, IV, VI günləri: Tue, Thu, Sat)
            context.CohortSchedules.AddRange(
                new CohortSchedule { CohortId = cohortBe.Id, DayOfWeek = DayOfWeek.Tuesday, StartTime = new TimeSpan(19, 0, 0), EndTime = new TimeSpan(21, 0, 0), RoomOrMeetingUrl = "Otaq 305 / Jitsi" },
                new CohortSchedule { CohortId = cohortBe.Id, DayOfWeek = DayOfWeek.Thursday, StartTime = new TimeSpan(19, 0, 0), EndTime = new TimeSpan(21, 0, 0), RoomOrMeetingUrl = "Otaq 305 / Jitsi" },
                new CohortSchedule { CohortId = cohortBe.Id, DayOfWeek = DayOfWeek.Saturday, StartTime = new TimeSpan(11, 0, 0), EndTime = new TimeSpan(13, 0, 0), RoomOrMeetingUrl = "Otaq 305 / Jitsi" }
            );

            // Enrollments
            context.CohortEnrollments.AddRange(
                new CohortEnrollment { CohortId = cohortBe.Id, StudentId = studentEsmer.Id, Status = CohortEnrollmentStatus.Enrolled, EnrolledAtUtc = DateTime.UtcNow.AddDays(-14) },
                new CohortEnrollment { CohortId = cohortBe.Id, StudentId = studentLeyla.Id, Status = CohortEnrollmentStatus.Enrolled, EnrolledAtUtc = DateTime.UtcNow.AddDays(-14) }
            );

            await context.SaveChangesAsync();
        }
    }
}