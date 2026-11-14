using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Moq;
using Pragma.Application.Common;
using Pragma.Application.Contracts;
using Pragma.Application.DTOs.Cohorts;
using Pragma.Application.Services;
using Pragma.Application.Validators.Cohorts;
using Pragma.Domain.Entities;
using Pragma.Domain.Enums;
using Pragma.Infrastructure.Persistence;
using Xunit;

namespace Pragma.UnitTests;

public class AcademicCoreTests
{
    private static AppDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private class FakeTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow;
        public FakeTimeProvider(DateTimeOffset utcNow) => _utcNow = utcNow;
        public override DateTimeOffset GetUtcNow() => _utcNow;
        public void SetUtcNow(DateTimeOffset utcNow) => _utcNow = utcNow;
    }

    #region 1. Sertifikat Qərar Siyasəti və Sərhəd Testləri (ICertificateEligibilityPolicy)

    [Theory]
    [InlineData(70.0, 0, CertificateEligibilityResult.Diploma)]
    [InlineData(85.5, 3, CertificateEligibilityResult.Diploma)]
    [InlineData(100.0, 6, CertificateEligibilityResult.Diploma)]
    public void CertificateEligibilityPolicy_Score70OrAbove_WithinAbsenceLimit_ReturnsDiploma(
        double scoreDouble, int absences, CertificateEligibilityResult expected)
    {
        var policy = new CertificateEligibilityPolicy();
        var score = (decimal)scoreDouble;
        var enrollment = new CohortEnrollment
        {
            Status = CohortEnrollmentStatus.Completed,
            IsProvisional = false
        };

        var eval = policy.Evaluate(
            enrollment: enrollment,
            officialGrade: score,
            totalAbsences: absences,
            maxAbsenceLimit: 6,
            isCohortFinished: true);

        Assert.Equal(expected, eval.Result);
    }

    [Fact]
    public void CertificateEligibilityPolicy_Score69Point95_RoundsTo70_ReturnsDiploma()
    {
        // Biznes qaydası: 1 onluq yuvarlaqlaşdırma MidpointRounding.AwayFromZero
        // 69.95 -> 70.0 -> Diplom
        var policy = new CertificateEligibilityPolicy();
        var rawScore = 69.95m;
        var roundedScore = Math.Round(rawScore, 1, MidpointRounding.AwayFromZero);
        Assert.Equal(70.0m, roundedScore);

        var enrollment = new CohortEnrollment
        {
            Status = CohortEnrollmentStatus.Completed,
            IsProvisional = false
        };

        var eval = policy.Evaluate(
            enrollment: enrollment,
            officialGrade: roundedScore,
            totalAbsences: 4,
            maxAbsenceLimit: 6,
            isCohortFinished: true);

        Assert.Equal(CertificateEligibilityResult.Diploma, eval.Result);
    }

    [Fact]
    public void CertificateEligibilityPolicy_Score69Point94_RoundsTo69Point9_ReturnsAttendance()
    {
        // 69.94 -> 69.9 -> İştirak sertifikatı (Attendance)
        var policy = new CertificateEligibilityPolicy();
        var rawScore = 69.94m;
        var roundedScore = Math.Round(rawScore, 1, MidpointRounding.AwayFromZero);
        Assert.Equal(69.9m, roundedScore);

        var enrollment = new CohortEnrollment
        {
            Status = CohortEnrollmentStatus.Completed,
            IsProvisional = false
        };

        var eval = policy.Evaluate(
            enrollment: enrollment,
            officialGrade: roundedScore,
            totalAbsences: 4,
            maxAbsenceLimit: 6,
            isCohortFinished: true);

        Assert.Equal(CertificateEligibilityResult.Attendance, eval.Result);
    }

    [Theory]
    [InlineData(69.9, 0, CertificateEligibilityResult.Attendance)]
    [InlineData(50.0, 5, CertificateEligibilityResult.Attendance)]
    [InlineData(0.0, 6, CertificateEligibilityResult.Attendance)]
    public void CertificateEligibilityPolicy_ScoreBelow70_WithinAbsenceLimit_ReturnsAttendance(
        double scoreDouble, int absences, CertificateEligibilityResult expected)
    {
        var policy = new CertificateEligibilityPolicy();
        var score = (decimal)scoreDouble;
        var enrollment = new CohortEnrollment
        {
            Status = CohortEnrollmentStatus.Completed,
            IsProvisional = false
        };

        var eval = policy.Evaluate(
            enrollment: enrollment,
            officialGrade: score,
            totalAbsences: absences,
            maxAbsenceLimit: 6,
            isCohortFinished: true);

        Assert.Equal(expected, eval.Result);
    }

    [Theory]
    [InlineData(7, 95.0)] // Limit 6, qayıb 7 -> Denied
    [InlineData(8, 70.0)]
    [InlineData(10, 100.0)]
    public void CertificateEligibilityPolicy_AbsencesExceedLimit_ReturnsDenied_EvenWithHighScore(
        int absences, double scoreDouble)
    {
        var policy = new CertificateEligibilityPolicy();
        var score = (decimal)scoreDouble;
        var enrollment = new CohortEnrollment
        {
            Status = CohortEnrollmentStatus.Completed,
            IsProvisional = false
        };

        var eval = policy.Evaluate(
            enrollment: enrollment,
            officialGrade: score,
            totalAbsences: absences,
            maxAbsenceLimit: 6,
            isCohortFinished: true);

        Assert.Equal(CertificateEligibilityResult.Denied, eval.Result);
    }

    [Fact]
    public void CertificateEligibilityPolicy_StudentDropped_ReturnsDenied()
    {
        var policy = new CertificateEligibilityPolicy();
        var enrollment = new CohortEnrollment
        {
            Status = CohortEnrollmentStatus.Dropped,
            IsProvisional = false
        };

        var eval = policy.Evaluate(
            enrollment: enrollment,
            officialGrade: 85.0m,
            totalAbsences: 2,
            maxAbsenceLimit: 6,
            isCohortFinished: true);

        Assert.Equal(CertificateEligibilityResult.Denied, eval.Result);
    }

    [Theory]
    [InlineData(false, false)] // Kohort Finished deyil -> Pending
    [InlineData(true, true)]   // Kohort Finished amma bal provisional-dır -> Pending
    public void CertificateEligibilityPolicy_PendingScenarios_ReturnsPending(
        bool isCohortFinished, bool isProvisional)
    {
        var policy = new CertificateEligibilityPolicy();
        var enrollment = new CohortEnrollment
        {
            Status = CohortEnrollmentStatus.Enrolled,
            IsProvisional = isProvisional
        };

        var eval = policy.Evaluate(
            enrollment: enrollment,
            officialGrade: 85.0m,
            totalAbsences: 2,
            maxAbsenceLimit: 6,
            isCohortFinished: isCohortFinished);

        Assert.Equal(CertificateEligibilityResult.Pending, eval.Result);
    }

    #endregion

    #region 2. Davamiyyət Düsturu və Qayıb Hesablamaları (IAttendanceService)

    [Theory]
    [InlineData(0, 0, 3, 0)] // Absent=0, Late=0 -> 0
    [InlineData(0, 2, 3, 0)] // Absent=0, Late=2 -> 0
    [InlineData(0, 3, 3, 1)] // Absent=0, Late=3 -> 1
    [InlineData(0, 5, 3, 1)] // Absent=0, Late=5 -> 1
    [InlineData(0, 6, 3, 2)] // Absent=0, Late=6 -> 2
    [InlineData(2, 4, 3, 3)] // Absent=2, Late=4 -> 2 + 1 = 3
    [InlineData(5, 3, 3, 6)] // Absent=5, Late=3 -> 5 + 1 = 6 (limit həddi)
    [InlineData(6, 1, 3, 6)] // Absent=6, Late=1 -> 6
    [InlineData(6, 3, 3, 7)] // Absent=6, Late=3 -> 7 (limiti aşır!)
    public void AttendanceFormula_CalculatesEffectiveAbsences_Accurately(
        int absent, int late, int ratio, int expectedAbsences)
    {
        var absences = absent + (late / ratio);
        Assert.Equal(expectedAbsences, absences);
    }

    [Fact]
    public async Task AttendanceService_TeacherRecordingWithin7Days_Succeeds()
    {
        using var context = CreateInMemoryContext();
        var teacherId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        var course = new Course
        {
            Title = "C# & .NET 10 Onion Architecture",
            Slug = "csharp-net10",
            DeliveryMode = DeliveryMode.Cohort
        };
        context.Courses.Add(course);

        // Bazar ertəsi dərsi
        var cohort = new Cohort
        {
            CourseId = course.Id,
            TeacherId = teacherId,
            Name = "Qrup .NET-01",
            Status = CohortStatus.Active,
            StartDate = DateTime.UtcNow.AddMonths(-1),
            EndDate = DateTime.UtcNow.AddMonths(2),
            MaxAbsenceLimit = 6,
            LateToAbsenceRatio = 3
        };
        cohort.Schedules.Add(new CohortSchedule
        {
            CohortId = cohort.Id,
            DayOfWeek = DayOfWeek.Monday,
            StartTime = new TimeSpan(19, 0, 0),
            EndTime = new TimeSpan(21, 0, 0)
        });
        cohort.Enrollments.Add(new CohortEnrollment
        {
            CohortId = cohort.Id,
            StudentId = studentId,
            Status = CohortEnrollmentStatus.Enrolled
        });
        context.Cohorts.Add(cohort);
        await context.SaveChangesAsync();

        // 2026-11-09 Bazar ertəsi dərsi
        var lessonDate = new DateOnly(2026, 11, 9);
        // Müəllim 2026-11-13 tarixində (4 gün sonra) daxil edir
        var fakeNow = new DateTimeOffset(2026, 11, 13, 10, 0, 0, TimeSpan.FromHours(4));
        var timeProvider = new FakeTimeProvider(fakeNow);

        var auditMock = new Mock<IAuditLogService>();
        var userLookupMock = new Mock<IUserLookupService>();
        var attendanceService = new AttendanceService(context, userLookupMock.Object, auditMock.Object, timeProvider);

        var dto = new BulkAttendanceDto(
            lessonDate,
            new List<AttendanceEntryDto>
            {
                new(studentId, AttendanceStatus.Present)
            });

        var result = await attendanceService.RecordBulkAttendanceAsync(
            cohort.Id, dto, teacherId, isAdmin: false);

        Assert.True(result.IsSuccess);
        var record = await context.AttendanceRecords.FirstOrDefaultAsync(a => a.CohortId == cohort.Id && a.StudentId == studentId);
        Assert.NotNull(record);
        Assert.Equal(AttendanceStatus.Present, record.Status);
    }

    [Fact]
    public async Task AttendanceService_TeacherRecordingAfter7Days_ReturnsForbidden()
    {
        using var context = CreateInMemoryContext();
        var teacherId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        var course = new Course
        {
            Title = "Full-Stack Web",
            Slug = "full-stack-web",
            DeliveryMode = DeliveryMode.Cohort
        };
        context.Courses.Add(course);

        var cohort = new Cohort
        {
            CourseId = course.Id,
            TeacherId = teacherId,
            Name = "Qrup Web-02",
            Status = CohortStatus.Active,
            StartDate = DateTime.UtcNow.AddMonths(-2),
            EndDate = DateTime.UtcNow.AddMonths(1),
            MaxAbsenceLimit = 6,
            LateToAbsenceRatio = 3
        };
        cohort.Schedules.Add(new CohortSchedule
        {
            CohortId = cohort.Id,
            DayOfWeek = DayOfWeek.Monday,
            StartTime = new TimeSpan(19, 0, 0),
            EndTime = new TimeSpan(21, 0, 0)
        });
        cohort.Enrollments.Add(new CohortEnrollment
        {
            CohortId = cohort.Id,
            StudentId = studentId,
            Status = CohortEnrollmentStatus.Enrolled
        });
        context.Cohorts.Add(cohort);
        await context.SaveChangesAsync();

        // 2026-11-02 Bazar ertəsi dərsi
        var lessonDate = new DateOnly(2026, 11, 2);
        // Müəllim 2026-11-11 tarixində (9 gün sonra > 7 gün) dəyişmək istəyir
        var fakeNow = new DateTimeOffset(2026, 11, 11, 10, 0, 0, TimeSpan.FromHours(4));
        var timeProvider = new FakeTimeProvider(fakeNow);

        var auditMock = new Mock<IAuditLogService>();
        var userLookupMock = new Mock<IUserLookupService>();
        var attendanceService = new AttendanceService(context, userLookupMock.Object, auditMock.Object, timeProvider);

        var dto = new BulkAttendanceDto(
            lessonDate,
            new List<AttendanceEntryDto>
            {
                new(studentId, AttendanceStatus.Absent)
            });

        var result = await attendanceService.RecordBulkAttendanceAsync(
            cohort.Id, dto, teacherId, isAdmin: false);

        Assert.False(result.IsSuccess);
        Assert.Equal("Attendance.WindowExpired", result.Error.Code);
    }

    [Fact]
    public async Task AttendanceService_AdminCanOverrideAfter7Days_AndLogsAudit()
    {
        using var context = CreateInMemoryContext();
        var adminId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        var course = new Course
        {
            Title = "Backend Architecture",
            Slug = "backend-arch",
            DeliveryMode = DeliveryMode.Cohort
        };
        context.Courses.Add(course);

        var cohort = new Cohort
        {
            CourseId = course.Id,
            TeacherId = teacherId,
            Name = "Qrup BE-01",
            Status = CohortStatus.Active,
            StartDate = DateTime.UtcNow.AddMonths(-2),
            EndDate = DateTime.UtcNow.AddMonths(1),
            MaxAbsenceLimit = 6,
            LateToAbsenceRatio = 3
        };
        cohort.Schedules.Add(new CohortSchedule
        {
            CohortId = cohort.Id,
            DayOfWeek = DayOfWeek.Monday,
            StartTime = new TimeSpan(19, 0, 0),
            EndTime = new TimeSpan(21, 0, 0)
        });
        cohort.Enrollments.Add(new CohortEnrollment
        {
            CohortId = cohort.Id,
            StudentId = studentId,
            Status = CohortEnrollmentStatus.Enrolled
        });
        context.Cohorts.Add(cohort);
        await context.SaveChangesAsync();

        // 2026-11-02 Bazar ertəsi dərsi (9 gün əvvəl)
        var lessonDate = new DateOnly(2026, 11, 2);
        var fakeNow = new DateTimeOffset(2026, 11, 11, 10, 0, 0, TimeSpan.FromHours(4));
        var timeProvider = new FakeTimeProvider(fakeNow);

        var auditMock = new Mock<IAuditLogService>();
        var userLookupMock = new Mock<IUserLookupService>();
        var attendanceService = new AttendanceService(context, userLookupMock.Object, auditMock.Object, timeProvider);

        var dto = new BulkAttendanceDto(
            lessonDate,
            new List<AttendanceEntryDto>
            {
                new(studentId, AttendanceStatus.Excused)
            });

        var result = await attendanceService.RecordBulkAttendanceAsync(
            cohort.Id, dto, adminId, isAdmin: true);

        Assert.True(result.IsSuccess);
        auditMock.Verify(a => a.LogAsync(
            adminId,
            "Admin",
            "AdminAttendanceOverride",
            nameof(Cohort),
            cohort.Id.ToString(),
            It.Is<string>(d => d.Contains("Admin 7 günlük müəllim pəncərəsini keçmiş tarix")),
            null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AttendanceService_AbsencesExceedingLimit_SetsStudentToDropped()
    {
        using var context = CreateInMemoryContext();
        var teacherId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        var course = new Course { Title = "DevOps", Slug = "devops", DeliveryMode = DeliveryMode.Cohort };
        context.Courses.Add(course);

        var cohort = new Cohort
        {
            CourseId = course.Id,
            TeacherId = teacherId,
            Name = "Qrup DO-01",
            Status = CohortStatus.Active,
            StartDate = DateTime.UtcNow.AddMonths(-2),
            EndDate = DateTime.UtcNow.AddMonths(1),
            MaxAbsenceLimit = 6,
            LateToAbsenceRatio = 3
        };
        cohort.Schedules.Add(new CohortSchedule
        {
            CohortId = cohort.Id,
            DayOfWeek = DayOfWeek.Wednesday,
            StartTime = new TimeSpan(19, 0, 0),
            EndTime = new TimeSpan(21, 0, 0)
        });
        var enrollment = new CohortEnrollment
        {
            CohortId = cohort.Id,
            StudentId = studentId,
            Status = CohortEnrollmentStatus.Enrolled
        };
        cohort.Enrollments.Add(enrollment);
        context.Cohorts.Add(cohort);

        // Artıq 6 dərs qayıbı var
        for (int i = 1; i <= 6; i++)
        {
            context.AttendanceRecords.Add(new AttendanceRecord
            {
                CohortId = cohort.Id,
                StudentId = studentId,
                Date = new DateOnly(2026, 9, i * 4),
                Status = AttendanceStatus.Absent,
                RecordedByUserId = teacherId
            });
        }
        await context.SaveChangesAsync();

        // 7-ci qayıb daxil edilir (2026-11-04 Çərşənbə)
        var lessonDate = new DateOnly(2026, 11, 4);
        var fakeNow = new DateTimeOffset(2026, 11, 5, 10, 0, 0, TimeSpan.FromHours(4));
        var timeProvider = new FakeTimeProvider(fakeNow);

        var auditMock = new Mock<IAuditLogService>();
        var userLookupMock = new Mock<IUserLookupService>();
        var attendanceService = new AttendanceService(context, userLookupMock.Object, auditMock.Object, timeProvider);

        var dto = new BulkAttendanceDto(
            lessonDate,
            new List<AttendanceEntryDto>
            {
                new(studentId, AttendanceStatus.Absent)
            });

        var result = await attendanceService.RecordBulkAttendanceAsync(
            cohort.Id, dto, teacherId, isAdmin: false);

        Assert.True(result.IsSuccess);
        var updatedEnrollment = await context.CohortEnrollments.FirstOrDefaultAsync(e => e.CohortId == cohort.Id && e.StudentId == studentId);
        Assert.NotNull(updatedEnrollment);
        Assert.Equal(CohortEnrollmentStatus.Dropped, updatedEnrollment.Status);
        Assert.True(updatedEnrollment.HasAbsenceWarning);
        Assert.NotNull(updatedEnrollment.DroppedReason);
    }

    [Fact]
    public async Task AttendanceService_AdminCorrection_RestoresStudentFromDroppedToEnrolled()
    {
        using var context = CreateInMemoryContext();
        var adminId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        var course = new Course { Title = "Cloud Computing", Slug = "cloud-comp", DeliveryMode = DeliveryMode.Cohort };
        context.Courses.Add(course);

        var cohort = new Cohort
        {
            CourseId = course.Id,
            TeacherId = teacherId,
            Name = "Qrup CC-01",
            Status = CohortStatus.Active,
            StartDate = DateTime.UtcNow.AddMonths(-2),
            EndDate = DateTime.UtcNow.AddMonths(1),
            MaxAbsenceLimit = 6,
            LateToAbsenceRatio = 3
        };
        cohort.Schedules.Add(new CohortSchedule
        {
            CohortId = cohort.Id,
            DayOfWeek = DayOfWeek.Monday,
            StartTime = new TimeSpan(19, 0, 0),
            EndTime = new TimeSpan(21, 0, 0)
        });
        var enrollment = new CohortEnrollment
        {
            CohortId = cohort.Id,
            StudentId = studentId,
            Status = CohortEnrollmentStatus.Dropped, // Hal-hazırda Dropped
            DroppedReason = "Maksimum qayıb limiti (6) aşıldı."
        };
        cohort.Enrollments.Add(enrollment);
        context.Cohorts.Add(cohort);

        // 7 ədəd qayıb mövcuddur
        var dates = new List<DateOnly>
        {
            new(2026, 9, 7), new(2026, 9, 14), new(2026, 9, 21), new(2026, 9, 28),
            new(2026, 10, 5), new(2026, 10, 12), new(2026, 10, 19)
        };
        foreach (var d in dates)
        {
            context.AttendanceRecords.Add(new AttendanceRecord
            {
                CohortId = cohort.Id,
                StudentId = studentId,
                Date = d,
                Status = AttendanceStatus.Absent,
                RecordedByUserId = teacherId
            });
        }
        await context.SaveChangesAsync();

        // Admin 2026-10-19 tarixini Absent-dən Excused-ə (üzrlü) dəyişir (qayıb 7-dən 6-ya düşür)
        var correctionDate = new DateOnly(2026, 10, 19);
        var fakeNow = new DateTimeOffset(2026, 10, 20, 10, 0, 0, TimeSpan.FromHours(4));
        var timeProvider = new FakeTimeProvider(fakeNow);

        var auditMock = new Mock<IAuditLogService>();
        var userLookupMock = new Mock<IUserLookupService>();
        var attendanceService = new AttendanceService(context, userLookupMock.Object, auditMock.Object, timeProvider);

        var dto = new BulkAttendanceDto(
            correctionDate,
            new List<AttendanceEntryDto>
            {
                new(studentId, AttendanceStatus.Excused)
            });

        var result = await attendanceService.RecordBulkAttendanceAsync(
            cohort.Id, dto, adminId, isAdmin: true);

        Assert.True(result.IsSuccess);
        var updatedEnrollment = await context.CohortEnrollments.FirstOrDefaultAsync(e => e.CohortId == cohort.Id && e.StudentId == studentId);
        Assert.NotNull(updatedEnrollment);
        // Biznes qaydası: Admin düzəlişindən sonra qayıb <= 6 olduqda status Dropped-dan Enrolled-ə qayıdır!
        Assert.Equal(CohortEnrollmentStatus.Enrolled, updatedEnrollment.Status);
        Assert.Null(updatedEnrollment.DroppedReason);
    }

    #endregion

    #region 3. Akademik Bal Hesablanması (IAcademicGradeService)

    [Fact]
    public async Task AcademicGradeService_CalculatesProvisionalAndFinalGrades_Accurately()
    {
        using var context = CreateInMemoryContext();
        var teacherId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        var course = new Course
        {
            Title = "AI Engineering",
            Slug = "ai-eng",
            DeliveryMode = DeliveryMode.Cohort
        };
        var module = new Module { Title = "Modul 1", Course = course };
        var lesson1 = new Lesson { Title = "Dərs 1", Module = module };
        var lesson2 = new Lesson { Title = "Dərs 2", Module = module };

        var quiz1 = new Quiz { Title = "Quiz 1", Lesson = lesson1 };
        var quiz2 = new Quiz { Title = "Quiz 2", Lesson = lesson2 };
        lesson1.Quiz = quiz1;
        lesson2.Quiz = quiz2;
        module.Lessons.Add(lesson1);
        module.Lessons.Add(lesson2);
        course.Modules.Add(module);
        context.Courses.Add(course);

        var cohort = new Cohort
        {
            CourseId = course.Id,
            TeacherId = teacherId,
            Name = "Qrup AI-01",
            Status = CohortStatus.Active,
            StartDate = DateTime.UtcNow.AddMonths(-1),
            EndDate = DateTime.UtcNow.AddMonths(2),
            QuizWeight = 30,
            MiniProjectWeight = 30,
            FinalWeight = 40,
            MaxAbsenceLimit = 6,
            LateToAbsenceRatio = 3
        };
        cohort.Enrollments.Add(new CohortEnrollment
        {
            CohortId = cohort.Id,
            StudentId = studentId,
            Status = CohortEnrollmentStatus.Enrolled
        });
        context.Cohorts.Add(cohort);

        // Tələbə Quiz 1 üzrə 2 cəhd edib: 60 və 80 -> Ən yüksək cəhd 80 olmalıdır
        context.QuizAttempts.Add(new QuizAttempt { QuizId = quiz1.Id, StudentId = studentId, ScorePercentage = 60, Status = QuizAttemptStatus.Completed });
        context.QuizAttempts.Add(new QuizAttempt { QuizId = quiz1.Id, StudentId = studentId, ScorePercentage = 80, Status = QuizAttemptStatus.Completed });

        // Tələbə Quiz 2 üzrə 1 cəhd edib: 90
        context.QuizAttempts.Add(new QuizAttempt { QuizId = quiz2.Id, StudentId = studentId, ScorePercentage = 90, Status = QuizAttemptStatus.Completed });

        // Quiz ortalaması = (80 + 90) / 2 = 85.0
        await context.SaveChangesAsync();

        var userLookupMock = new Mock<IUserLookupService>();
        userLookupMock.Setup(u => u.GetUserByIdAsync(studentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserBasicInfoDto(studentId, "Test Student", "student@pragma.az"));

        var gradeService = new AcademicGradeService(context, userLookupMock.Object);

        // 1-ci Mərhələ: Yalnız Kvizlər var (Mini layihə və Final yoxdur) -> IsProvisional = true
        var gradeResult1 = await gradeService.CalculateStudentGradeAsync(cohort.Id, studentId);
        Assert.True(gradeResult1.IsSuccess);
        var studentGrade1 = gradeResult1.Value;
        Assert.Equal(85.0m, studentGrade1.QuizAverage);
        Assert.Null(studentGrade1.MiniProjectScore);
        Assert.Null(studentGrade1.FinalScore);
        Assert.True(studentGrade1.IsProvisional);
        Assert.Equal(85.0m, studentGrade1.OfficialScore);

        // 2-ci Mərhələ: Müəllim Mini Layihə balını daxil edir: 75.0
        await gradeService.SubmitComponentGradeAsync(
            cohort.Id,
            studentId,
            GradeComponentType.MiniProject,
            75.0m,
            teacherId,
            isAdmin: false);

        // İndi Kviz (30%) və Mini Layihə (30%) var -> Mütənasib bölünür (50% - 50%), Final hələ yoxdur -> IsProvisional = true
        // (85.0 * 30 + 75.0 * 30) / 60 = 80.0
        var gradeResult2 = await gradeService.CalculateStudentGradeAsync(cohort.Id, studentId);
        var studentGrade2 = gradeResult2.Value;
        Assert.True(studentGrade2.IsProvisional);
        Assert.Equal(80.0m, studentGrade2.OfficialScore);

        // 3-cü Mərhələ: Müəllim Final Layihə balını daxil edir: 95.0
        await gradeService.SubmitComponentGradeAsync(
            cohort.Id,
            studentId,
            GradeComponentType.Final,
            95.0m,
            teacherId,
            isAdmin: false);

        // İndi bütün 3 komponent var: (85.0 * 0.3) + (75.0 * 0.3) + (95.0 * 0.4) = 25.5 + 22.5 + 38.0 = 86.0
        // IsProvisional = false
        var gradeResult3 = await gradeService.CalculateStudentGradeAsync(cohort.Id, studentId);
        var studentGrade3 = gradeResult3.Value;
        Assert.False(studentGrade3.IsProvisional);
        Assert.Equal(86.0m, studentGrade3.OfficialScore);
    }

    #endregion

    #region 4. Validasiya Qaydaları (CohortValidators)

    [Fact]
    public void CreateCohortValidator_WeightsSumNot100_FailsValidation()
    {
        var validator = new CreateCohortValidator();
        var dto = new CreateCohortDto(
            Name: "Qrup A",
            CourseId: Guid.NewGuid(),
            TeacherId: Guid.NewGuid(),
            StartDate: DateTime.UtcNow.AddDays(7),
            EndDate: DateTime.UtcNow.AddMonths(3),
            TotalHours: 80,
            QuizWeight: 40,
            MiniProjectWeight: 40,
            FinalWeight: 30, // 40 + 40 + 30 = 110 != 100
            MaxAbsenceLimit: 6,
            LateToAbsenceRatio: 3);

        var result = validator.Validate(dto);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("100%"));
    }

    [Fact]
    public void CreateCohortValidator_WeightsSum100_PassesValidation()
    {
        var validator = new CreateCohortValidator();
        var dto = new CreateCohortDto(
            Name: "Qrup B",
            CourseId: Guid.NewGuid(),
            TeacherId: Guid.NewGuid(),
            StartDate: DateTime.UtcNow.AddDays(7),
            EndDate: DateTime.UtcNow.AddMonths(3),
            TotalHours: 80,
            QuizWeight: 30,
            MiniProjectWeight: 30,
            FinalWeight: 40,
            MaxAbsenceLimit: 6,
            LateToAbsenceRatio: 3);

        var result = validator.Validate(dto);
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task AcademicGradeService_SubmitBulkGrades_UpdatesScoresCorrectly()
    {
        using var context = CreateInMemoryContext();
        var teacherId = Guid.NewGuid();
        var studentId1 = Guid.NewGuid();
        var studentId2 = Guid.NewGuid();

        var course = new Course
        {
            Title = "Backend Course",
            Slug = "be-course",
            DeliveryMode = DeliveryMode.Cohort
        };
        context.Courses.Add(course);

        var cohort = new Cohort
        {
            CourseId = course.Id,
            TeacherId = teacherId,
            Name = "Qrup BE-01",
            Status = CohortStatus.Active,
            StartDate = DateTime.UtcNow.AddMonths(-1),
            QuizWeight = 30,
            MiniProjectWeight = 30,
            FinalWeight = 40,
            MaxAbsenceLimit = 6,
            LateToAbsenceRatio = 3
        };
        cohort.Enrollments.Add(new CohortEnrollment { CohortId = cohort.Id, StudentId = studentId1, Status = CohortEnrollmentStatus.Enrolled });
        cohort.Enrollments.Add(new CohortEnrollment { CohortId = cohort.Id, StudentId = studentId2, Status = CohortEnrollmentStatus.Enrolled });
        context.Cohorts.Add(cohort);
        await context.SaveChangesAsync();

        var userLookupMock = new Mock<IUserLookupService>();
        var service = new AcademicGradeService(context, userLookupMock.Object);

        var bulkDto = new BulkSubmitGradesDto(new[]
        {
            new BulkGradeStudentItemDto(studentId1, MiniProjectScore: 85m, FinalScore: 90m, MiniProjectNotes: "Əla mini layihə", FinalNotes: "Super final"),
            new BulkGradeStudentItemDto(studentId2, MiniProjectScore: 70m, FinalScore: null, MiniProjectNotes: "Yaxşı", FinalNotes: null)
        });

        var result = await service.SubmitBulkGradesAsync(cohort.Id, bulkDto, teacherId, isAdmin: false);

        Assert.True(result.IsSuccess);

        var grades = await context.CohortGradeEntries.Where(g => g.CohortId == cohort.Id).ToListAsync();
        Assert.Equal(3, grades.Count); // student1: mini & final; student2: mini

        var student1Summary = await service.CalculateStudentGradeAsync(cohort.Id, studentId1);
        Assert.True(student1Summary.IsSuccess);
        Assert.Equal(85m, student1Summary.Value.MiniProjectScore);
        Assert.Equal(90m, student1Summary.Value.FinalScore);
        Assert.Equal("Əla mini layihə", student1Summary.Value.MiniProjectNotes);
        Assert.Equal("Super final", student1Summary.Value.FinalNotes);
    }

    [Fact]
    public async Task AcademicGradeService_GetCohortGradesAsync_ComputesExactMultiStudentScores()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var teacherId = Guid.NewGuid();
        var studentA = Guid.NewGuid();
        var studentB = Guid.NewGuid();
        var studentC = Guid.NewGuid();

        var course = new Course
        {
            Title = "FullStack Engineering",
            Slug = "fullstack-eng",
            DeliveryMode = DeliveryMode.Cohort
        };

        var module1 = new Module { Title = "Module 1", OrderIndex = 1 };
        var quiz1 = new Quiz { Title = "Quiz 1", PassingScorePercentage = 70, TimeLimitMinutes = 30 };
        module1.Lessons.Add(new Lesson { Title = "Lesson 1", OrderIndex = 1, Quiz = quiz1 });

        var module2 = new Module { Title = "Module 2", OrderIndex = 2 };
        var quiz2 = new Quiz { Title = "Quiz 2", PassingScorePercentage = 70, TimeLimitMinutes = 30 };
        module2.Lessons.Add(new Lesson { Title = "Lesson 2", OrderIndex = 1, Quiz = quiz2 });

        course.Modules.Add(module1);
        course.Modules.Add(module2);
        context.Courses.Add(course);

        var cohort = new Cohort
        {
            CourseId = course.Id,
            TeacherId = teacherId,
            Name = "FS-2026",
            Status = CohortStatus.Active,
            StartDate = DateTime.UtcNow.AddMonths(-1),
            QuizWeight = 30m,
            MiniProjectWeight = 30m,
            FinalWeight = 40m,
            MaxAbsenceLimit = 6,
            LateToAbsenceRatio = 3
        };

        cohort.Enrollments.Add(new CohortEnrollment { CohortId = cohort.Id, StudentId = studentA, Status = CohortEnrollmentStatus.Enrolled });
        cohort.Enrollments.Add(new CohortEnrollment { CohortId = cohort.Id, StudentId = studentB, Status = CohortEnrollmentStatus.Enrolled });
        cohort.Enrollments.Add(new CohortEnrollment { CohortId = cohort.Id, StudentId = studentC, Status = CohortEnrollmentStatus.Enrolled });
        context.Cohorts.Add(cohort);

        // Student A: Quiz 1 = 80, Quiz 2 = 90 -> Avg = 85.0
        context.QuizAttempts.Add(new QuizAttempt { QuizId = quiz1.Id, StudentId = studentA, ScorePercentage = 80, Status = QuizAttemptStatus.Completed });
        context.QuizAttempts.Add(new QuizAttempt { QuizId = quiz2.Id, StudentId = studentA, ScorePercentage = 90, Status = QuizAttemptStatus.Completed });
        // Student A Grades: Mini = 80, Final = 90 -> FinalScore = 85.5, IsProvisional = false
        context.CohortGradeEntries.Add(new CohortGradeEntry { CohortId = cohort.Id, StudentId = studentA, ComponentType = GradeComponentType.MiniProject, Score = 80m, GradedByTeacherId = teacherId });
        context.CohortGradeEntries.Add(new CohortGradeEntry { CohortId = cohort.Id, StudentId = studentA, ComponentType = GradeComponentType.Final, Score = 90m, GradedByTeacherId = teacherId });

        // Student B: Quiz 1 = 70, Quiz 2 = 80 -> Avg = 75.0
        context.QuizAttempts.Add(new QuizAttempt { QuizId = quiz1.Id, StudentId = studentB, ScorePercentage = 70, Status = QuizAttemptStatus.Completed });
        context.QuizAttempts.Add(new QuizAttempt { QuizId = quiz2.Id, StudentId = studentB, ScorePercentage = 80, Status = QuizAttemptStatus.Completed });
        // Student B Grades: Mini = 70, Final = null -> ProportionalScore = 72.5, IsProvisional = true
        context.CohortGradeEntries.Add(new CohortGradeEntry { CohortId = cohort.Id, StudentId = studentB, ComponentType = GradeComponentType.MiniProject, Score = 70m, GradedByTeacherId = teacherId });

        // Student C: No quizzes -> Mini = 60, Final = null -> ProportionalScore = 60.0, IsProvisional = true
        context.CohortGradeEntries.Add(new CohortGradeEntry { CohortId = cohort.Id, StudentId = studentC, ComponentType = GradeComponentType.MiniProject, Score = 60m, GradedByTeacherId = teacherId });

        await context.SaveChangesAsync();

        var usersDict = new Dictionary<Guid, UserBasicInfoDto>
        {
            [studentA] = new(studentA, "Tələbə A", "a@pragma.az"),
            [studentB] = new(studentB, "Tələbə B", "b@pragma.az"),
            [studentC] = new(studentC, "Tələbə C", "c@pragma.az")
        };

        var userLookupMock = new Mock<IUserLookupService>();
        userLookupMock.Setup(u => u.GetUserByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => usersDict.GetValueOrDefault(id));
        userLookupMock.Setup(u => u.GetUsersByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> ids, CancellationToken _) => ids.Where(usersDict.ContainsKey).ToDictionary(id => id, id => usersDict[id]));

        var service = new AcademicGradeService(context, userLookupMock.Object);

        // Act
        var result = await service.GetCohortGradesAsync(cohort.Id, teacherId, isAdmin: false);

        // Assert
        Assert.True(result.IsSuccess);
        var gradesList = result.Value;
        Assert.Equal(3, gradesList.Count);

        var gradeA = gradesList.First(g => g.StudentId == studentA);
        Assert.Equal("Tələbə A", gradeA.StudentFullName);
        Assert.Equal(85.0m, gradeA.QuizAverage);
        Assert.Equal(2, gradeA.QuizzesCompleted);
        Assert.Equal(2, gradeA.TotalQuizzes);
        Assert.Equal(80.0m, gradeA.MiniProjectScore);
        Assert.Equal(90.0m, gradeA.FinalScore);
        Assert.Equal(85.5m, gradeA.OfficialScore);
        Assert.False(gradeA.IsProvisional);

        var gradeB = gradesList.First(g => g.StudentId == studentB);
        Assert.Equal("Tələbə B", gradeB.StudentFullName);
        Assert.Equal(75.0m, gradeB.QuizAverage);
        Assert.Equal(2, gradeB.QuizzesCompleted);
        Assert.Equal(70.0m, gradeB.MiniProjectScore);
        Assert.Null(gradeB.FinalScore);
        Assert.Equal(72.5m, gradeB.OfficialScore);
        Assert.True(gradeB.IsProvisional);

        var gradeC = gradesList.First(g => g.StudentId == studentC);
        Assert.Equal("Tələbə C", gradeC.StudentFullName);
        Assert.Null(gradeC.QuizAverage);
        Assert.Equal(0, gradeC.QuizzesCompleted);
        Assert.Equal(60.0m, gradeC.MiniProjectScore);
        Assert.Null(gradeC.FinalScore);
        Assert.Equal(60.0m, gradeC.OfficialScore);
        Assert.True(gradeC.IsProvisional);
    }

    [Fact]
    public async Task AcademicGradeService_SubmitBulkGradesAsync_UpdatesEnrollmentsWithCalculatedScores()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var teacherId = Guid.NewGuid();
        var studentA = Guid.NewGuid();
        var studentB = Guid.NewGuid();
        var studentC = Guid.NewGuid();

        var course = new Course
        {
            Title = "Backend Course",
            Slug = "be-course",
            DeliveryMode = DeliveryMode.Cohort
        };

        var module = new Module { Title = "Mod 1" };
        var quiz = new Quiz { Title = "Quiz 1", PassingScorePercentage = 70, TimeLimitMinutes = 30 };
        module.Lessons.Add(new Lesson { Title = "Lesson 1", Quiz = quiz });
        course.Modules.Add(module);
        context.Courses.Add(course);

        var cohort = new Cohort
        {
            CourseId = course.Id,
            TeacherId = teacherId,
            Name = "Qrup BE-02",
            Status = CohortStatus.Active,
            StartDate = DateTime.UtcNow.AddMonths(-1),
            QuizWeight = 30,
            MiniProjectWeight = 30,
            FinalWeight = 40
        };

        var enrollmentA = new CohortEnrollment { CohortId = cohort.Id, StudentId = studentA, Status = CohortEnrollmentStatus.Enrolled };
        var enrollmentB = new CohortEnrollment { CohortId = cohort.Id, StudentId = studentB, Status = CohortEnrollmentStatus.Enrolled };
        var enrollmentC = new CohortEnrollment { CohortId = cohort.Id, StudentId = studentC, Status = CohortEnrollmentStatus.Enrolled };

        cohort.Enrollments.Add(enrollmentA);
        cohort.Enrollments.Add(enrollmentB);
        cohort.Enrollments.Add(enrollmentC);
        context.Cohorts.Add(cohort);

        // Student A Quiz = 90%
        context.QuizAttempts.Add(new QuizAttempt { QuizId = quiz.Id, StudentId = studentA, ScorePercentage = 90, Status = QuizAttemptStatus.Completed });
        // Student B Quiz = 60%
        context.QuizAttempts.Add(new QuizAttempt { QuizId = quiz.Id, StudentId = studentB, ScorePercentage = 60, Status = QuizAttemptStatus.Completed });
        // Student C: No quiz

        await context.SaveChangesAsync();

        var usersDict = new Dictionary<Guid, UserBasicInfoDto>
        {
            [studentA] = new(studentA, "Tələbə A", "a@pragma.az"),
            [studentB] = new(studentB, "Tələbə B", "b@pragma.az"),
            [studentC] = new(studentC, "Tələbə C", "c@pragma.az")
        };

        var userLookupMock = new Mock<IUserLookupService>();
        userLookupMock.Setup(u => u.GetUserByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => usersDict.GetValueOrDefault(id));
        userLookupMock.Setup(u => u.GetUsersByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> ids, CancellationToken _) => ids.Where(usersDict.ContainsKey).ToDictionary(id => id, id => usersDict[id]));

        var service = new AcademicGradeService(context, userLookupMock.Object);

        // Bulk Submit:
        // Student A: Mini=80, Final=100 -> (90*0.3)+(80*0.3)+(100*0.4) = 27 + 24 + 40 = 91.0, IsProvisional = false
        // Student B: Mini=90, Final=null -> (60*0.3)+(90*0.3) / 0.6 = (18+27)/0.6 = 75.0, IsProvisional = true
        // Student C: Mini=null, Final=80 -> (80*0.4)/0.4 = 80.0, IsProvisional = false (Final is present!)
        var bulkDto = new BulkSubmitGradesDto(new[]
        {
            new BulkGradeStudentItemDto(studentA, MiniProjectScore: 80m, FinalScore: 100m),
            new BulkGradeStudentItemDto(studentB, MiniProjectScore: 90m, FinalScore: null),
            new BulkGradeStudentItemDto(studentC, MiniProjectScore: null, FinalScore: 80m)
        });

        // Act
        var result = await service.SubmitBulkGradesAsync(cohort.Id, bulkDto, teacherId, isAdmin: false);

        // Assert
        Assert.True(result.IsSuccess);

        var savedEnrollmentA = await context.CohortEnrollments.FirstAsync(e => e.CohortId == cohort.Id && e.StudentId == studentA);
        Assert.Equal(91.0m, savedEnrollmentA.OfficialFinalScore);
        Assert.False(savedEnrollmentA.IsProvisional);

        var savedEnrollmentB = await context.CohortEnrollments.FirstAsync(e => e.CohortId == cohort.Id && e.StudentId == studentB);
        Assert.Equal(75.0m, savedEnrollmentB.OfficialFinalScore);
        Assert.True(savedEnrollmentB.IsProvisional);

        var savedEnrollmentC = await context.CohortEnrollments.FirstAsync(e => e.CohortId == cohort.Id && e.StudentId == studentC);
        Assert.Equal(80.0m, savedEnrollmentC.OfficialFinalScore);
        Assert.True(savedEnrollmentC.IsProvisional);
    }

    [Fact]
    public async Task CohortService_CreatingCohortOnSelfPacedCourse_ReturnsValidationError()
    {
        using var context = CreateInMemoryContext();
        var instructorId = Guid.NewGuid();

        // DeliveryMode = SelfPaced olan kurs
        var course = new Course
        {
            Title = "Self-Paced HTML/CSS",
            Slug = "html-css-self",
            InstructorId = instructorId,
            DeliveryMode = DeliveryMode.SelfPaced
        };
        context.Courses.Add(course);
        await context.SaveChangesAsync();

        var userLookupMock = new Mock<IUserLookupService>();
        var createValidator = new CreateCohortValidator();
        var updateValidator = new UpdateCohortValidator();
        var scheduleValidator = new CreateCohortScheduleValidator();
        var gradeServiceMock = new Mock<IAcademicGradeService>();
        var certificatePolicyMock = new Mock<ICertificateEligibilityPolicy>();

        var cohortService = new CohortService(
            context,
            userLookupMock.Object,
            createValidator,
            updateValidator,
            scheduleValidator,
            gradeServiceMock.Object,
            certificatePolicyMock.Object);

        var dto = new CreateCohortDto(
            Name: "Kohort Qrupu",
            CourseId: course.Id,
            TeacherId: Guid.NewGuid(),
            StartDate: DateTime.UtcNow.AddDays(7),
            EndDate: DateTime.UtcNow.AddMonths(3),
            TotalHours: 60,
            QuizWeight: 30,
            MiniProjectWeight: 30,
            FinalWeight: 40,
            MaxAbsenceLimit: 6,
            LateToAbsenceRatio: 3);

        var result = await cohortService.CreateCohortAsync(dto, instructorId, isAdmin: false);

        Assert.False(result.IsSuccess);
        Assert.Equal("Cohort.DeliveryModeMismatch", result.Error.Code);
    }

    #endregion

    #region 5. Səlahiyyət və Giriş Sərhədləri (Authorization Boundaries)

    [Fact]
    public async Task AttendanceService_TeacherCannotAccessOtherTeacherCohort_ReturnsForbidden()
    {
        using var context = CreateInMemoryContext();
        var assignedTeacherId = Guid.NewGuid();
        var otherTeacherId = Guid.NewGuid();

        var course = new Course { Title = "Security", Slug = "sec", DeliveryMode = DeliveryMode.Cohort };
        context.Courses.Add(course);

        var cohort = new Cohort
        {
            CourseId = course.Id,
            TeacherId = assignedTeacherId, // Yalnız assignedTeacherId cavabdehdir
            Name = "Qrup Sec-01",
            Status = CohortStatus.Active,
            StartDate = DateTime.UtcNow.AddMonths(-2),
            EndDate = DateTime.UtcNow.AddMonths(1),
            MaxAbsenceLimit = 6,
            LateToAbsenceRatio = 3
        };
        context.Cohorts.Add(cohort);
        await context.SaveChangesAsync();

        var auditMock = new Mock<IAuditLogService>();
        var userLookupMock = new Mock<IUserLookupService>();
        var attendanceService = new AttendanceService(context, userLookupMock.Object, auditMock.Object, TimeProvider.System);

        var result = await attendanceService.GetCohortAttendanceSummaryAsync(
            cohort.Id, otherTeacherId, isAdmin: false);

        Assert.False(result.IsSuccess);
        Assert.Equal("Attendance.Forbidden", result.Error.Code);
    }

    [Fact]
    public async Task AttendanceService_StudentCannotAccessOtherStudentAttendance_ReturnsForbidden()
    {
        using var context = CreateInMemoryContext();
        var student1Id = Guid.NewGuid();
        var student2Id = Guid.NewGuid();

        var course = new Course { Title = "Networking", Slug = "net", DeliveryMode = DeliveryMode.Cohort };
        context.Courses.Add(course);

        var cohort = new Cohort
        {
            CourseId = course.Id,
            TeacherId = Guid.NewGuid(),
            Name = "Qrup Net-01",
            Status = CohortStatus.Active,
            StartDate = DateTime.UtcNow.AddMonths(-2),
            EndDate = DateTime.UtcNow.AddMonths(1)
        };
        context.Cohorts.Add(cohort);
        await context.SaveChangesAsync();

        var auditMock = new Mock<IAuditLogService>();
        var userLookupMock = new Mock<IUserLookupService>();
        var attendanceService = new AttendanceService(context, userLookupMock.Object, auditMock.Object, TimeProvider.System);

        // Student 1 öz identifikatoru ilə Student 2-nin davamiyyətinə baxmaq istəyir
        var result = await attendanceService.GetStudentAttendanceAsync(
            cohort.Id, student2Id, student1Id, isAdmin: false);

        Assert.False(result.IsSuccess);
        Assert.Equal("Attendance.Forbidden", result.Error.Code);
    }

    #endregion

    #region 8. Şərh Sistemi və Tələbə Qiymətlərim Testləri (CohortNotes & StudentGrades)

    [Fact]
    public async Task CohortNoteService_CreateFeedbackAndPrivateNotes_Success()
    {
        using var context = CreateInMemoryContext();
        var teacherId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        var course = new Course { Title = "Architecture", Slug = "arch", DeliveryMode = DeliveryMode.Cohort };
        context.Courses.Add(course);

        var cohort = new Cohort
        {
            CourseId = course.Id,
            TeacherId = teacherId,
            Name = "Cohort-101",
            Status = CohortStatus.Active
        };
        context.Cohorts.Add(cohort);
        context.CohortEnrollments.Add(new CohortEnrollment { CohortId = cohort.Id, StudentId = studentId, Status = CohortEnrollmentStatus.Enrolled });
        await context.SaveChangesAsync();

        var userLookupMock = new Mock<IUserLookupService>();
        userLookupMock.Setup(u => u.GetUserByIdAsync(teacherId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserBasicInfoDto(teacherId, "Rəşad Müəllim", "teacher@pragma.az"));
        var auditMock = new Mock<IAuditLogService>();

        var noteService = new CohortNoteService(context, userLookupMock.Object, auditMock.Object);

        // 1. Tələbəyə şərh (Feedback) yarat
        var fbResult = await noteService.CreateNoteAsync(cohort.Id, studentId, new CreateCohortNoteDto(
            Type: CohortNoteType.Feedback,
            Text: "Mini layihəniz çox yaxşı alınıb, arxitektura təmizdir.",
            GradeComponent: GradeComponentType.MiniProject
        ), currentUserId: teacherId, isAdmin: false);

        Assert.True(fbResult.IsSuccess);
        Assert.Equal(CohortNoteType.Feedback, fbResult.Value.Type);
        Assert.Equal("Rəşad Müəllim", fbResult.Value.AuthorTeacherName);
        Assert.Equal(GradeComponentType.MiniProject, fbResult.Value.GradeComponent);

        // 2. Daxili qeyd (Private note) yarat
        var privResult = await noteService.CreateNoteAsync(cohort.Id, studentId, new CreateCohortNoteDto(
            Type: CohortNoteType.Private,
            Text: "Dərslərdə diqqətsizdir, lakin tapşırıqları vaxtında təhvil verir."
        ), currentUserId: teacherId, isAdmin: false);

        Assert.True(privResult.IsSuccess);
        Assert.Equal(CohortNoteType.Private, privResult.Value.Type);

        // 3. Müəllim hər iki növü görə bilməlidir
        var notes = await noteService.GetNotesForStudentAsync(cohort.Id, studentId, currentUserId: teacherId, isAdmin: false);
        Assert.True(notes.IsSuccess);
        Assert.Equal(2, notes.Value.Count);
    }

    [Fact]
    public async Task CohortNoteService_AnotherTeacherAccessingCohort_ReturnsForbidden()
    {
        using var context = CreateInMemoryContext();
        var teacherAId = Guid.NewGuid();
        var teacherBId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        var course = new Course { Title = "Security", Slug = "sec", DeliveryMode = DeliveryMode.Cohort };
        context.Courses.Add(course);

        var cohort = new Cohort
        {
            CourseId = course.Id,
            TeacherId = teacherAId, // Təyin olunmuş müəllim A
            Name = "Cohort-Sec",
            Status = CohortStatus.Active
        };
        context.Cohorts.Add(cohort);
        context.CohortEnrollments.Add(new CohortEnrollment { CohortId = cohort.Id, StudentId = studentId, Status = CohortEnrollmentStatus.Enrolled });
        await context.SaveChangesAsync();

        var noteService = new CohortNoteService(context, Mock.Of<IUserLookupService>(), Mock.Of<IAuditLogService>());

        // Müəllim B başqa qrupa şərh yazmaq istəyir
        var result = await noteService.CreateNoteAsync(cohort.Id, studentId, new CreateCohortNoteDto(
            Type: CohortNoteType.Feedback,
            Text: "İcazəsiz şərh"
        ), currentUserId: teacherBId, isAdmin: false);

        Assert.False(result.IsSuccess);
        Assert.Equal("CohortNote.Forbidden", result.Error.Code);
    }

    [Fact]
    public async Task CohortNoteService_UpdateNote_TypeChange_And_DeleteNote_LogsAudit()
    {
        using var context = CreateInMemoryContext();
        var teacherId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        var course = new Course { Title = "DevOps", Slug = "devops", DeliveryMode = DeliveryMode.Cohort };
        context.Courses.Add(course);

        var cohort = new Cohort
        {
            CourseId = course.Id,
            TeacherId = teacherId,
            Name = "Cohort-DevOps",
            Status = CohortStatus.Active
        };
        context.Cohorts.Add(cohort);
        context.CohortEnrollments.Add(new CohortEnrollment { CohortId = cohort.Id, StudentId = studentId, Status = CohortEnrollmentStatus.Enrolled });
        await context.SaveChangesAsync();

        var auditMock = new Mock<IAuditLogService>();
        var userLookupMock = new Mock<IUserLookupService>();
        var noteService = new CohortNoteService(context, userLookupMock.Object, auditMock.Object);

        // Note yarat
        var created = await noteService.CreateNoteAsync(cohort.Id, studentId, new CreateCohortNoteDto(
            Type: CohortNoteType.Feedback,
            Text: "İlkin rəy"
        ), teacherId, isAdmin: false);

        var noteId = created.Value.Id;

        // Note növünü Feedback-dən Private-ə dəyiş
        var updated = await noteService.UpdateNoteAsync(cohort.Id, noteId, new UpdateCohortNoteDto(
            Type: CohortNoteType.Private,
            Text: "Daxili qeydə çevrildi"
        ), teacherId, isAdmin: false);

        Assert.True(updated.IsSuccess);
        Assert.Equal(CohortNoteType.Private, updated.Value.Type);

        // Audit log çağırılmalıdır: CohortNoteTypeChanged
        auditMock.Verify(a => a.LogAsync(
            It.Is<Guid?>(u => u == teacherId),
            It.IsAny<string?>(),
            It.Is<string>(action => action == "CohortNoteTypeChanged"),
            It.Is<string>(e => e == nameof(CohortNote)),
            It.Is<string>(eId => eId == noteId.ToString()),
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);

        // Note-u sil
        var deleted = await noteService.DeleteNoteAsync(cohort.Id, noteId, teacherId, isAdmin: false);
        Assert.True(deleted.IsSuccess);

        // Audit log çağırılmalıdır: CohortNoteDeleted
        auditMock.Verify(a => a.LogAsync(
            It.Is<Guid?>(u => u == teacherId),
            It.IsAny<string?>(),
            It.Is<string>(action => action == "CohortNoteDeleted"),
            It.Is<string>(e => e == nameof(CohortNote)),
            It.Is<string>(eId => eId == noteId.ToString()),
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StudentGradeService_EnrolledStudent_ReturnsOnlyFeedbackNotes_ZeroPrivateNotes()
    {
        using var context = CreateInMemoryContext();
        var teacherId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        var course = new Course { Title = "FullStack", Slug = "fs", DeliveryMode = DeliveryMode.Cohort };
        context.Courses.Add(course);

        var cohort = new Cohort
        {
            CourseId = course.Id,
            TeacherId = teacherId,
            Name = "FS-202",
            Status = CohortStatus.Active,
            QuizWeight = 30,
            MiniProjectWeight = 30,
            FinalWeight = 40
        };
        context.Cohorts.Add(cohort);

        var enrollment = new CohortEnrollment
        {
            CohortId = cohort.Id,
            StudentId = studentId,
            Status = CohortEnrollmentStatus.Enrolled
        };
        context.CohortEnrollments.Add(enrollment);

        // 1 Feedback və 1 Private note əlavə et
        context.CohortNotes.Add(new CohortNote
        {
            CohortId = cohort.Id,
            StudentId = studentId,
            AuthorTeacherId = teacherId,
            Type = CohortNoteType.Feedback,
            Text = "Tələbəyə görünən rəy"
        });

        context.CohortNotes.Add(new CohortNote
        {
            CohortId = cohort.Id,
            StudentId = studentId,
            AuthorTeacherId = teacherId,
            Type = CohortNoteType.Private,
            Text = "MƏXFİ QEYD: Tələbə heç vaxt görməməlidir!"
        });

        await context.SaveChangesAsync();

        var userLookupMock = new Mock<IUserLookupService>();
        userLookupMock.Setup(u => u.GetUserByIdAsync(teacherId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserBasicInfoDto(teacherId, "Müəllim Xanlar", "teacher@pragma.az"));

        var policy = new CertificateEligibilityPolicy();
        var studentGradeService = new StudentGradeService(context, userLookupMock.Object, policy);

        // Tələbə öz qiymət detallarını sorğulayır
        var result = await studentGradeService.GetMyCohortGradeDetailsAsync(cohort.Id, studentId);

        Assert.True(result.IsSuccess);
        var details = result.Value;

        // CRITICAL SECURITY ASSERTION:
        // Yalnız 1 Feedback qeydi gəlməlidir
        Assert.Single(details.FeedbackNotes);
        Assert.Equal("Tələbəyə görünən rəy", details.FeedbackNotes.First().Text);
        Assert.Equal("Müəllim Xanlar", details.FeedbackNotes.First().AuthorTeacherName);

        // Private note-un heç bir mətni və ya izi cavabda olmamalıdır
        Assert.DoesNotContain(details.FeedbackNotes, n => n.Text.Contains("MƏXFİ"));
    }

    [Fact]
    public async Task StudentGradeService_NonEnrolledCohort_ReturnsNotFound()
    {
        using var context = CreateInMemoryContext();
        var studentId = Guid.NewGuid();
        var otherStudentId = Guid.NewGuid();

        var course = new Course { Title = "Algorithms", Slug = "algo", DeliveryMode = DeliveryMode.Cohort };
        context.Courses.Add(course);

        var cohort = new Cohort
        {
            CourseId = course.Id,
            TeacherId = Guid.NewGuid(),
            Name = "Algo-01",
            Status = CohortStatus.Active
        };
        context.Cohorts.Add(cohort);
        // Tələbə deyil, başqa tələbə qeydiyyatdadır
        context.CohortEnrollments.Add(new CohortEnrollment { CohortId = cohort.Id, StudentId = otherStudentId, Status = CohortEnrollmentStatus.Enrolled });
        await context.SaveChangesAsync();

        var studentGradeService = new StudentGradeService(context, Mock.Of<IUserLookupService>(), new CertificateEligibilityPolicy());

        // StudentId bu kohortda qeydiyyatda deyil
        var result = await studentGradeService.GetMyCohortGradeDetailsAsync(cohort.Id, studentId);

        Assert.False(result.IsSuccess);
        Assert.Equal("StudentGrade.CohortNotFound", result.Error.Code);
    }

    #endregion

    #region Excel Export Tests (Cohort Journal)

    [Fact]
    public async Task ExportCohortJournalAsync_TeacherAccessingOwnCohort_ExportsValidExcel()
    {
        using var context = CreateInMemoryContext();
        var teacherId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        var course = new Course { Title = "FullStack .NET", Slug = "fs-net", DeliveryMode = DeliveryMode.Cohort };
        context.Courses.Add(course);

        var cohort = new Cohort
        {
            CourseId = course.Id,
            TeacherId = teacherId,
            Name = "FS-101",
            Status = CohortStatus.Active,
            QuizWeight = 30m,
            MiniProjectWeight = 30m,
            FinalWeight = 40m,
            MaxAbsenceLimit = 6
        };
        context.Cohorts.Add(cohort);

        context.CohortEnrollments.Add(new CohortEnrollment
        {
            CohortId = cohort.Id,
            StudentId = studentId,
            Status = CohortEnrollmentStatus.Enrolled
        });

        context.AttendanceRecords.Add(new AttendanceRecord
        {
            CohortId = cohort.Id,
            StudentId = studentId,
            Date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2)),
            Status = AttendanceStatus.Present,
            RecordedByUserId = teacherId
        });

        context.CohortGradeEntries.Add(new CohortGradeEntry
        {
            CohortId = cohort.Id,
            StudentId = studentId,
            ComponentType = GradeComponentType.MiniProject,
            Score = 85m,
            GradedByTeacherId = teacherId
        });

        await context.SaveChangesAsync();

        var userLookupMock = new Mock<IUserLookupService>();
        userLookupMock.Setup(u => u.GetUserByIdAsync(studentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserBasicInfoDto(studentId, "Əsmər Babayeva", "esmer@pragma.az"));
        userLookupMock.Setup(u => u.GetUserByIdAsync(teacherId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserBasicInfoDto(teacherId, "Rəşad Müəllim", "teacher@pragma.az"));

        var currentUserServiceMock = new Mock<ICurrentUserService>();
        currentUserServiceMock.Setup(u => u.UserId).Returns(teacherId);
        currentUserServiceMock.Setup(u => u.IsInRole(UserRoles.Teacher)).Returns(true);
        currentUserServiceMock.Setup(u => u.IsInRole(UserRoles.Admin)).Returns(false);

        var gradeService = new AcademicGradeService(context, userLookupMock.Object);
        var attendanceService = new AttendanceService(context, userLookupMock.Object, Mock.Of<IAuditLogService>());
        var certPolicy = new CertificateEligibilityPolicy();

        var exporter = new Pragma.Infrastructure.Services.ClosedXMLExcelExporter(
            context,
            currentUserServiceMock.Object,
            userLookupMock.Object,
            gradeService,
            attendanceService,
            certPolicy
        );

        var result = await exporter.ExportCohortJournalAsync(cohort.Id);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.NotEmpty(result.Value.Content);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", result.Value.ContentType);
        Assert.StartsWith("Pragma_Journal_", result.Value.FileName);
        Assert.EndsWith(".xlsx", result.Value.FileName);
    }

    [Fact]
    public async Task ExportCohortJournalAsync_UnauthorizedTeacher_ReturnsForbidden()
    {
        using var context = CreateInMemoryContext();
        var teacherId = Guid.NewGuid();
        var otherTeacherId = Guid.NewGuid();

        var course = new Course { Title = "Cyber Security", Slug = "cyber", DeliveryMode = DeliveryMode.Cohort };
        context.Courses.Add(course);

        var cohort = new Cohort
        {
            CourseId = course.Id,
            TeacherId = teacherId,
            Name = "CYBER-101",
            Status = CohortStatus.Active
        };
        context.Cohorts.Add(cohort);
        await context.SaveChangesAsync();

        var currentUserServiceMock = new Mock<ICurrentUserService>();
        currentUserServiceMock.Setup(u => u.UserId).Returns(otherTeacherId);
        currentUserServiceMock.Setup(u => u.IsInRole(UserRoles.Teacher)).Returns(true);
        currentUserServiceMock.Setup(u => u.IsInRole(UserRoles.Admin)).Returns(false);

        var exporter = new Pragma.Infrastructure.Services.ClosedXMLExcelExporter(
            context,
            currentUserServiceMock.Object,
            Mock.Of<IUserLookupService>(),
            Mock.Of<IAcademicGradeService>(),
            Mock.Of<IAttendanceService>(),
            new CertificateEligibilityPolicy()
        );

        var result = await exporter.ExportCohortJournalAsync(cohort.Id);

        Assert.False(result.IsSuccess);
        Assert.Equal("Cohort.Forbidden", result.Error.Code);
    }

    [Fact]
    public async Task ExportCohortJournalAsync_NonExistentCohort_ReturnsNotFound()
    {
        using var context = CreateInMemoryContext();
        var teacherId = Guid.NewGuid();

        var currentUserServiceMock = new Mock<ICurrentUserService>();
        currentUserServiceMock.Setup(u => u.UserId).Returns(teacherId);

        var exporter = new Pragma.Infrastructure.Services.ClosedXMLExcelExporter(
            context,
            currentUserServiceMock.Object,
            Mock.Of<IUserLookupService>(),
            Mock.Of<IAcademicGradeService>(),
            Mock.Of<IAttendanceService>(),
            new CertificateEligibilityPolicy()
        );

        var result = await exporter.ExportCohortJournalAsync(Guid.NewGuid());

        Assert.False(result.IsSuccess);
        Assert.Equal("Cohort.NotFound", result.Error.Code);
    }

    [Fact]
    public async Task FinalizeCohortGradesAsync_WhenUnauthorizedUser_ReturnsForbidden()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var instructorId = Guid.NewGuid();
        var unauthorizedUserId = Guid.NewGuid();

        var course = new Course
        {
            Title = "Backend Course",
            Slug = "backend-course",
            InstructorId = instructorId,
            DeliveryMode = DeliveryMode.Cohort
        };
        context.Courses.Add(course);

        var cohort = new Cohort
        {
            Name = "Cohort 1",
            CourseId = course.Id,
            TeacherId = Guid.NewGuid(),
            TotalHours = 100,
            MaxAbsenceLimit = 6,
            QuizWeight = 30,
            MiniProjectWeight = 30,
            FinalWeight = 40,
            Status = CohortStatus.Active
        };
        context.Cohorts.Add(cohort);
        await context.SaveChangesAsync();

        var userLookupMock = new Mock<IUserLookupService>();
        var service = new AcademicGradeService(context, userLookupMock.Object);

        // Act: Qeyri-admin və kursun instructor-ı olmayan şəxs
        var result = await service.FinalizeCohortGradesAsync(cohort.Id, unauthorizedUserId, isAdmin: false);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("Cohort.Forbidden", result.Error.Code);
    }

    [Fact]
    public async Task FinalizeCohortGradesAsync_WhenAdminOrInstructor_CalculatesAndPersistsOfficialScores()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var instructorId = Guid.NewGuid();

        var course = new Course
        {
            Title = "FullStack Bootcamp",
            Slug = "fullstack-bootcamp",
            InstructorId = instructorId,
            DeliveryMode = DeliveryMode.Cohort
        };
        context.Courses.Add(course);

        var module = new Module { Title = "Module 1", CourseId = course.Id, OrderIndex = 1 };
        context.Modules.Add(module);

        var lesson = new Lesson { Title = "Lesson 1", ModuleId = module.Id, OrderIndex = 1, Content = "Content" };
        context.Lessons.Add(lesson);

        var quiz = new Quiz { Title = "Quiz 1", LessonId = lesson.Id };
        context.Quizzes.Add(quiz);

        var cohort = new Cohort
        {
            Name = "FS-2026",
            CourseId = course.Id,
            TeacherId = Guid.NewGuid(),
            TotalHours = 120,
            MaxAbsenceLimit = 6,
            QuizWeight = 30,
            MiniProjectWeight = 30,
            FinalWeight = 40,
            Status = CohortStatus.Active
        };
        context.Cohorts.Add(cohort);

        var student1Id = Guid.NewGuid();
        var student2Id = Guid.NewGuid();

        var enr1 = new CohortEnrollment
        {
            CohortId = cohort.Id,
            StudentId = student1Id,
            Status = CohortEnrollmentStatus.Enrolled,
            IsProvisional = true,
            OfficialFinalScore = null
        };
        var enr2 = new CohortEnrollment
        {
            CohortId = cohort.Id,
            StudentId = student2Id,
            Status = CohortEnrollmentStatus.Enrolled,
            IsProvisional = true,
            OfficialFinalScore = null
        };
        context.CohortEnrollments.AddRange(enr1, enr2);

        // Student 1: Quiz = 90%, Mini = 80, Final = 100 -> (90*0.3) + (80*0.3) + (100*0.4) = 27 + 24 + 40 = 91.0
        context.QuizAttempts.Add(new QuizAttempt
        {
            StudentId = student1Id,
            QuizId = quiz.Id,
            ScorePercentage = 90,
            Status = QuizAttemptStatus.Completed
        });
        context.CohortGradeEntries.Add(new CohortGradeEntry
        {
            CohortId = cohort.Id,
            StudentId = student1Id,
            ComponentType = GradeComponentType.MiniProject,
            Score = 80m,
            GradedByTeacherId = cohort.TeacherId
        });
        context.CohortGradeEntries.Add(new CohortGradeEntry
        {
            CohortId = cohort.Id,
            StudentId = student1Id,
            ComponentType = GradeComponentType.Final,
            Score = 100m,
            GradedByTeacherId = cohort.TeacherId
        });

        // Student 2: Quiz = 70%, Mini = 70, Final = null -> (21 + 21) / 0.6 = 70.0
        context.QuizAttempts.Add(new QuizAttempt
        {
            StudentId = student2Id,
            QuizId = quiz.Id,
            ScorePercentage = 70,
            Status = QuizAttemptStatus.Completed
        });
        context.CohortGradeEntries.Add(new CohortGradeEntry
        {
            CohortId = cohort.Id,
            StudentId = student2Id,
            ComponentType = GradeComponentType.MiniProject,
            Score = 70m,
            GradedByTeacherId = cohort.TeacherId
        });

        await context.SaveChangesAsync();

        var userLookupMock = new Mock<IUserLookupService>();
        var service = new AcademicGradeService(context, userLookupMock.Object);

        // Act: Instructor balları rəsmiləşdirir
        var result = await service.FinalizeCohortGradesAsync(cohort.Id, instructorId, isAdmin: false);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(result.Value);

        // Bazadakı qeydləri təkrar oxuyaq
        var updatedEnr1 = await context.CohortEnrollments.FirstAsync(e => e.StudentId == student1Id);
        var updatedEnr2 = await context.CohortEnrollments.FirstAsync(e => e.StudentId == student2Id);

        Assert.False(updatedEnr1.IsProvisional);
        Assert.Equal(91.0m, updatedEnr1.OfficialFinalScore);

        Assert.False(updatedEnr2.IsProvisional);
        Assert.Equal(42.0m, updatedEnr2.OfficialFinalScore);
        Assert.True(updatedEnr2.FinalNotSubmitted);
    }

    #endregion

    #region 6. Final Layihə Təhvil Qaydaları və Sertifikat Əlaqəsi (Mərhələ 3)

    [Fact]
    public async Task FinalizeCohortGradesAsync_WhenEndDateInFuture_MissingFinal_KeepsProvisionalProportionalScore()
    {
        // (a) EndDate keçməmiş, final yox -> müvəqqəti proporsional, 0 sayılmır, FinalNotSubmitted = false
        using var context = CreateInMemoryContext();
        var instructorId = Guid.NewGuid();
        var course = new Course { Title = "Course A", Slug = "course-a", InstructorId = instructorId, DeliveryMode = DeliveryMode.Cohort };
        context.Courses.Add(course);
        var module = new Module { Title = "M1", CourseId = course.Id, OrderIndex = 1 };
        context.Modules.Add(module);
        var lesson = new Lesson { Title = "L1", ModuleId = module.Id, OrderIndex = 1, Content = "C" };
        context.Lessons.Add(lesson);
        var quiz = new Quiz { Title = "Q1", LessonId = lesson.Id };
        context.Quizzes.Add(quiz);

        var cohort = new Cohort
        {
            Name = "FS-Future",
            CourseId = course.Id,
            TeacherId = instructorId,
            TotalHours = 100,
            MaxAbsenceLimit = 6,
            QuizWeight = 30,
            MiniProjectWeight = 30,
            FinalWeight = 40,
            Status = CohortStatus.Active,
            EndDate = DateTime.UtcNow.AddDays(10) // Future
        };
        context.Cohorts.Add(cohort);

        var studentId = Guid.NewGuid();
        var enr = new CohortEnrollment
        {
            CohortId = cohort.Id,
            StudentId = studentId,
            Status = CohortEnrollmentStatus.Enrolled,
            IsProvisional = true
        };
        context.CohortEnrollments.Add(enr);

        // Quiz = 70%, Mini = 70, Final = yox
        context.QuizAttempts.Add(new QuizAttempt { StudentId = studentId, QuizId = quiz.Id, ScorePercentage = 70, Status = QuizAttemptStatus.Completed });
        context.CohortGradeEntries.Add(new CohortGradeEntry { CohortId = cohort.Id, StudentId = studentId, ComponentType = GradeComponentType.MiniProject, Score = 70m });
        await context.SaveChangesAsync();

        var service = new AcademicGradeService(context, new Mock<IUserLookupService>().Object);
        var result = await service.FinalizeCohortGradesAsync(cohort.Id, instructorId, isAdmin: false);

        Assert.True(result.IsSuccess);
        var updated = await context.CohortEnrollments.FirstAsync(e => e.StudentId == studentId);
        Assert.True(updated.IsProvisional);
        Assert.Equal(70.0m, updated.OfficialFinalScore);
        Assert.False(updated.FinalNotSubmitted);
    }

    [Fact]
    public async Task FinalizeCohortGradesAsync_WhenEndDatePassed_MissingFinal_CalculatesWithZeroAndMarksFinalNotSubmitted()
    {
        // (b) EndDate keçib, final yox -> 42.0, rəsmi, bayraq true
        using var context = CreateInMemoryContext();
        var instructorId = Guid.NewGuid();
        var course = new Course { Title = "Course B", Slug = "course-b", InstructorId = instructorId, DeliveryMode = DeliveryMode.Cohort };
        context.Courses.Add(course);
        var module = new Module { Title = "M1", CourseId = course.Id, OrderIndex = 1 };
        context.Modules.Add(module);
        var lesson = new Lesson { Title = "L1", ModuleId = module.Id, OrderIndex = 1, Content = "C" };
        context.Lessons.Add(lesson);
        var quiz = new Quiz { Title = "Q1", LessonId = lesson.Id };
        context.Quizzes.Add(quiz);

        var cohort = new Cohort
        {
            Name = "FS-Past",
            CourseId = course.Id,
            TeacherId = instructorId,
            TotalHours = 100,
            MaxAbsenceLimit = 6,
            QuizWeight = 30,
            MiniProjectWeight = 30,
            FinalWeight = 40,
            Status = CohortStatus.Active,
            EndDate = DateTime.UtcNow.AddDays(-2) // Past
        };
        context.Cohorts.Add(cohort);

        var studentId = Guid.NewGuid();
        var enr = new CohortEnrollment
        {
            CohortId = cohort.Id,
            StudentId = studentId,
            Status = CohortEnrollmentStatus.Enrolled,
            IsProvisional = true
        };
        context.CohortEnrollments.Add(enr);

        // Quiz = 70%, Mini = 70, Final = yox -> (70*0.3)+(70*0.3)+(0*0.4) = 42.0
        context.QuizAttempts.Add(new QuizAttempt { StudentId = studentId, QuizId = quiz.Id, ScorePercentage = 70, Status = QuizAttemptStatus.Completed });
        context.CohortGradeEntries.Add(new CohortGradeEntry { CohortId = cohort.Id, StudentId = studentId, ComponentType = GradeComponentType.MiniProject, Score = 70m });
        await context.SaveChangesAsync();

        var service = new AcademicGradeService(context, new Mock<IUserLookupService>().Object);
        var result = await service.FinalizeCohortGradesAsync(cohort.Id, instructorId, isAdmin: false);

        Assert.True(result.IsSuccess);
        var updated = await context.CohortEnrollments.FirstAsync(e => e.StudentId == studentId);
        Assert.False(updated.IsProvisional);
        Assert.Equal(42.0m, updated.OfficialFinalScore);
        Assert.True(updated.FinalNotSubmitted);
    }

    [Fact]
    public async Task FinalizeCohortGradesAsync_WhenEndDatePassed_FinalSubmitted_CalculatesNormalScore()
    {
        // (c) EndDate keçib, final təhvil verilib -> adi hesablama, FinalNotSubmitted = false
        using var context = CreateInMemoryContext();
        var instructorId = Guid.NewGuid();
        var course = new Course { Title = "Course C", Slug = "course-c", InstructorId = instructorId, DeliveryMode = DeliveryMode.Cohort };
        context.Courses.Add(course);
        var module = new Module { Title = "M1", CourseId = course.Id, OrderIndex = 1 };
        context.Modules.Add(module);
        var lesson = new Lesson { Title = "L1", ModuleId = module.Id, OrderIndex = 1, Content = "C" };
        context.Lessons.Add(lesson);
        var quiz = new Quiz { Title = "Q1", LessonId = lesson.Id };
        context.Quizzes.Add(quiz);

        var cohort = new Cohort
        {
            Name = "FS-Normal",
            CourseId = course.Id,
            TeacherId = instructorId,
            TotalHours = 100,
            MaxAbsenceLimit = 6,
            QuizWeight = 30,
            MiniProjectWeight = 30,
            FinalWeight = 40,
            Status = CohortStatus.Finished,
            EndDate = DateTime.UtcNow.AddDays(-1)
        };
        context.Cohorts.Add(cohort);

        var studentId = Guid.NewGuid();
        var enr = new CohortEnrollment
        {
            CohortId = cohort.Id,
            StudentId = studentId,
            Status = CohortEnrollmentStatus.Enrolled,
            IsProvisional = true
        };
        context.CohortEnrollments.Add(enr);

        context.QuizAttempts.Add(new QuizAttempt { StudentId = studentId, QuizId = quiz.Id, ScorePercentage = 90, Status = QuizAttemptStatus.Completed });
        context.CohortGradeEntries.Add(new CohortGradeEntry { CohortId = cohort.Id, StudentId = studentId, ComponentType = GradeComponentType.MiniProject, Score = 80m });
        context.CohortGradeEntries.Add(new CohortGradeEntry { CohortId = cohort.Id, StudentId = studentId, ComponentType = GradeComponentType.Final, Score = 100m });
        await context.SaveChangesAsync();

        var service = new AcademicGradeService(context, new Mock<IUserLookupService>().Object);
        var result = await service.FinalizeCohortGradesAsync(cohort.Id, instructorId, isAdmin: false);

        Assert.True(result.IsSuccess);
        var updated = await context.CohortEnrollments.FirstAsync(e => e.StudentId == studentId);
        Assert.False(updated.IsProvisional);
        Assert.Equal(91.0m, updated.OfficialFinalScore);
        Assert.False(updated.FinalNotSubmitted);
    }

    [Fact]
    public async Task CertificateService_WhenFinalNotSubmitted_ReturnsConflict409_EvenIfScoreAbove70()
    {
        // (d) Final yox, quiz+mini ilə bal 70-dən çox olsa belə -> sertifikat 409
        using var context = CreateInMemoryContext();
        var studentId = Guid.NewGuid();
        var course = new Course
        {
            Title = "Cohort Course",
            Slug = "cohort-course",
            DeliveryMode = DeliveryMode.Cohort
        };
        context.Courses.Add(course);

        var cohort = new Cohort
        {
            Name = "FS-Cohort",
            CourseId = course.Id,
            Status = CohortStatus.Finished,
            EndDate = DateTime.UtcNow.AddDays(-5),
            TeacherId = Guid.NewGuid()
        };
        context.Cohorts.Add(cohort);

        var enrollment = new Enrollment
        {
            CourseId = course.Id,
            StudentId = studentId,
            IsCompleted = true,
            ProgressPercentage = 100.0
        };
        context.Enrollments.Add(enrollment);

        var cohortEnr = new CohortEnrollment
        {
            CohortId = cohort.Id,
            StudentId = studentId,
            Status = CohortEnrollmentStatus.Completed,
            OfficialFinalScore = 75.0m,
            FinalNotSubmitted = true,
            IsProvisional = false
        };
        context.CohortEnrollments.Add(cohortEnr);
        await context.SaveChangesAsync();

        var currentUserServiceMock = new Mock<ICurrentUserService>();
        currentUserServiceMock.Setup(u => u.UserId).Returns(studentId);

        var policy = new CertificateEligibilityPolicy();
        var service = new CertificateService(
            context,
            currentUserServiceMock.Object,
            new Mock<IUserLookupService>().Object,
            new Mock<ICertificateGenerator>().Object,
            policy,
            new Mock<IAuditLogService>().Object
        );

        // Policy yoxlayırıq: Denied qaytarmalıdır
        var eval = policy.Evaluate(cohortEnr, 75.0m, 0, 6, isCohortFinished: true);
        Assert.Equal(CertificateEligibilityResult.Denied, eval.Result);

        // Service çağırırıq: Conflict (409) ProblemDetails qaytarmalıdır
        var result = await service.GenerateCertificateAsync(course.Id);
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal("Certificate.FinalNotSubmitted", result.Error.Code);
    }

    [Fact]
    public async Task CertificateService_WhenFinalSubmitted_ScoreAbovePassing_IssuesCertificateWithOfficialFinalScore()
    {
        // (e) Final təhvil verilib, bal >= hədd -> sertifikat verilir, yazılan bal OfficialFinalScore
        using var context = CreateInMemoryContext();
        var studentId = Guid.NewGuid();
        var course = new Course
        {
            Title = "Cohort Course",
            Slug = "cohort-course",
            DeliveryMode = DeliveryMode.Cohort
        };
        context.Courses.Add(course);

        var cohort = new Cohort
        {
            Name = "FS-Cohort",
            CourseId = course.Id,
            Status = CohortStatus.Finished,
            EndDate = DateTime.UtcNow.AddDays(-5),
            TeacherId = Guid.NewGuid()
        };
        context.Cohorts.Add(cohort);

        var enrollment = new Enrollment
        {
            CourseId = course.Id,
            StudentId = studentId,
            IsCompleted = true,
            ProgressPercentage = 100.0
        };
        context.Enrollments.Add(enrollment);

        var cohortEnr = new CohortEnrollment
        {
            CohortId = cohort.Id,
            StudentId = studentId,
            Status = CohortEnrollmentStatus.Completed,
            OfficialFinalScore = 88.5m,
            FinalNotSubmitted = false,
            IsProvisional = false
        };
        context.CohortEnrollments.Add(cohortEnr);

        context.CohortGradeEntries.Add(new CohortGradeEntry
        {
            CohortId = cohort.Id,
            StudentId = studentId,
            ComponentType = GradeComponentType.Final,
            Score = 90m
        });
        await context.SaveChangesAsync();

        var currentUserServiceMock = new Mock<ICurrentUserService>();
        currentUserServiceMock.Setup(u => u.UserId).Returns(studentId);

        var userLookupMock = new Mock<IUserLookupService>();
        userLookupMock.Setup(u => u.GetUserByIdAsync(studentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserBasicInfoDto(studentId, "Əli Məmmədov", "ali@pragma.edu.az"));

        var service = new CertificateService(
            context,
            currentUserServiceMock.Object,
            userLookupMock.Object,
            new Mock<ICertificateGenerator>().Object,
            new CertificateEligibilityPolicy(),
            new Mock<IAuditLogService>().Object
        );

        var result = await service.GenerateCertificateAsync(course.Id);
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(88.5, result.Value.GradePercentage);

        var certInDb = await context.Certificates.FirstAsync(c => c.StudentId == studentId && c.CourseId == course.Id);
        Assert.Equal(88.5, certInDb.GradePercentage);
        Assert.Equal(cohort.Id, certInDb.CohortId);
    }

    [Fact]
    public async Task FinalizeCohortGradesAsync_WhenTeacherEntersExplicitZeroOnFinal_TreatedAsSubmitted()
    {
        // (f) Müəllim finala açıq 0 yazıb -> təhvil verilmiş sayılır (FinalNotSubmitted = false)
        using var context = CreateInMemoryContext();
        var instructorId = Guid.NewGuid();
        var course = new Course { Title = "Course F", Slug = "course-f", InstructorId = instructorId, DeliveryMode = DeliveryMode.Cohort };
        context.Courses.Add(course);
        var module = new Module { Title = "M1", CourseId = course.Id, OrderIndex = 1 };
        context.Modules.Add(module);
        var lesson = new Lesson { Title = "L1", ModuleId = module.Id, OrderIndex = 1, Content = "C" };
        context.Lessons.Add(lesson);
        var quiz = new Quiz { Title = "Q1", LessonId = lesson.Id };
        context.Quizzes.Add(quiz);

        var cohort = new Cohort
        {
            Name = "FS-ZeroFinal",
            CourseId = course.Id,
            TeacherId = instructorId,
            TotalHours = 100,
            MaxAbsenceLimit = 6,
            QuizWeight = 30,
            MiniProjectWeight = 30,
            FinalWeight = 40,
            Status = CohortStatus.Active,
            EndDate = DateTime.UtcNow.AddDays(-3) // EndDate keçib
        };
        context.Cohorts.Add(cohort);

        var studentId = Guid.NewGuid();
        var enr = new CohortEnrollment
        {
            CohortId = cohort.Id,
            StudentId = studentId,
            Status = CohortEnrollmentStatus.Enrolled,
            IsProvisional = true
        };
        context.CohortEnrollments.Add(enr);

        context.QuizAttempts.Add(new QuizAttempt { StudentId = studentId, QuizId = quiz.Id, ScorePercentage = 80, Status = QuizAttemptStatus.Completed });
        context.CohortGradeEntries.Add(new CohortGradeEntry { CohortId = cohort.Id, StudentId = studentId, ComponentType = GradeComponentType.MiniProject, Score = 80m });
        // Müəllim açıq şəkildə Final üçün 0 daxil edib
        context.CohortGradeEntries.Add(new CohortGradeEntry { CohortId = cohort.Id, StudentId = studentId, ComponentType = GradeComponentType.Final, Score = 0m, GradedByTeacherId = instructorId });
        await context.SaveChangesAsync();

        var service = new AcademicGradeService(context, new Mock<IUserLookupService>().Object);
        var result = await service.FinalizeCohortGradesAsync(cohort.Id, instructorId, isAdmin: false);

        Assert.True(result.IsSuccess);
        var updated = await context.CohortEnrollments.FirstAsync(e => e.StudentId == studentId);
        Assert.False(updated.IsProvisional);
        // (80*0.3) + (80*0.3) + (0*0.4) = 24 + 24 + 0 = 48.0
        Assert.Equal(48.0m, updated.OfficialFinalScore);
        Assert.False(updated.FinalNotSubmitted); // Təhvil verilib (qiymətləndirilib)
    }

    [Fact]
    public async Task CertificateService_WhenCourseWithoutCohort_UnchangedAndSucceeds()
    {
        // (g) Kohortu olmayan kurs-əsaslı axın (SelfPaced) dəyişməyib
        using var context = CreateInMemoryContext();
        var studentId = Guid.NewGuid();
        var course = new Course
        {
            Title = "Self Paced Course",
            Slug = "self-paced-course",
            DeliveryMode = DeliveryMode.SelfPaced
        };
        context.Courses.Add(course);

        var enrollment = new Enrollment
        {
            CourseId = course.Id,
            StudentId = studentId,
            IsCompleted = true,
            ProgressPercentage = 100.0
        };
        context.Enrollments.Add(enrollment);
        await context.SaveChangesAsync();

        var currentUserServiceMock = new Mock<ICurrentUserService>();
        currentUserServiceMock.Setup(u => u.UserId).Returns(studentId);

        var userLookupMock = new Mock<IUserLookupService>();
        userLookupMock.Setup(u => u.GetUserByIdAsync(studentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserBasicInfoDto(studentId, "Leyla Əliyeva", "leyla@pragma.edu.az"));

        var service = new CertificateService(
            context,
            currentUserServiceMock.Object,
            userLookupMock.Object,
            new Mock<ICertificateGenerator>().Object,
            new CertificateEligibilityPolicy(),
            new Mock<IAuditLogService>().Object
        );

        var result = await service.GenerateCertificateAsync(course.Id);
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(100.0, result.Value.GradePercentage);

        var certInDb = await context.Certificates.FirstAsync(c => c.StudentId == studentId && c.CourseId == course.Id);
        Assert.Null(certInDb.CohortId);
        Assert.Equal(100.0, certInDb.GradePercentage);
    }

    #endregion
}
