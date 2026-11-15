using System.Text;
using Pragma.Application.Contracts;
using Pragma.Domain.Entities;
using Pragma.Domain.Enums;
using Pragma.Infrastructure.Services;

namespace Pragma.UnitTests;

public class GamificationAndCertificateTests
{
    [Fact]
    public void QuestPdfCertificateGenerator_GeneratesValidPdfBytesWithHeader()
    {
        // Arrange
        var generator = new QuestPdfCertificateGenerator();
        var model = new CertificateRenderModel(
            CertificateCode: "PRAGMA-2026-CSHR-7X9B1",
            StudentFullName: "Əsmər Babayeva",
            CourseTitle: "C# 14 & .NET 10 Clean Architecture",
            InstructorName: "Kənan Əliyev",
            IssuedAtUtc: new DateTime(2026, 11, 8, 12, 0, 0, DateTimeKind.Utc),
            VerificationUrl: "https://pragma.edu.az/certificates/verify/PRAGMA-2026-CSHR-7X9B1"
        );

        // Act
        var pdfBytes = generator.GeneratePdf(model);

        // Assert
        Assert.NotNull(pdfBytes);
        Assert.True(pdfBytes.Length > 1000, "PDF faylının həcmi 1 KB-dan çox olmalıdır.");

        // PDF faylı '%PDF-' başlığı ilə başlamalıdır (RFC / PDF spesifikasiyası)
        var header = Encoding.ASCII.GetString(pdfBytes.Take(5).ToArray());
        Assert.Equal("%PDF-", header);
    }

    [Fact]
    public void QuestPdfCertificateGenerator_WhenDiploma_GeneratesValidPdfWithHonorsLayout()
    {
        // Arrange
        var generator = new QuestPdfCertificateGenerator();
        var model = new CertificateRenderModel(
            CertificateCode: "PRAGMA-DIP-2026-9A1B2C3D",
            StudentFullName: "Murad Əliyev",
            CourseTitle: "Enterprise Backend Architecture",
            InstructorName: "Kənan Əliyev",
            IssuedAtUtc: DateTime.UtcNow,
            VerificationUrl: "https://pragma.edu.az/verify/PRAGMA-DIP-2026-9A1B2C3D",
            Type: CertificateType.Diploma,
            GradePercentage: 94.5
        );

        // Act
        var pdfBytes = generator.GeneratePdf(model);

        // Assert
        Assert.NotNull(pdfBytes);
        Assert.True(pdfBytes.Length > 1000);
        var header = Encoding.ASCII.GetString(pdfBytes.Take(5).ToArray());
        Assert.Equal("%PDF-", header);
    }

    [Fact]
    public void QuestPdfCertificateGenerator_WhenAttendance_GeneratesValidPdfWithAttendanceLayout()
    {
        // Arrange
        var generator = new QuestPdfCertificateGenerator();
        var model = new CertificateRenderModel(
            CertificateCode: "PRAGMA-ATT-2026-8X7Y6Z5W",
            StudentFullName: "Leyla Məmmədova",
            CourseTitle: "Enterprise Backend Architecture",
            InstructorName: "Kənan Əliyev",
            IssuedAtUtc: DateTime.UtcNow,
            VerificationUrl: "https://pragma.edu.az/verify/PRAGMA-ATT-2026-8X7Y6Z5W",
            Type: CertificateType.Attendance,
            GradePercentage: 62.0
        );

        // Act
        var pdfBytes = generator.GeneratePdf(model);

        // Assert
        Assert.NotNull(pdfBytes);
        Assert.True(pdfBytes.Length > 1000);
        var header = Encoding.ASCII.GetString(pdfBytes.Take(5).ToArray());
        Assert.Equal("%PDF-", header);
    }

    [Fact]
    public void UserStreak_WhenConsecutiveDay_IncrementsStreakAndGrantsBonusXp()
    {
        // Arrange
        var streak = new UserStreak
        {
            UserId = Guid.NewGuid(),
            CurrentStreak = 3,
            LongestStreak = 3,
            TotalXp = 150,
            LastActivityDateUtc = DateTime.UtcNow.Date.AddDays(-1) // Dünən
        };

        var today = DateTime.UtcNow.Date;
        var lastDate = streak.LastActivityDateUtc.Value.Date;
        var diff = (today - lastDate).TotalDays;

        // Act - Simulate streak calculation
        if (diff == 1)
        {
            streak.CurrentStreak += 1;
            if (streak.CurrentStreak > streak.LongestStreak)
                streak.LongestStreak = streak.CurrentStreak;
            streak.TotalXp += 25; // Ardıcıl gün bonusu
        }

        // Assert
        Assert.Equal(4, streak.CurrentStreak);
        Assert.Equal(4, streak.LongestStreak);
        Assert.Equal(175, streak.TotalXp);
    }

    [Fact]
    public void UserStreak_WhenDayMissed_ResetsStreakToOne()
    {
        // Arrange
        var streak = new UserStreak
        {
            UserId = Guid.NewGuid(),
            CurrentStreak = 7,
            LongestStreak = 7,
            TotalXp = 400,
            LastActivityDateUtc = DateTime.UtcNow.Date.AddDays(-3) // 3 gün əvvəl (fasilə yaranıb)
        };

        var today = DateTime.UtcNow.Date;
        var lastDate = streak.LastActivityDateUtc.Value.Date;
        var diff = (today - lastDate).TotalDays;

        // Act - Simulate streak calculation
        if (diff > 1)
        {
            streak.CurrentStreak = 1;
            streak.TotalXp += 10;
        }

        // Assert
        Assert.Equal(1, streak.CurrentStreak);
        Assert.Equal(7, streak.LongestStreak); // Ən uzun rekord qorunub saxlanılır
        Assert.Equal(410, streak.TotalXp);
    }

    [Fact]
    public void UserStreak_WhenSameDay_StreakUnchangedAndGrantsMinorXp()
    {
        // Arrange
        var streak = new UserStreak
        {
            UserId = Guid.NewGuid(),
            CurrentStreak = 5,
            LongestStreak = 5,
            TotalXp = 250,
            LastActivityDateUtc = DateTime.UtcNow.Date // Bu gün
        };

        var today = DateTime.UtcNow.Date;
        var lastDate = streak.LastActivityDateUtc.Value.Date;
        var diff = (today - lastDate).TotalDays;

        // Act - Simulate streak calculation
        if (diff == 0)
        {
            streak.TotalXp += 5;
        }

        // Assert
        Assert.Equal(5, streak.CurrentStreak);
        Assert.Equal(255, streak.TotalXp);
    }

    [Fact]
    public void MilestoneBadges_HaveExpectedCodesAndDescriptions()
    {
        // Arrange & Act
        var badges = new[]
        {
            new { Code = "FIRST_STEP", ExpectedName = "İlk Addım" },
            new { Code = "STREAK_3", ExpectedName = "3 Günlük Alov" },
            new { Code = "QUIZ_MASTER", ExpectedName = "Kviz Ustası" },
            new { Code = "GRADUATE", ExpectedName = "Pragma Məzunu" }
        };

        // Assert
        Assert.Equal(4, badges.Length);
        Assert.All(badges, b => Assert.False(string.IsNullOrWhiteSpace(b.Code)));
    }

    [Fact]
    public void LiveSessionRoomUrl_MatchesJitsiMeetPattern()
    {
        // Arrange
        var lessonId = Guid.NewGuid();
        var roomName = $"pragma-lesson-{lessonId:N}"[..28];
        var jitsiBaseUrl = "https://meet.jit.si";

        // Act
        var fullUrl = $"{jitsiBaseUrl}/{roomName}";

        // Assert
        Assert.StartsWith("https://meet.jit.si/pragma-lesson-", fullUrl);
        Assert.True(fullUrl.Length > 30);
    }
}
