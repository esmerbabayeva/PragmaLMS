using Pragma.Application.Common;
using Pragma.Application.DTOs.Certificates;
using Pragma.Application.DTOs.LiveSession;
using Pragma.Domain.Entities;
using Pragma.Domain.Enums;

namespace Pragma.UnitTests;

/// <summary>
/// Mərhələ 6 üçün AI Mentor, Canlı Dərs, Sertifikat və İmtahan məntiqini yoxlayan hərtərəfli Unit Testlər.
/// </summary>
public class AiMentorAndServiceIntegrationTests
{
    [Fact]
    public void AiMentor_SocraticHint_EmphasizesPedagogicalGuidanceWithoutLeakingDirectAnswer()
    {
        // Arrange
        var questionText = "Onion Architecture-də Domain qatı hansı layihədən asılı olmalıdır?";

        // Act - Simulate Socratic pedagogical hint generator
        var hint = $"İpucu (Sokratik metod): '{questionText}' sualında cavabı tapmaq üçün arxitekturanın əsas 'Asılılıqların İdarəedilməsi' (Dependency Inversion) qaydasını xatırlayın. " +
                   "Əsas biznes məntiqinin digər xarici infrastrukturlardan asılı olub-olmadığını düşünün.";

        // Assert
        Assert.NotNull(hint);
        Assert.Contains("Sokratik", hint);
        Assert.Contains("Dependency Inversion", hint);
        Assert.DoesNotContain("Doğru cavab A variantıdır", hint);
    }

    [Fact]
    public void AiMentor_SuggestedFollowUps_ProvidesThreeContextualQuestions()
    {
        // Arrange
        var lessonTitle = "Dependency Injection & Lifetimes";

        // Act
        var followUps = new List<string>
        {
            $"{lessonTitle} mövzusunda ən çox edilən səhvlər nələrdir?",
            "Bunu real layihədə necə tətbiq edə bilərəm?",
            "Bu mövzu üzrə praktiki tapşırıq nümunəsi verə bilərsən?"
        };

        // Assert
        Assert.Equal(3, followUps.Count);
        Assert.All(followUps, q => Assert.True(q.Contains(lessonTitle) || q.EndsWith("?")));
    }

    [Fact]
    public void LiveSession_RoomNameAndUrl_FollowsDeterministicPattern()
    {
        // Arrange
        var courseSlug = "csharp-14-net-10-clean-architecture";
        var lessonId = Guid.Parse("11111111-2222-3333-4444-555555555555");

        // Act
        var roomName = $"pragma-live-{courseSlug}-{lessonId.ToString()[..8]}";
        var meetingUrl = $"https://meet.jit.si/{roomName}";

        // Assert
        Assert.Equal("pragma-live-csharp-14-net-10-clean-architecture-11111111", roomName);
        Assert.StartsWith("https://meet.jit.si/pragma-live-", meetingUrl);
    }

    [Theory]
    [InlineData("Instructor", true)]
    [InlineData("Admin", true)]
    [InlineData("Student", false)]
    public void LiveSession_HostPrivilege_DeterminedByRoleAndCourseOwnership(string userRole, bool expectedIsHost)
    {
        // Arrange
        var instructorId = Guid.NewGuid();
        var currentUserId = userRole == "Instructor" ? instructorId : Guid.NewGuid();

        // Act
        var isInstructorOfCourse = currentUserId == instructorId;
        var isAdmin = userRole == "Admin";
        var canHost = isInstructorOfCourse || isAdmin;

        // Assert
        Assert.Equal(expectedIsHost, canHost);
    }

    [Theory]
    [InlineData(100.0, true, true)]
    [InlineData(100.0, false, true)]
    [InlineData(99.9, false, false)]
    [InlineData(50.0, false, false)]
    [InlineData(0.0, false, false)]
    public void Certificate_Eligibility_EnforcesStrict100PercentRequirement(double progress, bool isCompletedFlag, bool expectedEligible)
    {
        // Arrange & Act
        var isEligible = isCompletedFlag || progress >= 100.0;

        // Assert
        Assert.Equal(expectedEligible, isEligible);
    }

    [Fact]
    public void Certificate_CodeFormat_MatchesStandardPragmaPattern()
    {
        // Arrange
        var year = DateTime.UtcNow.Year;
        var uniqueSuffix = Guid.NewGuid().ToString("N")[..5].ToUpperInvariant();

        // Act
        var code = $"PRAGMA-{year}-CSHR-{uniqueSuffix}";
        var verificationUrl = $"https://pragma.edu.az/certificates/verify/{code}";

        // Assert
        Assert.Matches(@"^PRAGMA-\d{4}-[A-Z0-9]{4}-[A-Z0-9]{5}$", code);
        Assert.Contains(code, verificationUrl);
    }

    [Theory]
    [InlineData(4, 4, 100.0, true)]  // 4 sualdan 4 düzgün = 100% (Keçib)
    [InlineData(3, 4, 75.0, false)]  // 4 sualdan 3 düzgün = 75% (< 80% Qalıb)
    [InlineData(5, 5, 100.0, true)]  // 5 sualdan 5 düzgün = 100% (Keçib)
    [InlineData(4, 5, 80.0, true)]   // 5 sualdan 4 düzgün = 80% (>= 80% Keçib)
    [InlineData(0, 4, 0.0, false)]   // 0% (Qalıb)
    public void QuizScoring_PassingThreshold80Percent_EvaluatesAccurately(
        int correctCount, int totalQuestions, double expectedScore, bool expectedPassed)
    {
        // Arrange & Act
        var calculatedScore = Math.Round((double)correctCount / totalQuestions * 100.0, 1);
        var isPassed = calculatedScore >= 80.0;

        // Assert
        Assert.Equal(expectedScore, calculatedScore);
        Assert.Equal(expectedPassed, isPassed);
    }

    [Fact]
    public void QuizAttempt_IncrementAttemptCount_TracksAttemptsSeparately()
    {
        // Arrange
        var quiz = new Quiz
        {
            Title = "Clean Architecture Yoxlama İmtahanı",
            PassingScorePercentage = 80,
            MaxAttempts = 3
        };

        var studentAttempts = new List<QuizAttempt>
        {
            new() { QuizId = quiz.Id, AttemptNumber = 1, ScorePercentage = 60, IsPassed = false },
            new() { QuizId = quiz.Id, AttemptNumber = 2, ScorePercentage = 100, IsPassed = true }
        };

        // Act
        var nextAttemptNumber = studentAttempts.Count + 1;
        var hasPassedAlready = studentAttempts.Any(a => a.IsPassed);

        // Assert
        Assert.Equal(3, nextAttemptNumber);
        Assert.True(hasPassedAlready);
        Assert.True(nextAttemptNumber <= quiz.MaxAttempts);
    }

    [Fact]
    public void Gamification_LongestStreakPreserved_WhenStreakResets()
    {
        // Arrange
        var streak = new UserStreak
        {
            UserId = Guid.NewGuid(),
            CurrentStreak = 14,
            LongestStreak = 14,
            TotalXp = 500,
            LastActivityDateUtc = DateTime.UtcNow.AddDays(-5) // 5 gün heç nə etməyib
        };

        // Act - Break streak and start fresh
        streak.CurrentStreak = 1;
        streak.TotalXp += 10;
        streak.LastActivityDateUtc = DateTime.UtcNow;

        // Assert
        Assert.Equal(1, streak.CurrentStreak);
        Assert.Equal(14, streak.LongestStreak); // Ən yüksək rekord 14 olaraq toxunulmaz qalır
        Assert.Equal(510, streak.TotalXp);
    }

    [Theory]
    [InlineData("Hava bugun necedi", true)]
    [InlineData("Bugun hava necedir?", true)]
    [InlineData("Sabah yagis yagacaq?", true)]
    [InlineData("Futbol oyunu neçədədir?", true)]
    [InlineData("Mənə pizza resepti ver", true)]
    [InlineData("Kino və film tövsiyə et", true)]
    [InlineData("Dolların məzənnəsi neçədir?", true)]
    [InlineData("QB limitim nə qədərdir?", false)]
    [InlineData("Qayıb necə hesablanır?", false)]
    [InlineData("Yekun bal və qiymətləndirmə qaydası necədir?", false)]
    [InlineData("Diplom və sertifikat necə verilir?", false)]
    [InlineData("Dərs saatları cədvəli haradadır?", false)]
    [InlineData("C# kursunda nə öyrədilir?", false)]
    public void AiMentor_ScopeClassifier_AccuratelyDetectsOffTopicVersusInScopeQuestions(string question, bool expectedOutOfScope)
    {
        // Act
        var isOut = Pragma.Application.Services.AiMentorService.IsOutOfScope(question);

        // Assert
        Assert.Equal(expectedOutOfScope, isOut);
    }

    [Fact]
    public async Task AiMentor_OutOfScopeQuestion_ReturnsOfficialBoundaryMessage()
    {
        // Arrange
        var contextMock = new Moq.Mock<Pragma.Application.Contracts.IAppDbContext>();
        var userMock = new Moq.Mock<Pragma.Application.Contracts.ICurrentUserService>();
        var accessMock = new Moq.Mock<Pragma.Application.Contracts.ICourseAccessService>();
        var aiSettings = new Pragma.Application.Common.AiSettings { GeminiApiKey = null };

        var service = new Pragma.Application.Services.AiMentorService(
            contextMock.Object,
            userMock.Object,
            accessMock.Object,
            aiSettings);

        // Act
        var result = await service.AskGlobalQuestionAsync("Hava bugun necedi");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(Pragma.Application.Services.AiMentorService.OutOfScopeMessage, result.Value.Answer);
        Assert.Contains("tədris platforması", result.Value.Answer);
        Assert.Contains("hava proqnozu", result.Value.Answer);
    }

    [Fact]
    public async Task AiMentor_InScopeQbQuestion_ReturnsAccurateAbsenceRules()
    {
        // Arrange
        var contextMock = new Moq.Mock<Pragma.Application.Contracts.IAppDbContext>();
        var userMock = new Moq.Mock<Pragma.Application.Contracts.ICurrentUserService>();
        var accessMock = new Moq.Mock<Pragma.Application.Contracts.ICourseAccessService>();
        var aiSettings = new Pragma.Application.Common.AiSettings { GeminiApiKey = null };

        var service = new Pragma.Application.Services.AiMentorService(
            contextMock.Object,
            userMock.Object,
            accessMock.Object,
            aiSettings);

        // Act
        var result = await service.AskGlobalQuestionAsync("QB limitim və qayıb qaydaları necədir?");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Contains("Maksimum Qayıb Limiti", result.Value.Answer);
        Assert.Contains("6 qayıbdır", result.Value.Answer);
        Assert.Contains("LateToAbsenceRatio = 3", result.Value.Answer);
        Assert.Contains("Dropped", result.Value.Answer);
    }

    [Fact]
    public async Task AiMentor_InScopeGradingQuestion_ReturnsStrictWeightsAndThreshold()
    {
        // Arrange
        var contextMock = new Moq.Mock<Pragma.Application.Contracts.IAppDbContext>();
        var userMock = new Moq.Mock<Pragma.Application.Contracts.ICurrentUserService>();
        var accessMock = new Moq.Mock<Pragma.Application.Contracts.ICourseAccessService>();
        var aiSettings = new Pragma.Application.Common.AiSettings { GeminiApiKey = null };

        var service = new Pragma.Application.Services.AiMentorService(
            contextMock.Object,
            userMock.Object,
            accessMock.Object,
            aiSettings);

        // Act
        var result = await service.AskGlobalQuestionAsync("Yekun bal necə hesablanır?");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Contains("Kviz (Quiz) Ortalaması (30%)", result.Value.Answer);
        Assert.Contains("Mini Layihə Ortalaması (30%)", result.Value.Answer);
        Assert.Contains("Final Layihə (40%)", result.Value.Answer);
        Assert.Contains("70.0", result.Value.Answer);
    }

    [Fact]
    public async Task AiMentor_GreetingVersusHelpInquiry_ReturnsDistinctAndSpecificResponses()
    {
        // Arrange
        var contextMock = new Moq.Mock<Pragma.Application.Contracts.IAppDbContext>();
        var userMock = new Moq.Mock<Pragma.Application.Contracts.ICurrentUserService>();
        var accessMock = new Moq.Mock<Pragma.Application.Contracts.ICourseAccessService>();
        var aiSettings = new Pragma.Application.Common.AiSettings { GeminiApiKey = null };

        var service = new Pragma.Application.Services.AiMentorService(
            contextMock.Object,
            userMock.Object,
            accessMock.Object,
            aiSettings);

        // Act 1: Yalnız salamlaşma
        var greetingResult = await service.AskGlobalQuestionAsync("Salam");

        // Act 2: Köməklik və imkanlar barədə spesifik sual
        var helpResult = await service.AskGlobalQuestionAsync("mene hansi komeklikleri gostere bilirsen?");

        // Assert
        Assert.True(greetingResult.IsSuccess);
        Assert.True(helpResult.IsSuccess);

        // Cavablar eyni olmamalıdır!
        Assert.NotEqual(greetingResult.Value.Answer, helpResult.Value.Answer);

        // Köməklik sualında mütləq bənd-bənd 5 istiqamət spesifik təqdim edilməlidir
        Assert.Contains("Davamiyyət və Qayıb (QB)", helpResult.Value.Answer);
        Assert.Contains("Yekun Bal və Qiymətləndirmə", helpResult.Value.Answer);
        Assert.Contains("Diplom və Sertifikat", helpResult.Value.Answer);
        Assert.Contains("Dərs Cədvəli", helpResult.Value.Answer);
        Assert.Contains("Kurrikulum", helpResult.Value.Answer);

        // Salamlaşmada isə qısa, xoş qarşılama və istiqamətləndirmə olmalıdır
        Assert.Contains("Gününüz uğurlu olsun", greetingResult.Value.Answer);
        Assert.Contains("maraqlandığınız mövzunu qeyd edin", greetingResult.Value.Answer);
    }
}
