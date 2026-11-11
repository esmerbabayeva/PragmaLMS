using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Pragma.Application.Common;
using Pragma.Application.Contracts;
using Pragma.Application.DTOs.Admin;
using Pragma.Application.DTOs.Quizzes;
using Pragma.Application.Services;
using Pragma.Domain.Common;
using Pragma.Domain.Entities;
using Pragma.Domain.Enums;
using Pragma.Infrastructure.Identity;
using Pragma.Infrastructure.Persistence;
using Pragma.Infrastructure.Services;
using Xunit;

namespace Pragma.UnitTests;

public class RoleAuthorizationPolicyTests
{
    private static AppDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static Mock<UserManager<ApplicationUser>> MockUserManager()
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        return new Mock<UserManager<ApplicationUser>>(store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
    }

    private static Mock<RoleManager<ApplicationRole>> MockRoleManager()
    {
        var store = new Mock<IRoleStore<ApplicationRole>>();
        return new Mock<RoleManager<ApplicationRole>>(store.Object, null!, null!, null!, null!);
    }

    // ==========================================
    // 1. ADMİN TESTLƏRİ VƏ QORUYUCU QAYDALAR
    // ==========================================

    [Fact]
    public async Task Admin_CannotDeleteThemselves_ReturnsForbiddenFailure()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var userManager = MockUserManager();
        var roleManager = MockRoleManager();
        var auditService = new Mock<IAuditLogService>();
        var adminService = new AdminService(userManager.Object, roleManager.Object, context, auditService.Object);

        var adminId = Guid.NewGuid();

        // Act: Admin özünü silməyə cəhd edir
        var result = await adminService.DeleteUserAsync(adminId, adminId, "admin@pragma.edu.az");

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("Admin.SelfDelete", result.Error.Code);
    }

    [Fact]
    public async Task Admin_CannotLockThemselves_ReturnsForbiddenFailure()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var userManager = MockUserManager();
        var roleManager = MockRoleManager();
        var auditService = new Mock<IAuditLogService>();
        var adminService = new AdminService(userManager.Object, roleManager.Object, context, auditService.Object);

        var adminId = Guid.NewGuid();

        // Act: Admin özünü bloklamağa cəhd edir
        var result = await adminService.ToggleUserLockAsync(adminId, adminId, "admin@pragma.edu.az");

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("Admin.SelfLock", result.Error.Code);
    }

    [Fact]
    public async Task Admin_CannotDemoteThemselves_ReturnsForbiddenFailure()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var userManager = MockUserManager();
        var roleManager = MockRoleManager();
        var auditService = new Mock<IAuditLogService>();
        var adminService = new AdminService(userManager.Object, roleManager.Object, context, auditService.Object);

        var adminId = Guid.NewGuid();
        var adminUser = new ApplicationUser { Id = adminId, Email = "admin@pragma.edu.az" };

        userManager.Setup(m => m.FindByIdAsync(adminId.ToString())).ReturnsAsync(adminUser);
        userManager.Setup(m => m.GetRolesAsync(adminUser)).ReturnsAsync(new List<string> { UserRoles.Admin });

        // Act: Admin özündən Admin rolunu çıxarmağa cəhd edir
        var result = await adminService.AssignUserRolesAsync(adminId, new ChangeUserRolesDto(new[] { UserRoles.Student }), adminId, "admin@pragma.edu.az");

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("Admin.SelfDemote", result.Error.Code);
    }

    [Fact]
    public async Task Admin_CannotDeleteLastActiveAdmin_ReturnsForbiddenFailure()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var userManager = MockUserManager();
        var roleManager = MockRoleManager();
        var auditService = new Mock<IAuditLogService>();
        var adminService = new AdminService(userManager.Object, roleManager.Object, context, auditService.Object);

        var callingAdminId = Guid.NewGuid();
        var targetAdminId = Guid.NewGuid();
        var targetAdmin = new ApplicationUser { Id = targetAdminId, Email = "lastadmin@pragma.edu.az", LockoutEnd = null };

        userManager.Setup(m => m.FindByIdAsync(targetAdminId.ToString())).ReturnsAsync(targetAdmin);
        userManager.Setup(m => m.IsInRoleAsync(targetAdmin, UserRoles.Admin)).ReturnsAsync(true);
        // Sistemdə yalnız 1 aktiv admin var
        userManager.Setup(m => m.GetUsersInRoleAsync(UserRoles.Admin)).ReturnsAsync(new List<ApplicationUser> { targetAdmin });

        // Act: Sonuncu admin silinməyə cəhd edilir
        var result = await adminService.DeleteUserAsync(targetAdminId, callingAdminId, "callingadmin@pragma.edu.az");

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("Admin.LastAdmin", result.Error.Code);
    }

    [Fact]
    public async Task Admin_AdjustStudentGrade_UpdatesEnrollmentAndWritesAuditLog()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var userManager = MockUserManager();
        var roleManager = MockRoleManager();
        var auditService = new Mock<IAuditLogService>();
        var adminService = new AdminService(userManager.Object, roleManager.Object, context, auditService.Object);

        var course = new Course { Title = "Test Kurs", Slug = "test-kurs-" + Guid.NewGuid().ToString("N")[..8] };
        context.Courses.Add(course);

        var enrollment = new Enrollment
        {
            CourseId = course.Id,
            Course = course,
            StudentId = Guid.NewGuid(),
            MiniProjectGrade = 60m,
            FinalGrade = 65m,
            AbsentCount = 2,
            LateCount = 1
        };
        context.Enrollments.Add(enrollment);
        await context.SaveChangesAsync();

        var adminId = Guid.NewGuid();
        var dto = new AdjustGradeDto(
            enrollment.Id,
            MiniProjectGrade: 85m,
            FinalGrade: 90m,
            AbsentCount: 1,
            LateCount: 0,
            Reason: "Apellyasiya nəticəsi təsdiqləndi."
        );

        // Act
        var result = await adminService.AdjustStudentGradeAsync(dto, adminId, "admin@pragma.edu.az");

        // Assert
        Assert.True(result.IsSuccess);
        var updated = await context.Enrollments.FindAsync(enrollment.Id);
        Assert.NotNull(updated);
        Assert.Equal(85m, updated.MiniProjectGrade);
        Assert.Equal(90m, updated.FinalGrade);
        Assert.Equal(1, updated.AbsentCount);

        // Audit log çağırışı yoxlanılır
        auditService.Verify(a => a.LogAsync(
            adminId,
            "admin@pragma.edu.az",
            "GradeAdjusted",
            "Enrollment",
            enrollment.Id.ToString(),
            It.Is<string>(d => d.Contains("Apellyasiya nəticəsi təsdiqləndi")),
            null,
            default), Times.Once);
    }

    // ==========================================
    // 2. INSTRUCTOR TESTLƏRİ VƏ RESURS SAHİBLİYİ
    // ==========================================

    [Fact]
    public async Task Instructor_CannotUpdateAnotherInstructorsCourse_ReturnsForbidden()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var accessService = new CourseAccessService(context);

        var instructor1Id = Guid.NewGuid();
        var instructor2Id = Guid.NewGuid();

        var course = new Course
        {
            Title = "Instructor 1 Kursu",
            Slug = "instructor-1-kursu",
            InstructorId = instructor1Id,
            Status = CourseStatus.Draft
        };
        context.Courses.Add(course);
        await context.SaveChangesAsync();

        // Act: Instructor 2 Kurs 1-i idarə etmək istəyir (isAdmin = false)
        var canManage = await accessService.CanManageCourseAsync(course.Id, instructor2Id, isAdmin: false);

        // Assert: Başqasının kursuna çıxış qadağandır
        Assert.False(canManage);

        // Amma Admin idarə edə bilər (isAdmin = true)
        var adminCanManage = await accessService.CanManageCourseAsync(course.Id, Guid.NewGuid(), isAdmin: true);
        Assert.True(adminCanManage);
    }

    [Fact]
    public async Task Instructor_CannotEnrollStudentInAnotherInstructorsCourse_ReturnsForbidden()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var enrollmentService = new EnrollmentService(context);

        var instructor1Id = Guid.NewGuid();
        var instructor2Id = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        var category = new Category { Name = "Backend", Slug = "backend-test" };
        context.Categories.Add(category);

        var course = new Course
        {
            Title = "Instructor 1 Kursu",
            Slug = "instructor-1-kursu",
            InstructorId = instructor1Id,
            CategoryId = category.Id,
            Status = CourseStatus.Published
        };
        context.Courses.Add(course);
        await context.SaveChangesAsync();

        // Act: Instructor 2 tələbəni Instructor 1-in kursuna əlavə etməyə cəhd edir (isAdmin = false)
        var result = await enrollmentService.ManageEnrollStudentAsync(course.Id, studentId, instructor2Id, isAdmin: false);

        // Assert: 403 Forbidden
        Assert.False(result.IsSuccess);
        Assert.Equal("Course.Forbidden", result.Error.Code);
    }

    // ==========================================
    // 3. STUDENT TESTLƏRİ VƏ GİRİŞ MƏHDUDİYYƏTLƏRİ
    // ==========================================

    [Fact]
    public async Task Student_CannotAccessLessonContent_IfNotEnrolled()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var accessService = new CourseAccessService(context);

        var course = new Course
        {
            Title = "Gizli Kurs",
            Slug = "gizli-kurs",
            InstructorId = Guid.NewGuid(),
            Status = CourseStatus.Published
        };
        var module = new Module { Course = course, Title = "Modul 1" };
        var lesson = new Lesson { Module = module, Title = "Ödənişli Dərs", IsPreview = false };

        context.Courses.Add(course);
        context.Modules.Add(module);
        context.Lessons.Add(lesson);
        await context.SaveChangesAsync();

        var unEnrolledStudentId = Guid.NewGuid();

        // Act: Qeydiyyatı olmayan tələbə dərsi açmaq istəyir
        var canAccess = await accessService.CanAccessLessonAsync(lesson.Id, unEnrolledStudentId, isAdmin: false);

        // Assert: Qadağandır (false)
        Assert.False(canAccess);

        // Əgər tələbə qeydiyyatdan keçərsə
        var enrollment = new Enrollment { CourseId = course.Id, StudentId = unEnrolledStudentId };
        context.Enrollments.Add(enrollment);
        await context.SaveChangesAsync();

        var canAccessAfterEnroll = await accessService.CanAccessLessonAsync(lesson.Id, unEnrolledStudentId, isAdmin: false);
        Assert.True(canAccessAfterEnroll);
    }

    [Fact]
    public void Student_QuizTakingDto_NeverExposesIsCorrect_Property()
    {
        // Arrange & Act: QuestionTakingDto və AnswerOptionTakingDto refleksiyası
        var optionType = typeof(AnswerOptionTakingDto);
        var questionType = typeof(QuestionTakingDto);

        var hasIsCorrectInOption = optionType.GetProperty("IsCorrect") != null;
        var hasIsCorrectInQuestion = questionType.GetProperty("IsCorrect") != null;

        // Assert: Tələbənin imtahan DTO-sunda heç vaxt IsCorrect sahəsi olmamalıdır!
        Assert.False(hasIsCorrectInOption);
        Assert.False(hasIsCorrectInQuestion);
    }

    [Fact]
    public async Task AuthorizationService_Policies_EnforceCorrectRoleHierarchy()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthorizationPolicies.RequireAdmin, p => p.RequireRole(UserRoles.Admin));
            options.AddPolicy(AuthorizationPolicies.CanManageUsers, p => p.RequireRole(UserRoles.Admin));
            options.AddPolicy(AuthorizationPolicies.CanManageCourses, p => p.RequireRole(UserRoles.Admin, UserRoles.Instructor));
            options.AddPolicy(AuthorizationPolicies.RequireStudent, p => p.RequireRole(UserRoles.Student));
            options.AddPolicy(AuthorizationPolicies.CanTakeQuiz, p => p.RequireRole(UserRoles.Student));
            options.AddPolicy(AuthorizationPolicies.CanTrackProgress, p => p.RequireRole(UserRoles.Student));
            options.AddPolicy(AuthorizationPolicies.CanViewOwnCourses, p => p.RequireRole(UserRoles.Student));
        });

        var provider = services.BuildServiceProvider();
        var authService = provider.GetRequiredService<IAuthorizationService>();

        var adminUser = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, UserRoles.Admin) }, "test"));
        var instructorUser = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, UserRoles.Instructor) }, "test"));
        var teacherUser = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, UserRoles.Teacher) }, "test"));
        var studentUser = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, UserRoles.Student) }, "test"));

        // Admin checks
        Assert.True((await authService.AuthorizeAsync(adminUser, AuthorizationPolicies.RequireAdmin)).Succeeded);
        Assert.True((await authService.AuthorizeAsync(adminUser, AuthorizationPolicies.CanManageUsers)).Succeeded);
        Assert.True((await authService.AuthorizeAsync(adminUser, AuthorizationPolicies.CanManageCourses)).Succeeded);
        Assert.False((await authService.AuthorizeAsync(adminUser, AuthorizationPolicies.CanViewOwnCourses)).Succeeded);

        // Instructor checks
        Assert.False((await authService.AuthorizeAsync(instructorUser, AuthorizationPolicies.RequireAdmin)).Succeeded);
        Assert.False((await authService.AuthorizeAsync(instructorUser, AuthorizationPolicies.CanManageUsers)).Succeeded);
        Assert.True((await authService.AuthorizeAsync(instructorUser, AuthorizationPolicies.CanManageCourses)).Succeeded);
        Assert.False((await authService.AuthorizeAsync(instructorUser, AuthorizationPolicies.CanViewOwnCourses)).Succeeded);
        Assert.False((await authService.AuthorizeAsync(instructorUser, AuthorizationPolicies.CanTrackProgress)).Succeeded);
        Assert.False((await authService.AuthorizeAsync(instructorUser, AuthorizationPolicies.CanTakeQuiz)).Succeeded);

        // Teacher checks
        Assert.False((await authService.AuthorizeAsync(teacherUser, AuthorizationPolicies.RequireAdmin)).Succeeded);
        Assert.False((await authService.AuthorizeAsync(teacherUser, AuthorizationPolicies.CanManageCourses)).Succeeded);
        Assert.False((await authService.AuthorizeAsync(teacherUser, AuthorizationPolicies.CanViewOwnCourses)).Succeeded);
        Assert.False((await authService.AuthorizeAsync(teacherUser, AuthorizationPolicies.CanTrackProgress)).Succeeded);
        Assert.False((await authService.AuthorizeAsync(teacherUser, AuthorizationPolicies.CanTakeQuiz)).Succeeded);

        // Student checks
        Assert.False((await authService.AuthorizeAsync(studentUser, AuthorizationPolicies.RequireAdmin)).Succeeded);
        Assert.False((await authService.AuthorizeAsync(studentUser, AuthorizationPolicies.CanManageCourses)).Succeeded);
        Assert.True((await authService.AuthorizeAsync(studentUser, AuthorizationPolicies.RequireStudent)).Succeeded);
        Assert.True((await authService.AuthorizeAsync(studentUser, AuthorizationPolicies.CanTakeQuiz)).Succeeded);
        Assert.True((await authService.AuthorizeAsync(studentUser, AuthorizationPolicies.CanTrackProgress)).Succeeded);
        Assert.True((await authService.AuthorizeAsync(studentUser, AuthorizationPolicies.CanViewOwnCourses)).Succeeded);
    }
}
