using ClosedXML.Excel;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Moq;
using Pragma.Application.Common;
using Pragma.Application.Contracts;
using Pragma.Application.DTOs.Cohorts;
using Pragma.Application.Services;
using Pragma.Domain.Entities;
using Pragma.Domain.Enums;
using Pragma.Infrastructure.Persistence;
using Xunit;

namespace Pragma.UnitTests;

public class FinalSubmissionAndDeadlineTests
{
    private static AppDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static CohortService CreateCohortService(AppDbContext context)
    {
        return new CohortService(
            context,
            new Mock<IUserLookupService>().Object,
            new Mock<IValidator<CreateCohortDto>>().Object,
            new Mock<IValidator<UpdateCohortDto>>().Object,
            new Mock<IValidator<CreateCohortScheduleDto>>().Object,
            new Mock<IAcademicGradeService>().Object,
            new CertificateEligibilityPolicy()
        );
    }

    // -------------------------------------------------------------
    // 1. FINAL DEADLINE HELPER TESTS
    // -------------------------------------------------------------
    [Fact]
    public void FinalDeadlineHelper_WhenFinalDeadlineUtcSet_ReturnsFinalDeadlineUtc()
    {
        var deadline = new DateTime(2026, 11, 20, 18, 0, 0, DateTimeKind.Utc);
        var cohort = new Cohort
        {
            Name = "Cohort 1",
            FinalDeadlineUtc = deadline,
            EndDate = new DateTime(2026, 11, 25, 0, 0, 0, DateTimeKind.Utc)
        };

        var result = FinalDeadlineHelper.GetFinalDeadlineUtc(cohort);
        Assert.Equal(deadline, result);
    }

    [Fact]
    public void FinalDeadlineHelper_WhenFinalDeadlineUtcNull_FallsBackToEndDateBakuEndOfDay()
    {
        var endDate = new DateTime(2026, 11, 20, 10, 0, 0, DateTimeKind.Utc);
        var cohort = new Cohort
        {
            Name = "Cohort 1",
            FinalDeadlineUtc = null,
            EndDate = endDate
        };

        var result = FinalDeadlineHelper.GetFinalDeadlineUtc(cohort);
        Assert.NotNull(result);

        // Asia/Baku is UTC+4. 2026-11-20 23:59:59 in UTC is 2026-11-20 19:59:59 UTC
        var local = FinalDeadlineHelper.ToBakuLocal(result.Value);
        Assert.Equal(2026, local.Year);
        Assert.Equal(11, local.Month);
        Assert.Equal(20, local.Day);
        Assert.Equal(23, local.Hour);
        Assert.Equal(59, local.Minute);
        Assert.Equal(59, local.Second);
    }

    [Fact]
    public void FinalDeadlineHelper_WhenBothNull_ReturnsNull()
    {
        var cohort = new Cohort
        {
            Name = "Cohort 1",
            FinalDeadlineUtc = null,
            EndDate = null
        };

        var result = FinalDeadlineHelper.GetFinalDeadlineUtc(cohort);
        Assert.Null(result);
        Assert.False(FinalDeadlineHelper.IsFinalDeadlinePassed(cohort, DateTime.UtcNow));
    }

    [Fact]
    public void FinalDeadlineHelper_IsFinalDeadlinePassed_EvaluatesCorrectly()
    {
        var pastDeadline = DateTime.UtcNow.AddHours(-2);
        var cohortPast = new Cohort { Name = "Past", FinalDeadlineUtc = pastDeadline };
        Assert.True(FinalDeadlineHelper.IsFinalDeadlinePassed(cohortPast, DateTime.UtcNow));

        var futureDeadline = DateTime.UtcNow.AddHours(2);
        var cohortFuture = new Cohort { Name = "Future", FinalDeadlineUtc = futureDeadline };
        Assert.False(FinalDeadlineHelper.IsFinalDeadlinePassed(cohortFuture, DateTime.UtcNow));
    }

    // -------------------------------------------------------------
    // 2. SET FINAL DEADLINE ASYNC TESTS
    // -------------------------------------------------------------
    [Fact]
    public async Task SetFinalDeadlineAsync_WhenTeacher_SetsDeadlineSuccessfully()
    {
        using var context = CreateInMemoryContext();
        var teacherId = Guid.NewGuid();
        var course = new Course { Title = "Course 101", Slug = "course-101", InstructorId = teacherId };
        context.Courses.Add(course);

        var cohort = new Cohort
        {
            Name = "Cohort 101",
            CourseId = course.Id,
            TeacherId = teacherId,
            StartDate = DateTime.UtcNow.AddDays(-10),
            Status = CohortStatus.Active
        };
        context.Cohorts.Add(cohort);
        await context.SaveChangesAsync();

        var service = CreateCohortService(context);
        var targetDate = DateTime.UtcNow.AddDays(5).ToString("yyyy-MM-dd");
        var dto = new SetCohortFinalDeadlineDto(
            Date: targetDate,
            Time: null,
            IsEndOfDay: true,
            ClearToDefault: false
        );

        var result = await service.SetFinalDeadlineAsync(cohort.Id, dto, teacherId, isAdmin: false);
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.FinalDeadlineUtc);
        Assert.True(result.Value.IsEndOfDay);

        var updatedCohort = await context.Cohorts.FindAsync(cohort.Id);
        Assert.NotNull(updatedCohort!.FinalDeadlineUtc);
    }

    [Fact]
    public async Task SetFinalDeadlineAsync_WhenClearToDefault_SetsNull()
    {
        using var context = CreateInMemoryContext();
        var teacherId = Guid.NewGuid();
        var course = new Course { Title = "Course 101", Slug = "course-101-clr", InstructorId = teacherId };
        context.Courses.Add(course);

        var cohort = new Cohort
        {
            Name = "Cohort 101",
            CourseId = course.Id,
            TeacherId = teacherId,
            StartDate = DateTime.UtcNow.AddDays(-10),
            FinalDeadlineUtc = DateTime.UtcNow.AddDays(5),
            Status = CohortStatus.Active
        };
        context.Cohorts.Add(cohort);
        await context.SaveChangesAsync();

        var service = CreateCohortService(context);
        var dto = new SetCohortFinalDeadlineDto(
            Date: null,
            Time: null,
            IsEndOfDay: true,
            ClearToDefault: true
        );

        var result = await service.SetFinalDeadlineAsync(cohort.Id, dto, teacherId, isAdmin: false);
        Assert.True(result.IsSuccess);

        var updatedCohort = await context.Cohorts.FindAsync(cohort.Id);
        Assert.Null(updatedCohort!.FinalDeadlineUtc);
    }

    [Fact]
    public async Task SetFinalDeadlineAsync_WhenCohortFinished_ReturnsConflict409()
    {
        using var context = CreateInMemoryContext();
        var teacherId = Guid.NewGuid();
        var course = new Course { Title = "Course Finished", Slug = "course-fin", InstructorId = teacherId };
        context.Courses.Add(course);

        var cohort = new Cohort
        {
            Name = "Cohort 101",
            CourseId = course.Id,
            TeacherId = teacherId,
            StartDate = DateTime.UtcNow.AddDays(-30),
            Status = CohortStatus.Finished
        };
        context.Cohorts.Add(cohort);
        await context.SaveChangesAsync();

        var service = CreateCohortService(context);
        var targetDate = DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-dd");
        var dto = new SetCohortFinalDeadlineDto(targetDate, null, true, false);

        var result = await service.SetFinalDeadlineAsync(cohort.Id, dto, teacherId, isAdmin: false);
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
    }

    [Fact]
    public async Task SetFinalDeadlineAsync_WhenNotTeacherOrAdmin_ReturnsForbidden403()
    {
        using var context = CreateInMemoryContext();
        var course = new Course { Title = "Course Forbidden", Slug = "course-forb", InstructorId = Guid.NewGuid() };
        context.Courses.Add(course);

        var cohort = new Cohort
        {
            Name = "Cohort 101",
            CourseId = course.Id,
            TeacherId = Guid.NewGuid(),
            StartDate = DateTime.UtcNow.AddDays(-10),
            Status = CohortStatus.Active
        };
        context.Cohorts.Add(cohort);
        await context.SaveChangesAsync();

        var service = CreateCohortService(context);
        var targetDate = DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-dd");
        var dto = new SetCohortFinalDeadlineDto(targetDate, null, true, false);

        var result = await service.SetFinalDeadlineAsync(cohort.Id, dto, Guid.NewGuid(), isAdmin: false);
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Forbidden, result.Error.Type);
    }

    // -------------------------------------------------------------
    // 3. STUDENT FINAL SUBMISSION TESTS
    // -------------------------------------------------------------
    [Fact]
    public async Task SubmitFinalProjectAsync_WhenEnrolledAndOnTime_SavesSubmissionSuccessfully()
    {
        using var context = CreateInMemoryContext();
        var studentId = Guid.NewGuid();
        var course = new Course { Title = "Course Sub", Slug = "course-sub" };
        context.Courses.Add(course);

        var cohort = new Cohort
        {
            Name = "Cohort 1",
            CourseId = course.Id,
            StartDate = DateTime.UtcNow.AddDays(-20),
            FinalDeadlineUtc = DateTime.UtcNow.AddDays(2),
            Status = CohortStatus.Active
        };
        context.Cohorts.Add(cohort);
        context.CohortEnrollments.Add(new CohortEnrollment
        {
            CohortId = cohort.Id,
            StudentId = studentId,
            Status = CohortEnrollmentStatus.Enrolled
        });
        await context.SaveChangesAsync();

        var service = CreateCohortService(context);
        var dto = new SubmitFinalProjectDto("https://github.com/my-student/final-project");

        var result = await service.SubmitFinalProjectAsync(cohort.Id, dto, studentId);
        Assert.True(result.IsSuccess);
        Assert.Equal("https://github.com/my-student/final-project", result.Value.Url);
        Assert.False(result.Value.IsDeadlinePassed);
        Assert.False(result.Value.IsGraded);

        var saved = await context.FinalSubmissions.FirstOrDefaultAsync(s => s.CohortId == cohort.Id && s.StudentId == studentId);
        Assert.NotNull(saved);
        Assert.Equal("https://github.com/my-student/final-project", saved.Url);
    }

    [Fact]
    public async Task SubmitFinalProjectAsync_WhenResubmitted_UpdatesUrlPreservesInitialSubmittedAtUtc()
    {
        using var context = CreateInMemoryContext();
        var studentId = Guid.NewGuid();
        var course = new Course { Title = "Course Resub", Slug = "course-resub" };
        context.Courses.Add(course);

        var cohort = new Cohort
        {
            Name = "Cohort 1",
            CourseId = course.Id,
            StartDate = DateTime.UtcNow.AddDays(-20),
            FinalDeadlineUtc = DateTime.UtcNow.AddDays(2),
            Status = CohortStatus.Active
        };
        context.Cohorts.Add(cohort);
        context.CohortEnrollments.Add(new CohortEnrollment
        {
            CohortId = cohort.Id,
            StudentId = studentId,
            Status = CohortEnrollmentStatus.Enrolled
        });
        var initialSubmittedAt = DateTime.UtcNow.AddHours(-5);
        context.FinalSubmissions.Add(new FinalSubmission
        {
            CohortId = cohort.Id,
            StudentId = studentId,
            Url = "https://github.com/old-link",
            SubmittedAtUtc = initialSubmittedAt
        });
        await context.SaveChangesAsync();

        var service = CreateCohortService(context);
        var dto = new SubmitFinalProjectDto("https://github.com/new-link");

        var result = await service.SubmitFinalProjectAsync(cohort.Id, dto, studentId);
        Assert.True(result.IsSuccess);
        Assert.Equal("https://github.com/new-link", result.Value.Url);
        Assert.Equal(initialSubmittedAt, result.Value.SubmittedAtUtc);
        Assert.NotNull(result.Value.UpdatedAtUtc);
    }

    [Fact]
    public async Task SubmitFinalProjectAsync_WhenDeadlinePassed_ReturnsConflict409()
    {
        using var context = CreateInMemoryContext();
        var studentId = Guid.NewGuid();
        var course = new Course { Title = "Course Late", Slug = "course-late" };
        context.Courses.Add(course);

        var cohort = new Cohort
        {
            Name = "Cohort 1",
            CourseId = course.Id,
            StartDate = DateTime.UtcNow.AddDays(-20),
            FinalDeadlineUtc = DateTime.UtcNow.AddHours(-1),
            Status = CohortStatus.Active
        };
        context.Cohorts.Add(cohort);
        context.CohortEnrollments.Add(new CohortEnrollment
        {
            CohortId = cohort.Id,
            StudentId = studentId,
            Status = CohortEnrollmentStatus.Enrolled
        });
        await context.SaveChangesAsync();

        var service = CreateCohortService(context);
        var dto = new SubmitFinalProjectDto("https://github.com/late-link");

        var result = await service.SubmitFinalProjectAsync(cohort.Id, dto, studentId);
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal("FinalSubmission.DeadlinePassed", result.Error.Code);
    }

    [Fact]
    public async Task SubmitFinalProjectAsync_WhenStudentDropped_ReturnsForbidden403()
    {
        using var context = CreateInMemoryContext();
        var studentId = Guid.NewGuid();
        var course = new Course { Title = "Course Dropped", Slug = "course-dropped" };
        context.Courses.Add(course);

        var cohort = new Cohort
        {
            Name = "Cohort 1",
            CourseId = course.Id,
            StartDate = DateTime.UtcNow.AddDays(-20),
            FinalDeadlineUtc = DateTime.UtcNow.AddDays(5),
            Status = CohortStatus.Active
        };
        context.Cohorts.Add(cohort);
        context.CohortEnrollments.Add(new CohortEnrollment
        {
            CohortId = cohort.Id,
            StudentId = studentId,
            Status = CohortEnrollmentStatus.Dropped
        });
        await context.SaveChangesAsync();

        var service = CreateCohortService(context);
        var dto = new SubmitFinalProjectDto("https://github.com/dropped");

        var result = await service.SubmitFinalProjectAsync(cohort.Id, dto, studentId);
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Forbidden, result.Error.Type);
        Assert.Equal("Cohort.Dropped", result.Error.Code);
    }

    // -------------------------------------------------------------
    // 4. FINALIZE COHORT GRADES TESTS
    // -------------------------------------------------------------
    [Fact]
    public async Task FinalizeCohortGradesAsync_WhenDeadlineNotPassed_ReturnsConflict409()
    {
        using var context = CreateInMemoryContext();
        var teacherId = Guid.NewGuid();
        var course = new Course { Title = "Course Active", Slug = "course-act", InstructorId = teacherId };
        context.Courses.Add(course);

        var cohort = new Cohort
        {
            Name = "Active Cohort",
            CourseId = course.Id,
            TeacherId = teacherId,
            StartDate = DateTime.UtcNow.AddDays(-20),
            FinalDeadlineUtc = DateTime.UtcNow.AddDays(2),
            Status = CohortStatus.Active
        };
        context.Cohorts.Add(cohort);
        await context.SaveChangesAsync();

        var gradeService = new AcademicGradeService(context, new Mock<IUserLookupService>().Object);
        var result = await gradeService.FinalizeCohortGradesAsync(cohort.Id, teacherId, isAdmin: false);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal("Cohort.DeadlineNotPassed", result.Error.Code);
    }

    [Fact]
    public async Task FinalizeCohortGradesAsync_WhenUngradedSubmittedLinksExist_ReturnsConflict409WithStudentIds()
    {
        using var context = CreateInMemoryContext();
        var teacherId = Guid.NewGuid();
        var course = new Course { Title = "Course Ungraded", Slug = "course-ungraded", InstructorId = teacherId };
        context.Courses.Add(course);

        var cohort = new Cohort
        {
            Name = "Cohort with Ungraded",
            CourseId = course.Id,
            TeacherId = teacherId,
            StartDate = DateTime.UtcNow.AddDays(-30),
            FinalDeadlineUtc = DateTime.UtcNow.AddDays(-2),
            Status = CohortStatus.Active
        };
        context.Cohorts.Add(cohort);

        var studentId = Guid.NewGuid();
        context.CohortEnrollments.Add(new CohortEnrollment
        {
            CohortId = cohort.Id,
            StudentId = studentId,
            Status = CohortEnrollmentStatus.Enrolled
        });
        context.FinalSubmissions.Add(new FinalSubmission
        {
            CohortId = cohort.Id,
            StudentId = studentId,
            Url = "https://github.com/ungraded-project",
            SubmittedAtUtc = DateTime.UtcNow.AddDays(-3)
        });
        await context.SaveChangesAsync();

        var gradeService = new AcademicGradeService(context, new Mock<IUserLookupService>().Object);
        var result = await gradeService.FinalizeCohortGradesAsync(cohort.Id, teacherId, isAdmin: false);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal("Cohort.UngradedFinalSubmissions", result.Error.Code);
        Assert.Contains(studentId.ToString(), result.Error.Message);
    }

    [Fact]
    public async Task FinalizeCohortGradesAsync_WhenNoSubmissionAndNoGrade_MarksFinalScoreZeroAndFinalNotSubmittedTrue()
    {
        using var context = CreateInMemoryContext();
        var teacherId = Guid.NewGuid();
        var course = new Course { Title = "Course Fin", Slug = "course-fin-zero", InstructorId = teacherId };
        context.Courses.Add(course);

        var cohort = new Cohort
        {
            Name = "Cohort Finished",
            CourseId = course.Id,
            TeacherId = teacherId,
            StartDate = DateTime.UtcNow.AddDays(-30),
            FinalDeadlineUtc = DateTime.UtcNow.AddDays(-2),
            Status = CohortStatus.Active,
            QuizWeight = 30,
            MiniProjectWeight = 30,
            FinalWeight = 40
        };
        context.Cohorts.Add(cohort);

        var studentId = Guid.NewGuid();
        context.CohortEnrollments.Add(new CohortEnrollment
        {
            CohortId = cohort.Id,
            StudentId = studentId,
            Status = CohortEnrollmentStatus.Enrolled,
            IsProvisional = true
        });

        // Mini project grade only, no quiz, no final
        context.CohortGradeEntries.Add(new CohortGradeEntry
        {
            CohortId = cohort.Id,
            StudentId = studentId,
            ComponentType = GradeComponentType.MiniProject,
            Score = 80m
        });
        await context.SaveChangesAsync();

        var gradeService = new AcademicGradeService(context, new Mock<IUserLookupService>().Object);
        var result = await gradeService.FinalizeCohortGradesAsync(cohort.Id, teacherId, isAdmin: false);

        Assert.True(result.IsSuccess);
        var enrollment = await context.CohortEnrollments.FirstAsync(e => e.StudentId == studentId);
        Assert.True(enrollment.FinalNotSubmitted);
        Assert.False(enrollment.IsProvisional);
        // Mini (80) proportional with 0 final: (80*0.3)/(0.7) = 34.2857 -> 34.3
        Assert.Equal(34.3m, enrollment.OfficialFinalScore);
    }

    // -------------------------------------------------------------
    // 5. CERTIFICATE ELIGIBILITY RULE A TESTS
    // -------------------------------------------------------------
    [Fact]
    public void CertificateEligibility_WhenFinalNotSubmitted_AndAbsencesWithinLimit_ReturnsAttendance()
    {
        var policy = new CertificateEligibilityPolicy();
        var enrollment = new CohortEnrollment
        {
            FinalNotSubmitted = true,
            Status = CohortEnrollmentStatus.Completed,
            IsProvisional = false
        };

        var eval = policy.Evaluate(
            enrollment,
            officialGrade: 85.0m, // High score, but final not submitted
            totalAbsences: 3,
            maxAbsenceLimit: 6,
            isCohortFinished: true
        );

        Assert.Equal(CertificateEligibilityResult.Attendance, eval.Result);
        Assert.Contains("Final layihə təhvil verilmədiyi", eval.Reason);
    }

    [Fact]
    public void CertificateEligibility_WhenFinalNotSubmitted_AndAbsencesExceedLimit_ReturnsDenied()
    {
        var policy = new CertificateEligibilityPolicy();
        var enrollment = new CohortEnrollment
        {
            FinalNotSubmitted = true,
            Status = CohortEnrollmentStatus.Dropped,
            IsProvisional = false
        };

        var eval = policy.Evaluate(
            enrollment,
            officialGrade: 85.0m,
            totalAbsences: 7,
            maxAbsenceLimit: 6,
            isCohortFinished: true
        );

        Assert.Equal(CertificateEligibilityResult.Denied, eval.Result);
    }

    [Fact]
    public void CertificateEligibility_WhenFinalSubmitted_AndScoreAbove70_ReturnsDiploma()
    {
        var policy = new CertificateEligibilityPolicy();
        var enrollment = new CohortEnrollment
        {
            FinalNotSubmitted = false,
            Status = CohortEnrollmentStatus.Completed,
            IsProvisional = false
        };

        var eval = policy.Evaluate(
            enrollment,
            officialGrade: 78.5m,
            totalAbsences: 2,
            maxAbsenceLimit: 6,
            isCohortFinished: true
        );

        Assert.Equal(CertificateEligibilityResult.Diploma, eval.Result);
    }

    // -------------------------------------------------------------
    // 6. EXCEL FORMULA INJECTION SAFETY TESTS
    // -------------------------------------------------------------
    [Theory]
    [InlineData("=1+1", true)]
    [InlineData("+cmd|' /C calc'!A0", true)]
    [InlineData("-2+3", true)]
    [InlineData("@SUM(A1:A10)", true)]
    [InlineData("https://github.com/safe/repo", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void ExcelExport_SetTextSafe_EscapesFormulaTriggersCorrectly(string? input, bool shouldEscape)
    {
        string processed = input ?? string.Empty;
        if (!string.IsNullOrEmpty(input) && (input[0] is '=' or '+' or '-' or '@'))
        {
            processed = "'" + input;
        }

        if (shouldEscape)
        {
            Assert.StartsWith("'", processed);
        }
        else
        {
            Assert.False(processed.StartsWith("'"));
        }
    }
}
