using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Pragma.Application.Common;
using Pragma.Application.Contracts;
using Pragma.Application.DTOs.Announcements;
using Pragma.Application.DTOs.LessonQa;
using Pragma.Application.DTOs.Notifications;
using Pragma.Application.Services;
using Pragma.Domain.Entities;
using Pragma.Domain.Enums;
using Pragma.Infrastructure.Persistence;
using Xunit;

namespace Pragma.UnitTests;

public class AnnouncementNotificationAndQaTests
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
    }

    [Fact]
    public async Task NotificationService_CreateAndGetUnread_ReturnsCorrectCountAndMarksRead()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var emailSenderMock = new Mock<IEmailSender>();
        var userLookupMock = new Mock<IUserLookupService>();
        var loggerMock = new Mock<ILogger<NotificationService>>();

        var service = new NotificationService(context, emailSenderMock.Object, userLookupMock.Object, loggerMock.Object);
        var userId = Guid.NewGuid();

        // Act - 1. Create notification
        var createResult = await service.CreateNotificationAsync(new CreateNotificationDto(
            UserId: userId,
            Title: "Test Bildiriş",
            Message: "Bu bir sınaq bildirişidir.",
            Type: NotificationType.System
        ));

        Assert.True(createResult.IsSuccess);
        var notifId = createResult.Value;

        // Act - 2. Check unread count
        var countBefore = await service.GetUnreadCountAsync(userId);
        Assert.Equal(1, countBefore.Value);

        // Act - 3. Mark as read
        var readResult = await service.MarkAsReadAsync(notifId, userId);
        Assert.True(readResult.IsSuccess);

        // Act - 4. Check unread count again
        var countAfter = await service.GetUnreadCountAsync(userId);
        Assert.Equal(0, countAfter.Value);
    }

    [Fact]
    public async Task NotificationService_MarkAllAsRead_MarksAllUnreadNotificationsAsRead()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var emailSenderMock = new Mock<IEmailSender>();
        var userLookupMock = new Mock<IUserLookupService>();
        var loggerMock = new Mock<ILogger<NotificationService>>();

        var service = new NotificationService(context, emailSenderMock.Object, userLookupMock.Object, loggerMock.Object);
        var userId = Guid.NewGuid();

        await service.CreateNotificationAsync(new CreateNotificationDto(userId, "N1", "M1"));
        await service.CreateNotificationAsync(new CreateNotificationDto(userId, "N2", "M2"));
        await service.CreateNotificationAsync(new CreateNotificationDto(userId, "N3", "M3"));

        var countBefore = await service.GetUnreadCountAsync(userId);
        Assert.Equal(3, countBefore.Value);

        // Act
        var result = await service.MarkAllAsReadAsync(userId);
        Assert.True(result.IsSuccess);

        // Assert
        var countAfter = await service.GetUnreadCountAsync(userId);
        Assert.Equal(0, countAfter.Value);
    }

    [Fact]
    public async Task AnnouncementService_WhenGlobalAndNotAdmin_ReturnsForbidden()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var notifServiceMock = new Mock<INotificationService>();
        var userLookupMock = new Mock<IUserLookupService>();
        var loggerMock = new Mock<ILogger<AnnouncementService>>();

        var service = new AnnouncementService(context, notifServiceMock.Object, userLookupMock.Object, loggerMock.Object);

        var dto = new CreateAnnouncementDto("Qlobal Elan", "Bütün tələbələrə elan", CohortId: null);

        // Act
        var result = await service.CreateAnnouncementAsync(dto, authorId: Guid.NewGuid(), isAdmin: false);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Forbidden, result.Error.Type);
    }

    [Fact]
    public async Task AnnouncementService_WhenValidCohortAnnouncement_CreatesAndDispatchesNotifications()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var notifServiceMock = new Mock<INotificationService>();
        var userLookupMock = new Mock<IUserLookupService>();
        var loggerMock = new Mock<ILogger<AnnouncementService>>();

        var teacherId = Guid.NewGuid();
        var student1Id = Guid.NewGuid();
        var student2Id = Guid.NewGuid();

        var cohort = new Cohort
        {
            Name = "FS-101",
            TeacherId = teacherId,
            CourseId = Guid.NewGuid(),
            StartDate = DateTime.UtcNow.AddMonths(-1),
            EndDate = DateTime.UtcNow.AddMonths(2)
        };
        cohort.Enrollments.Add(new CohortEnrollment { CohortId = cohort.Id, StudentId = student1Id });
        cohort.Enrollments.Add(new CohortEnrollment { CohortId = cohort.Id, StudentId = student2Id });
        context.Cohorts.Add(cohort);
        await context.SaveChangesAsync();

        var service = new AnnouncementService(context, notifServiceMock.Object, userLookupMock.Object, loggerMock.Object);

        var dto = new CreateAnnouncementDto("Dərs Təxirə Salındı", "Növbəti dərs saat 19:00-da olacaq", CohortId: cohort.Id, IsPinned: true);

        // Act
        var result = await service.CreateAnnouncementAsync(dto, authorId: teacherId, isAdmin: false);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message ?? "Xəta");
        Assert.Equal("Dərs Təxirə Salındı", result.Value.Title);
        Assert.True(result.Value.IsPinned);

        // 2 tələbəyə bildiriş göndərildiyi yoxlanılır
        notifServiceMock.Verify(n => n.CreateNotificationAsync(
            It.Is<CreateNotificationDto>(d => d.UserId == student1Id && d.Type == NotificationType.Announcement),
            It.IsAny<CancellationToken>()), Times.Once);

        notifServiceMock.Verify(n => n.CreateNotificationAsync(
            It.Is<CreateNotificationDto>(d => d.UserId == student2Id && d.Type == NotificationType.Announcement),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LessonQaService_AskQuestion_CreatesQuestionWithPositionSeconds()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var notifServiceMock = new Mock<INotificationService>();
        var userLookupMock = new Mock<IUserLookupService>();
        var loggerMock = new Mock<ILogger<LessonQaService>>();

        var lesson = new Lesson { Title = "Async Await in C#", ModuleId = Guid.NewGuid(), OrderIndex = 1 };
        context.Lessons.Add(lesson);
        await context.SaveChangesAsync();

        var service = new LessonQaService(context, notifServiceMock.Object, userLookupMock.Object, loggerMock.Object);
        var studentId = Guid.NewGuid();

        var dto = new CreateLessonQuestionDto("ConfigureAwait nə üçündür?", "UI tətbiqlərində kontekst bloku necə qarşısını alır?", PositionSeconds: 145);

        // Act
        var result = await service.AskQuestionAsync(lesson.Id, studentId, dto);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message ?? "Xəta");
        Assert.Equal("ConfigureAwait nə üçündür?", result.Value.Title);
        Assert.Equal(145, result.Value.PositionSeconds);
        Assert.False(result.Value.IsResolved);
    }

    [Fact]
    public async Task LessonQaService_AnswerQuestion_WhenInstructor_SetsBadgeAndNotifiesStudent()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var notifServiceMock = new Mock<INotificationService>();
        var userLookupMock = new Mock<IUserLookupService>();
        var loggerMock = new Mock<ILogger<LessonQaService>>();

        var studentId = Guid.NewGuid();
        var instructorId = Guid.NewGuid();

        var question = new LessonQuestion
        {
            LessonId = Guid.NewGuid(),
            StudentId = studentId,
            Title = "EF Core Split Query",
            Content = "N+1 problemini necə həll edir?"
        };
        context.LessonQuestions.Add(question);
        await context.SaveChangesAsync();

        var service = new LessonQaService(context, notifServiceMock.Object, userLookupMock.Object, loggerMock.Object);

        var dto = new CreateLessonAnswerDto("AsSplitQuery() metodu böyük Cartesian partlayışının qarşısını alır.");

        // Act
        var result = await service.AnswerQuestionAsync(question.Id, authorId: instructorId, isInstructor: true, dto);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message ?? "Xəta");
        Assert.True(result.Value.IsInstructorAnswer);

        // Tələbəyə bildiriş göndərildiyi yoxlanılır
        notifServiceMock.Verify(n => n.CreateNotificationAsync(
            It.Is<CreateNotificationDto>(d => d.UserId == studentId && d.Type == NotificationType.LessonQa),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AttendanceService_WhenAbsenceReachesWarningLimit_TriggersNotification()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var userLookupMock = new Mock<IUserLookupService>();
        var auditMock = new Mock<IAuditLogService>();
        var notifServiceMock = new Mock<INotificationService>();

        var teacherId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        var course = new Course
        {
            Title = "Backend Development",
            Slug = "backend-dev",
            InstructorId = teacherId
        };
        context.Courses.Add(course);

        var cohort = new Cohort
        {
            Name = "PRAGMA-BACKEND",
            TeacherId = teacherId,
            CourseId = course.Id,
            StartDate = DateTime.UtcNow.AddMonths(-1),
            EndDate = DateTime.UtcNow.AddMonths(1),
            MaxAbsenceLimit = 6,
            LateToAbsenceRatio = 3
        };
        var enrollment = new CohortEnrollment
        {
            CohortId = cohort.Id,
            StudentId = studentId,
            HasAbsenceWarning = false
        };
        cohort.Enrollments.Add(enrollment);

        // Cədvələ bazar ertəsi əlavə et
        var testDate = new DateOnly(2026, 11, 16); // Monday
        cohort.Schedules.Add(new CohortSchedule
        {
            CohortId = cohort.Id,
            DayOfWeek = DayOfWeek.Monday,
            StartTime = new TimeSpan(19, 0, 0),
            EndTime = new TimeSpan(21, 0, 0)
        });

        // 3 mövcud qayıb əlavə et
        for (int i = 1; i <= 3; i++)
        {
            context.AttendanceRecords.Add(new AttendanceRecord
            {
                CohortId = cohort.Id,
                StudentId = studentId,
                Date = testDate.AddDays(-7 * i),
                Status = AttendanceStatus.Absent
            });
        }

        context.Cohorts.Add(cohort);
        await context.SaveChangesAsync();

        var fakeNow = new DateTimeOffset(2026, 11, 18, 10, 0, 0, TimeSpan.FromHours(4));
        var timeProvider = new FakeTimeProvider(fakeNow);

        var attendanceService = new AttendanceService(
            context,
            userLookupMock.Object,
            auditMock.Object,
            timeProvider,
            notifServiceMock.Object);

        // Act - 4-cü qayıbı yazırıq (limit 6 - 2 = 4 xəbərdarlıq həddidir)
        var bulkDto = new BulkAttendanceDto(
            testDate,
            new List<AttendanceEntryDto>
            {
                new(studentId, AttendanceStatus.Absent, "Dərsə gəlmədi")
            }
        );

        var result = await attendanceService.RecordBulkAttendanceAsync(
            cohort.Id,
            bulkDto,
            teacherId,
            isAdmin: false);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message ?? "Xəta");

        var dbEnrollment = await context.CohortEnrollments.FirstAsync(e => e.CohortId == cohort.Id && e.StudentId == studentId);
        Assert.True(dbEnrollment.HasAbsenceWarning);

        // Xəbərdarlıq bildirişinin çağırıldığı təsdiqlənir
        notifServiceMock.Verify(n => n.CreateNotificationAsync(
            It.Is<CreateNotificationDto>(d => d.UserId == studentId && d.Type == NotificationType.AbsenceWarning),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
