using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Pragma.Application.Common;
using Pragma.Application.Contracts;
using Pragma.Application.DTOs.Quizzes;
using Pragma.Application.DTOs.Teacher;
using Pragma.Application.Services;
using Pragma.Application.Validators.Quizzes;
using Pragma.Domain.Entities;
using Pragma.Domain.Enums;
using Pragma.Infrastructure.Persistence;
using Xunit;

namespace Pragma.UnitTests;

public class TeacherAuthorizationTests
{
    private static AppDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task AuthorizationPolicies_ShouldEnforceRoleSeparation_AccordingToMatrix()
    {
        // Arrange
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging();
        serviceCollection.AddAuthorization(options =>
        {
            options.AddPolicy(AuthorizationPolicies.CanManageCourses, policy =>
                policy.RequireRole(UserRoles.Admin, UserRoles.Instructor, UserRoles.Moderator));

            options.AddPolicy(AuthorizationPolicies.CanManageEnrollments, policy =>
                policy.RequireRole(UserRoles.Admin, UserRoles.Instructor, UserRoles.Moderator));

            options.AddPolicy(AuthorizationPolicies.CanManageCohorts, policy =>
                policy.RequireRole(UserRoles.Admin, UserRoles.Instructor, UserRoles.Moderator));

            options.AddPolicy(AuthorizationPolicies.CanManageUsers, policy =>
                policy.RequireRole(UserRoles.Admin));

            options.AddPolicy(AuthorizationPolicies.CanAccessAdminPanel, policy =>
                policy.RequireRole(UserRoles.Admin));

            options.AddPolicy(AuthorizationPolicies.CanManageQuiz, policy =>
                policy.RequireRole(UserRoles.Admin, UserRoles.Instructor, UserRoles.Teacher));

            options.AddPolicy(AuthorizationPolicies.CanViewGroups, policy =>
                policy.RequireRole(UserRoles.Admin, UserRoles.Moderator, UserRoles.Instructor, UserRoles.Teacher));

            options.AddPolicy(AuthorizationPolicies.CanGradeStudents, policy =>
                policy.RequireRole(UserRoles.Admin, UserRoles.Instructor, UserRoles.Teacher));

            options.AddPolicy(AuthorizationPolicies.CanViewResults, policy =>
                policy.RequireRole(UserRoles.Admin, UserRoles.Moderator, UserRoles.Instructor, UserRoles.Teacher));
        });

        var provider = serviceCollection.BuildServiceProvider();
        var authService = provider.GetRequiredService<IAuthorizationService>();

        // Act & Assert: Teacher rolunda olan istifadəçi
        var teacherPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, UserRoles.Teacher)
        }, "TestAuth"));

        // 1. Teacher Kurs yarada, silə və ya dərc edə BİLMƏZ (CanManageCourses: FALSE)
        var canManageCourses = await authService.AuthorizeAsync(teacherPrincipal, AuthorizationPolicies.CanManageCourses);
        Assert.False(canManageCourses.Succeeded);

        // 2. Teacher Kursa tələbə əlavə/silə BİLMƏZ (CanManageEnrollments: FALSE)
        var canManageEnrollments = await authService.AuthorizeAsync(teacherPrincipal, AuthorizationPolicies.CanManageEnrollments);
        Assert.False(canManageEnrollments.Succeeded);

        // 3. Teacher Qrup (Cohort) yarada və ya silə BİLMƏZ (CanManageCohorts: FALSE)
        var canManageCohorts = await authService.AuthorizeAsync(teacherPrincipal, AuthorizationPolicies.CanManageCohorts);
        Assert.False(canManageCohorts.Succeeded);

        // 4. Teacher İstifadəçi idarə edə BİLMƏZ (CanManageUsers: FALSE)
        var canManageUsers = await authService.AuthorizeAsync(teacherPrincipal, AuthorizationPolicies.CanManageUsers);
        Assert.False(canManageUsers.Succeeded);

        // 5. Teacher Admin Panelinə (port 5089 və ya admin konsola) girə BİLMƏZ (CanAccessAdminPanel: FALSE)
        var canAccessAdminPanel = await authService.AuthorizeAsync(teacherPrincipal, AuthorizationPolicies.CanAccessAdminPanel);
        Assert.False(canAccessAdminPanel.Succeeded);

        // 6. Teacher Kviz idarə edə BİLƏR (CanManageQuiz: TRUE)
        var canManageQuiz = await authService.AuthorizeAsync(teacherPrincipal, AuthorizationPolicies.CanManageQuiz);
        Assert.True(canManageQuiz.Succeeded);

        // 7. Teacher Qruplara baxa BİLƏR (CanViewGroups: TRUE)
        var canViewGroups = await authService.AuthorizeAsync(teacherPrincipal, AuthorizationPolicies.CanViewGroups);
        Assert.True(canViewGroups.Succeeded);

        // 8. Teacher Tələbələrə bal qoya BİLƏR (CanGradeStudents: TRUE)
        var canGradeStudents = await authService.AuthorizeAsync(teacherPrincipal, AuthorizationPolicies.CanGradeStudents);
        Assert.True(canGradeStudents.Succeeded);

        // 9. Teacher Nəticələrə və Gradebook-a baxa BİLƏR (CanViewResults: TRUE)
        var canViewResults = await authService.AuthorizeAsync(teacherPrincipal, AuthorizationPolicies.CanViewResults);
        Assert.True(canViewResults.Succeeded);
    }

    [Fact]
    public async Task TeacherService_GetAssignedGroups_ShouldReturnOnlyTeacherOwnGroups()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var teacher1Id = Guid.NewGuid();
        var teacher2Id = Guid.NewGuid();

        var cat = new Category { Name = "Backend", Slug = "backend" };
        context.Categories.Add(cat);

        // Teacher 1-in 2 kursu
        var course1 = new Course { Title = "C# 14", InstructorId = teacher1Id, CategoryId = cat.Id };
        var course2 = new Course { Title = ".NET 10", InstructorId = teacher1Id, CategoryId = cat.Id };
        // Teacher 2-nin 1 kursu
        var course3 = new Course { Title = "Frontend", InstructorId = teacher2Id, CategoryId = cat.Id };

        context.Courses.AddRange(course1, course2, course3);
        await context.SaveChangesAsync();

        var mockLookup = new Mock<IUserLookupService>();
        var service = new TeacherService(context, mockLookup.Object);

        // Act
        var result = await service.GetAssignedGroupsAsync(teacher1Id, isAdmin: false);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
        Assert.Contains(result.Value, g => g.Id == course1.Id);
        Assert.Contains(result.Value, g => g.Id == course2.Id);
        Assert.DoesNotContain(result.Value, g => g.Id == course3.Id);
    }

    [Fact]
    public async Task TeacherService_GetGroupStudents_WhenAccessingOtherTeacherGroup_ShouldReturnForbidden()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var teacher1Id = Guid.NewGuid();
        var teacher2Id = Guid.NewGuid();

        var cat = new Category { Name = "AI", Slug = "ai" };
        var course = new Course { Title = "Gemini AI", InstructorId = teacher2Id, CategoryId = cat.Id };
        context.Categories.Add(cat);
        context.Courses.Add(course);
        await context.SaveChangesAsync();

        var mockLookup = new Mock<IUserLookupService>();
        var service = new TeacherService(context, mockLookup.Object);

        // Act: Teacher 1 başqa müəllimin (Teacher 2) qrupuna baxmaq istəyir
        var result = await service.GetGroupStudentsAsync(course.Id, teacher1Id, isAdmin: false);

        // Assert: 403 Forbidden
        Assert.True(result.IsFailure);
        Assert.Equal("Group.Forbidden", result.Error.Code);
    }

    [Fact]
    public async Task TeacherService_AssignGrade_WhenTeacherIsOwner_ShouldUpdateGradesAndFeedback()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var teacherId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        var cat = new Category { Name = "Cloud", Slug = "cloud" };
        var course = new Course { Title = "Docker & K8s", InstructorId = teacherId, CategoryId = cat.Id };
        var enrollment = new Enrollment { CourseId = course.Id, StudentId = studentId };

        context.Categories.Add(cat);
        context.Courses.Add(course);
        context.Enrollments.Add(enrollment);
        await context.SaveChangesAsync();

        var mockLookup = new Mock<IUserLookupService>();
        var service = new TeacherService(context, mockLookup.Object);

        var dto = new AssignGradeDto(
            GroupId: course.Id,
            StudentId: studentId,
            MiniProjectGrade: 85.5m,
            FinalGrade: 90.0m,
            AbsentCount: 1,
            LateCount: 2,
            TeacherFeedback: "Əla nəticə, layihə tam standarta uyğundur."
        );

        // Act
        var result = await service.AssignGradeAsync(dto, teacherId, isAdmin: false);

        // Assert
        Assert.True(result.IsSuccess);
        var updated = await context.Enrollments.FirstAsync(e => e.Id == enrollment.Id);
        Assert.Equal(85.5m, updated.MiniProjectGrade);
        Assert.Equal(90.0m, updated.FinalGrade);
        Assert.Equal(1, updated.AbsentCount);
        Assert.Equal(2, updated.LateCount);
        Assert.Equal("Əla nəticə, layihə tam standarta uyğundur.", updated.TeacherFeedback);
        Assert.NotNull(updated.GradedAtUtc);
    }

    [Fact]
    public async Task TeacherService_AssignGrade_WhenAccessingOtherTeacherGroup_ShouldReturnForbidden()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var teacher1Id = Guid.NewGuid();
        var teacher2Id = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        var cat = new Category { Name = "Cloud", Slug = "cloud" };
        var course = new Course { Title = "DevOps", InstructorId = teacher2Id, CategoryId = cat.Id };
        var enrollment = new Enrollment { CourseId = course.Id, StudentId = studentId };

        context.Categories.Add(cat);
        context.Courses.Add(course);
        context.Enrollments.Add(enrollment);
        await context.SaveChangesAsync();

        var mockLookup = new Mock<IUserLookupService>();
        var service = new TeacherService(context, mockLookup.Object);

        var dto = new AssignGradeDto(course.Id, studentId, 75m, 80m, 0, 0, "Test");

        // Act: Teacher 1 başqasının kursundakı tələbəyə bal qoymaq istəyir
        var result = await service.AssignGradeAsync(dto, teacher1Id, isAdmin: false);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Group.Forbidden", result.Error.Code);
    }

    [Fact]
    public async Task AcademicGradeCalculation_ShouldFollow30_30_40RuleAndProvisionalLogic()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var teacherId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        var cat = new Category { Name = "Software", Slug = "software" };
        var course = new Course { Title = "Algorithms", InstructorId = teacherId, CategoryId = cat.Id };
        var mod = new Module { CourseId = course.Id, Title = "Module 1" };
        var lesson = new Lesson { ModuleId = mod.Id, Title = "Lesson 1" };
        var quiz = new Quiz { LessonId = lesson.Id, Title = "Alg Quiz", PassingScorePercentage = 70, MaxAttempts = 3 };

        var attempt = new QuizAttempt
        {
            QuizId = quiz.Id,
            StudentId = studentId,
            AttemptNumber = 1,
            ScorePercentage = 80.0,
            IsPassed = true,
            Status = QuizAttemptStatus.Completed
        };

        var enrollment = new Enrollment
        {
            CourseId = course.Id,
            StudentId = studentId,
            MiniProjectGrade = 90.0m,
            FinalGrade = 85.0m,
            AbsentCount = 2,
            LateCount = 3
        };

        context.Categories.Add(cat);
        context.Courses.Add(course);
        context.Modules.Add(mod);
        context.Lessons.Add(lesson);
        context.Quizzes.Add(quiz);
        context.QuizAttempts.Add(attempt);
        context.Enrollments.Add(enrollment);
        await context.SaveChangesAsync();

        var mockLookup = new Mock<IUserLookupService>();
        mockLookup.Setup(l => l.GetUsersByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, UserBasicInfoDto>
            {
                [studentId] = new UserBasicInfoDto(studentId, "Əli Həsənov", "ali@pragma.edu.az")
            });

        var service = new TeacherService(context, mockLookup.Object);

        // Act
        var result = await service.GetGroupStudentsAsync(course.Id, teacherId, isAdmin: false);

        // Assert
        Assert.True(result.IsSuccess);
        var studentRecord = Assert.Single(result.Value);
        Assert.Equal(80.0, studentRecord.QuizAverage);
        Assert.Equal(90.0m, studentRecord.MiniProjectGrade);
        Assert.Equal(85.0m, studentRecord.FinalGrade);
        Assert.Equal(85.0m, studentRecord.FinalScore);
        Assert.False(studentRecord.IsProvisional);
        Assert.Equal(3, studentRecord.TotalAbsences);
        Assert.Equal("Aktiv", studentRecord.Status);
    }

    [Fact]
    public async Task AcademicGradeCalculation_WhenAbsenceExceedsLimit_ShouldMarkAsDropped()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var teacherId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        var cat = new Category { Name = "Math", Slug = "math" };
        var course = new Course { Title = "Linear Algebra", InstructorId = teacherId, CategoryId = cat.Id };

        var enrollment = new Enrollment
        {
            CourseId = course.Id,
            StudentId = studentId,
            AbsentCount = 6,
            LateCount = 4
        };

        context.Categories.Add(cat);
        context.Courses.Add(course);
        context.Enrollments.Add(enrollment);
        await context.SaveChangesAsync();

        var mockLookup = new Mock<IUserLookupService>();
        mockLookup.Setup(l => l.GetUsersByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, UserBasicInfoDto>
            {
                [studentId] = new UserBasicInfoDto(studentId, "Tələbə Test", "test@pragma.edu.az")
            });

        var service = new TeacherService(context, mockLookup.Object);

        // Act
        var result = await service.GetGroupStudentsAsync(course.Id, teacherId, isAdmin: false);

        // Assert
        Assert.True(result.IsSuccess);
        var studentRecord = Assert.Single(result.Value);
        Assert.Equal(7, studentRecord.TotalAbsences);
        Assert.Equal("Dropped", studentRecord.Status);
    }

    [Fact]
    public async Task TeacherQuizCreation_SingleAndMultipleChoice_ShouldBeSupportedAndUpsertGracefully()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var teacherId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        var cat = new Category { Name = "Backend", Slug = "backend-test" };
        var course = new Course { Title = "C# 14", InstructorId = teacherId, CategoryId = cat.Id };
        var mod = new Module { CourseId = course.Id, Title = "Module 1" };
        var lesson = new Lesson { ModuleId = mod.Id, Title = "Lesson 1" };
        var enrollment = new Enrollment { CourseId = course.Id, StudentId = studentId };

        context.Categories.Add(cat);
        context.Courses.Add(course);
        context.Modules.Add(mod);
        context.Lessons.Add(lesson);
        context.Enrollments.Add(enrollment);
        await context.SaveChangesAsync();

        var mockLookup = new Mock<IUserLookupService>();
        mockLookup.Setup(l => l.GetUserIdsInRoleAsync(UserRoles.Teacher, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<Guid> { teacherId });

        var accessService = new CourseAccessService(context, mockLookup.Object);
        var validator = new CreateQuizValidator();
        var quizService = new QuizService(context, accessService, validator);

        // Act 1: Teacher creates quiz with 1 single-choice and 1 multiple-choice question
        var createDto = new CreateQuizDto(
            Title: "Test Single and Multiple Quiz",
            Description: "Testing both question types",
            PassingScorePercentage: 70,
            TimeLimitMinutes: 15,
            MaxAttempts: 3,
            Questions: new List<CreateQuestionDto>
            {
                new(
                    Text: "Single choice: What is 2 + 2?",
                    Points: 10,
                    OrderIndex: 1,
                    Explanation: "2 + 2 is 4",
                    Options: new List<CreateAnswerOptionDto>
                    {
                        new("3", false),
                        new("4", true),
                        new("5", false)
                    }
                ),
                new(
                    Text: "Multiple choice: Which of these are .NET languages?",
                    Points: 20,
                    OrderIndex: 2,
                    Explanation: "C# and F# are .NET languages",
                    Options: new List<CreateAnswerOptionDto>
                    {
                        new("C#", true),
                        new("F#", true),
                        new("Python", false)
                    }
                )
            }
        );

        var createResult = await quizService.CreateQuizAsync(lesson.Id, createDto, teacherId, isAdmin: false);
        Assert.True(createResult.IsSuccess);
        var quizId = createResult.Value;

        // Act 2: Re-saving/updating for same lesson should upsert gracefully without conflict
        var upsertResult = await quizService.CreateQuizAsync(lesson.Id, createDto, teacherId, isAdmin: false);
        Assert.True(upsertResult.IsSuccess);

        // Act 3: Student fetches quiz for taking via GetQuizByLessonIdAsync
        var takingResult = await quizService.GetQuizByLessonIdAsync(lesson.Id, studentId);
        Assert.True(takingResult.IsSuccess);
        var takingDto = takingResult.Value;
        Assert.Equal(2, takingDto.Questions.Count);

        var q1 = takingDto.Questions.First(q => q.OrderIndex == 1);
        var q2 = takingDto.Questions.First(q => q.OrderIndex == 2);
        Assert.False(q1.IsMultipleChoice);
        Assert.True(q2.IsMultipleChoice);

        // Act 4: Student submits answers (both single and multiple correct)
        var q1CorrectOpt = context.AnswerOptions.First(o => o.Text == "4").Id;
        var q2OptCSharp = context.AnswerOptions.First(o => o.Text == "C#").Id;
        var q2OptFSharp = context.AnswerOptions.First(o => o.Text == "F#").Id;

        var submitDto = new SubmitQuizRequestDto(new List<SubmitQuizAnswerDto>
        {
            new(q1.Id, q1CorrectOpt, null),
            new(q2.Id, null, new List<Guid> { q2OptCSharp, q2OptFSharp })
        });

        var submitResult = await quizService.SubmitQuizAsync(quizId, studentId, submitDto);
        Assert.True(submitResult.IsSuccess);
        Assert.True(submitResult.Value.IsPassed);
        Assert.Equal(100.0, submitResult.Value.ScorePercentage);
        Assert.Equal(30, submitResult.Value.EarnedPoints);
    }
}
