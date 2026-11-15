using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pragma.Application.Common;
using Pragma.Application.Contracts;
using Pragma.Application.DTOs.LessonQa;
using Pragma.Application.DTOs.Notifications;
using Pragma.Domain.Entities;
using Pragma.Domain.Enums;

namespace Pragma.Application.Services;

public class LessonQaService : ILessonQaService
{
    private readonly IAppDbContext _context;
    private readonly INotificationService _notificationService;
    private readonly IUserLookupService _userLookup;
    private readonly ILogger<LessonQaService> _logger;

    public LessonQaService(
        IAppDbContext context,
        INotificationService notificationService,
        IUserLookupService userLookup,
        ILogger<LessonQaService> logger)
    {
        _context = context;
        _notificationService = notificationService;
        _userLookup = userLookup;
        _logger = logger;
    }

    public async Task<Result<PagedResult<LessonQuestionDto>>> GetLessonQuestionsAsync(
        Guid lessonId,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var query = _context.LessonQuestions
            .Include(q => q.Answers)
            .AsNoTracking()
            .Where(q => q.LessonId == lessonId);

        var totalCount = await query.CountAsync(ct);

        var questions = await query
            .OrderBy(q => q.IsResolved)
            .ThenByDescending(q => q.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        // İstifadəçi adlarını topla
        var userIds = questions.Select(q => q.StudentId)
            .Concat(questions.SelectMany(q => q.Answers.Select(a => a.AuthorId)))
            .Distinct()
            .ToList();

        var users = await _userLookup.GetUsersByIdsAsync(userIds, ct);

        var dtos = questions.Select(q => new LessonQuestionDto(
            q.Id,
            q.LessonId,
            q.StudentId,
            users.TryGetValue(q.StudentId, out var s) ? s.FullName : null,
            q.Title,
            q.Content,
            q.PositionSeconds,
            q.IsResolved,
            q.Answers.Count(a => !a.IsDeleted),
            q.CreatedAtUtc,
            q.Answers
                .Where(a => !a.IsDeleted)
                .OrderBy(a => a.CreatedAtUtc)
                .Select(a => new LessonAnswerDto(
                    a.Id,
                    a.QuestionId,
                    a.AuthorId,
                    users.TryGetValue(a.AuthorId, out var au) ? au.FullName : null,
                    a.Content,
                    a.IsInstructorAnswer,
                    a.CreatedAtUtc
                )).ToList()
        )).ToList();

        return Result<PagedResult<LessonQuestionDto>>.Success(
            PagedResult<LessonQuestionDto>.Create(dtos, totalCount, page, pageSize));
    }

    public async Task<Result<LessonQuestionDto>> AskQuestionAsync(
        Guid lessonId,
        Guid studentId,
        CreateLessonQuestionDto dto,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Title))
            return Result<LessonQuestionDto>.Failure(Error.Validation("Sualın başlığı boş ola bilməz."));

        if (string.IsNullOrWhiteSpace(dto.Content))
            return Result<LessonQuestionDto>.Failure(Error.Validation("Sualın mətni boş ola bilməz."));

        var lessonExists = await _context.Lessons
            .AnyAsync(l => l.Id == lessonId, ct);

        if (!lessonExists)
            return Result<LessonQuestionDto>.Failure(Error.NotFound("Dərs tapılmadı."));

        var question = new LessonQuestion
        {
            LessonId = lessonId,
            StudentId = studentId,
            Title = dto.Title.Trim(),
            Content = dto.Content.Trim(),
            PositionSeconds = dto.PositionSeconds,
            IsResolved = false
        };

        _context.LessonQuestions.Add(question);
        await _context.SaveChangesAsync(ct);

        var student = await _userLookup.GetUserByIdAsync(studentId, ct);

        var resultDto = new LessonQuestionDto(
            question.Id,
            question.LessonId,
            question.StudentId,
            student?.FullName,
            question.Title,
            question.Content,
            question.PositionSeconds,
            question.IsResolved,
            0,
            question.CreatedAtUtc,
            Array.Empty<LessonAnswerDto>()
        );

        return Result<LessonQuestionDto>.Success(resultDto);
    }

    public async Task<Result<LessonAnswerDto>> AnswerQuestionAsync(
        Guid questionId,
        Guid authorId,
        bool isInstructor,
        CreateLessonAnswerDto dto,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Content))
            return Result<LessonAnswerDto>.Failure(Error.Validation("Cavabın mətni boş ola bilməz."));

        var question = await _context.LessonQuestions
            .FirstOrDefaultAsync(q => q.Id == questionId, ct);

        if (question == null)
            return Result<LessonAnswerDto>.Failure(Error.NotFound("Sual tapılmadı."));

        var answer = new LessonAnswer
        {
            QuestionId = questionId,
            AuthorId = authorId,
            Content = dto.Content.Trim(),
            IsInstructorAnswer = isInstructor
        };

        _context.LessonAnswers.Add(answer);
        await _context.SaveChangesAsync(ct);

        // Əgər cavab verən sualın müəllifi deyilsə, tələbəyə bildiriş göndər
        if (authorId != question.StudentId)
        {
            try
            {
                var roleName = isInstructor ? "Müəllim" : "İstifadəçi";
                await _notificationService.CreateNotificationAsync(new CreateNotificationDto(
                    UserId: question.StudentId,
                    Title: "Sualınıza yeni cavab verildi",
                    Message: $"{roleName} sizin \"{question.Title}\" sualınıza cavab yazdı.",
                    Type: NotificationType.LessonQa,
                    TargetUrl: $"/portal/lesson.html?id={question.LessonId}"
                ), ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Sual cavablandıqda bildiriş yaradılarkən xəta baş verdi. QuestionId: {Id}", questionId);
            }
        }

        var author = await _userLookup.GetUserByIdAsync(authorId, ct);

        var resultDto = new LessonAnswerDto(
            answer.Id,
            answer.QuestionId,
            answer.AuthorId,
            author?.FullName,
            answer.Content,
            answer.IsInstructorAnswer,
            answer.CreatedAtUtc
        );

        return Result<LessonAnswerDto>.Success(resultDto);
    }

    public async Task<Result<bool>> ResolveQuestionAsync(
        Guid questionId,
        Guid currentUserId,
        bool isInstructor,
        CancellationToken ct = default)
    {
        var question = await _context.LessonQuestions
            .FirstOrDefaultAsync(q => q.Id == questionId, ct);

        if (question == null)
            return Result<bool>.Failure(Error.NotFound("Sual tapılmadı."));

        if (!isInstructor && question.StudentId != currentUserId)
            return Result<bool>.Failure(Error.Forbidden("Yalnız sualı verən tələbə və ya müəllim sualı həll edilmiş kimi qeyd edə bilər."));

        question.IsResolved = !question.IsResolved;
        await _context.SaveChangesAsync(ct);

        return Result<bool>.Success(question.IsResolved);
    }
}
