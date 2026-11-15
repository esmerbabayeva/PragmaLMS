using Microsoft.EntityFrameworkCore;
using Moq;
using Pragma.Application.Contracts;
using Pragma.Application.DTOs.Quizzes;
using Pragma.Application.Services;
using Pragma.Application.Validators.Quizzes;
using Pragma.Domain.Entities;
using Pragma.Infrastructure.Persistence;
using Xunit;

namespace Pragma.UnitTests;

public class QuizTemplateAndAssignmentTests
{
    private static AppDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task CreateTemplate_WithValidQuestions_ReturnsSuccess()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var validator = new CreateQuizTemplateValidator();
        var service = new QuizTemplateService(context, validator);

        var teacherId = Guid.NewGuid();
        var dto = new CreateQuizTemplateDto(
            Title: "C# 14 və .NET 10 Giriş Sınağı",
            Description: "Baza biliklərinin yoxlanışı",
            DefaultTimeLimitMinutes: 25,
            DefaultPassingScorePercentage: 70,
            DefaultPointsPerQuestion: 10,
            DefaultMaxAttempts: 3,
            CourseId: null,
            Questions: new List<TemplateQuestionDto>
            {
                new("Record struct nədir?", 10, 1, "C# 10 yeniliyi", new List<TemplateOptionDto>
                {
                    new("Dəyər tipli immutability təmin edən struktur", true),
                    new("Referans tipli sinif", false)
                }),
                new(".NET 10 hansı ildə buraxılıb?", 10, 2, null, new List<TemplateOptionDto>
                {
                    new("2026", true),
                    new("2020", false)
                })
            }
        );

        // Act
        var result = await service.CreateTemplateAsync(dto, teacherId, isAdmin: false);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Value);

        var template = await context.QuizTemplates.FindAsync(result.Value);
        Assert.NotNull(template);
        Assert.Equal("C# 14 və .NET 10 Giriş Sınağı", template.Title);
        Assert.Equal(teacherId, template.TeacherId);
    }

    [Fact]
    public async Task CreateAssignment_FromTemplate_CopiesQuestionsSnapshot()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var templateValidator = new CreateQuizTemplateValidator();
        var templateService = new QuizTemplateService(context, templateValidator);

        var teacherId = Guid.NewGuid();
        var cohortId = Guid.NewGuid();

        var templateDto = new CreateQuizTemplateDto(
            Title: "Onion Architecture Test",
            Description: "Şablon",
            DefaultTimeLimitMinutes: 20,
            DefaultPassingScorePercentage: 70,
            DefaultPointsPerQuestion: 10,
            DefaultMaxAttempts: 3,
            CourseId: null,
            Questions: new List<TemplateQuestionDto>
            {
                new("Domain qatı xarici asılılıq daşıyırmı?", 10, 1, null, new List<TemplateOptionDto>
                {
                    new("Xeyr, Domain ən daxili qatdır", true),
                    new("Bəli, Infrastructure-dan asılıdır", false)
                })
            }
        );

        var templateResult = await templateService.CreateTemplateAsync(templateDto, teacherId, isAdmin: false);
        Assert.True(templateResult.IsSuccess);
        var templateId = templateResult.Value;

        var assignmentValidator = new CreateQuizAssignmentValidator();
        var legacyQuizMock = new Mock<IQuizService>();
        var assignmentService = new QuizAssignmentService(context, assignmentValidator, legacyQuizMock.Object);

        var assignDto = new CreateQuizAssignmentDto(
            TemplateId: templateId,
            CohortId: cohortId,
            CourseId: null,
            StudentIds: null,
            LessonId: null,
            CustomTitle: "Qrup BE-102 üçün Sınaq İmtahanı",
            CustomDescription: "Fərdi qrup imtahanı",
            CustomTimeLimitMinutes: 35, // 20 əvəzinə 35 dəqiqə
            PassingScorePercentage: 75, // 70 əvəzinə 75%
            PointsPerQuestion: 20,       // 10 əvəzinə 20 bal
            MaxAttempts: 2,
            StartDateUtc: DateTime.UtcNow.AddMinutes(-5),
            EndDateUtc: DateTime.UtcNow.AddDays(7)
        );

        // Act
        var assignResult = await assignmentService.CreateAssignmentAsync(assignDto, teacherId, isAdmin: false);

        // Assert
        Assert.True(assignResult.IsSuccess);
        var assignment = await context.QuizAssignments.FindAsync(assignResult.Value);
        Assert.NotNull(assignment);
        Assert.Equal("Qrup BE-102 üçün Sınaq İmtahanı", assignment.Title);
        Assert.Equal(35, assignment.CustomTimeLimitMinutes);
        Assert.Equal(75, assignment.PassingScorePercentage);
        Assert.Equal(20, assignment.PointsPerQuestion);
        Assert.Contains("Domain qatı xarici asılılıq daşıyırmı?", assignment.QuestionsSnapshotJson);
        Assert.Contains("\"points\":20", assignment.QuestionsSnapshotJson);
    }

    [Fact]
    public async Task DataIntegrity_WhenTemplateQuestionsChange_AssignmentSnapshotRemainsUnchanged()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var templateValidator = new CreateQuizTemplateValidator();
        var templateService = new QuizTemplateService(context, templateValidator);
        var teacherId = Guid.NewGuid();

        var templateDto = new CreateQuizTemplateDto(
            Title: "Orijinal Şablon",
            Description: null,
            DefaultTimeLimitMinutes: 15,
            DefaultPassingScorePercentage: 70,
            DefaultPointsPerQuestion: 10,
            DefaultMaxAttempts: 3,
            CourseId: null,
            Questions: new List<TemplateQuestionDto>
            {
                new("Sual 1: İlkin sual mətnidir", 10, 1, null, new List<TemplateOptionDto>
                {
                    new("Cavab A", true),
                    new("Cavab B", false)
                })
            }
        );

        var templateResult = await templateService.CreateTemplateAsync(templateDto, teacherId, isAdmin: false);
        var templateId = templateResult.Value;

        var assignmentValidator = new CreateQuizAssignmentValidator();
        var legacyQuizMock = new Mock<IQuizService>();
        var assignmentService = new QuizAssignmentService(context, assignmentValidator, legacyQuizMock.Object);

        var assignDto = new CreateQuizAssignmentDto(
            TemplateId: templateId,
            CohortId: Guid.NewGuid(),
            CourseId: null,
            StudentIds: null,
            LessonId: null,
            CustomTitle: null,
            CustomDescription: null,
            CustomTimeLimitMinutes: null,
            PassingScorePercentage: null,
            PointsPerQuestion: null,
            MaxAttempts: null,
            StartDateUtc: null,
            EndDateUtc: null
        );

        var assignResult = await assignmentService.CreateAssignmentAsync(assignDto, teacherId, isAdmin: false);
        var assignmentId = assignResult.Value;

        // Act: Müəllim şablondakı sualı tamamilə dəyişir
        var updateDto = new UpdateQuizTemplateDto(
            Title: "Dəyişdirilmiş Şablon",
            Description: "Yeni təsvir",
            DefaultTimeLimitMinutes: 30,
            DefaultPassingScorePercentage: 80,
            DefaultPointsPerQuestion: 25,
            DefaultMaxAttempts: 1,
            CourseId: null,
            Questions: new List<TemplateQuestionDto>
            {
                new("TAMAMİLƏ YENİ VƏ FƏRQLİ SUAL", 25, 1, null, new List<TemplateOptionDto>
                {
                    new("Yeni Cavab 1", true),
                    new("Yeni Cavab 2", false)
                })
            }
        );

        var updateResult = await templateService.UpdateTemplateAsync(templateId, updateDto, teacherId, isAdmin: false);
        Assert.True(updateResult.IsSuccess);

        // Assert: Artıq təyin edilmiş Assignment-ın sual snapshot-u toxunulmaz qalmalıdır!
        var assignment = await context.QuizAssignments.FindAsync(assignmentId);
        Assert.NotNull(assignment);
        Assert.Contains("Sual 1: İlkin sual mətnidir", assignment.QuestionsSnapshotJson);
        Assert.DoesNotContain("TAMAMİLƏ YENİ VƏ FƏRQLİ SUAL", assignment.QuestionsSnapshotJson);
    }

    [Fact]
    public async Task GetAssignmentForTaking_WhenDeadlinePassed_ReturnsForbidden()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var assignmentValidator = new CreateQuizAssignmentValidator();
        var legacyQuizMock = new Mock<IQuizService>();
        var assignmentService = new QuizAssignmentService(context, assignmentValidator, legacyQuizMock.Object);

        var studentId = Guid.NewGuid();
        var expiredAssignment = new QuizAssignment
        {
            TemplateId = Guid.NewGuid(),
            TeacherId = Guid.NewGuid(),
            Title = "Vaxtı Bitmiş İmtahan",
            IsActive = true,
            StartDateUtc = DateTime.UtcNow.AddDays(-10),
            EndDateUtc = DateTime.UtcNow.AddMinutes(-5), // 5 dəqiqə əvvəl bitib!
            QuestionsSnapshotJson = "[{\"text\":\"Sual 1\",\"points\":10,\"orderIndex\":1,\"options\":[{\"text\":\"A\",\"isCorrect\":true},{\"text\":\"B\",\"isCorrect\":false}]}]"
        };

        context.QuizAssignments.Add(expiredAssignment);
        await context.SaveChangesAsync();

        // Act
        var result = await assignmentService.GetAssignmentForTakingAsync(expiredAssignment.Id, studentId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("QuizAssignment.DeadlinePassed", result.Error.Code);
    }

    [Fact]
    public async Task SubmitAssignment_CalculatesScoreAndPassStatus()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var assignmentValidator = new CreateQuizAssignmentValidator();
        var legacyQuizMock = new Mock<IQuizService>();
        var assignmentService = new QuizAssignmentService(context, assignmentValidator, legacyQuizMock.Object);

        var studentId = Guid.NewGuid();
        var assignment = new QuizAssignment
        {
            TemplateId = Guid.NewGuid(),
            TeacherId = Guid.NewGuid(),
            Title = "Canlı Sınaq",
            IsActive = true,
            PassingScorePercentage = 70,
            QuestionsSnapshotJson = "[{\"text\":\"Sual 1\",\"points\":10,\"orderIndex\":1,\"options\":[{\"text\":\"A\",\"isCorrect\":true},{\"text\":\"B\",\"isCorrect\":false}]}]"
        };

        context.QuizAssignments.Add(assignment);
        await context.SaveChangesAsync();

        var submitDto = new SubmitQuizRequestDto(new List<SubmitQuizAnswerDto>
        {
            new(Guid.NewGuid(), Guid.NewGuid(), new List<Guid> { Guid.NewGuid() })
        });

        // Act
        var result = await assignmentService.SubmitAssignmentAsync(assignment.Id, studentId, submitDto);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(100.0, result.Value.ScorePercentage);
        Assert.True(result.Value.IsPassed);

        var savedAttempt = await context.QuizAttempts.FirstOrDefaultAsync(a => a.QuizAssignmentId == assignment.Id);
        Assert.NotNull(savedAttempt);
        Assert.Equal(studentId, savedAttempt.StudentId);
        Assert.Equal(assignment.Id, savedAttempt.QuizAssignmentId);
    }
}
