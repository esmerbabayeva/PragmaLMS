using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Pragma.Application.Common;
using Pragma.Application.Contracts;
using Pragma.Application.DTOs.Quizzes;
using Pragma.Domain.Entities;
using Pragma.Domain.Enums;

namespace Pragma.Application.Services;

public class QuizService : IQuizService
{
    private readonly IAppDbContext _context;
    private readonly ICourseAccessService _accessService;
    private readonly IValidator<CreateQuizDto> _createValidator;

    public QuizService(
        IAppDbContext context,
        ICourseAccessService accessService,
        IValidator<CreateQuizDto> createValidator)
    {
        _context = context;
        _accessService = accessService;
        _createValidator = createValidator;
    }

    public async Task<Result<Guid>> CreateQuizAsync(Guid lessonId, CreateQuizDto dto, Guid userId, bool isAdmin, CancellationToken cancellationToken = default)
    {
        var validation = await _createValidator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<Guid>.Failure(Error.Validation("Quiz.Validation", string.Join("; ", validation.Errors.Select(e => e.ErrorMessage))));
        }

        var lesson = await _context.Lessons
            .Include(l => l.Module)
            .FirstOrDefaultAsync(l => l.Id == lessonId, cancellationToken);

        if (lesson == null)
        {
            return Result<Guid>.Failure(Error.NotFound("Lesson.NotFound", "Dərs tapılmadı."));
        }

        var canManage = await _accessService.CanManageCourseAsync(lesson.Module!.CourseId, userId, isAdmin, cancellationToken);
        if (!canManage)
        {
            return Result<Guid>.Failure(Error.Forbidden("Course.Forbidden", "Bu dərsə quiz əlavə etmək icazəniz yoxdur."));
        }

        var existingQuiz = await _context.Quizzes.AnyAsync(q => q.LessonId == lessonId, cancellationToken);
        if (existingQuiz)
        {
            return Result<Guid>.Failure(Error.Conflict("Quiz.AlreadyExists", "Bu dərsə artıq bir quiz bağlanıb."));
        }

        var quiz = new Quiz
        {
            LessonId = lessonId,
            Title = dto.Title.Trim(),
            Description = dto.Description,
            PassingScorePercentage = dto.PassingScorePercentage,
            TimeLimitMinutes = dto.TimeLimitMinutes,
            MaxAttempts = dto.MaxAttempts
        };

        foreach (var qDto in dto.Questions)
        {
            var question = new Question
            {
                Text = qDto.Text.Trim(),
                Points = qDto.Points,
                OrderIndex = qDto.OrderIndex,
                Explanation = qDto.Explanation
            };

            foreach (var optDto in qDto.Options)
            {
                question.Options.Add(new AnswerOption
                {
                    Text = optDto.Text.Trim(),
                    IsCorrect = optDto.IsCorrect
                });
            }

            quiz.Questions.Add(question);
        }

        _context.Quizzes.Add(quiz);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(quiz.Id);
    }

    public async Task<Result<QuizTakingDto>> GetQuizForTakingAsync(Guid quizId, Guid studentId, CancellationToken cancellationToken = default)
    {
        var quiz = await _context.Quizzes
            .AsNoTracking()
            .Include(q => q.Lesson)
            .ThenInclude(l => l!.Module)
            .Include(q => q.Questions.OrderBy(qu => qu.OrderIndex))
            .ThenInclude(qu => qu.Options)
            .FirstOrDefaultAsync(q => q.Id == quizId, cancellationToken);

        if (quiz == null)
        {
            return Result<QuizTakingDto>.Failure(Error.NotFound("Quiz.NotFound", "Quiz tapılmadı."));
        }

        // Tələbənin dərsə giriş icazəsi
        var canAccess = await _accessService.CanAccessLessonAsync(quiz.LessonId, studentId, cancellationToken);
        if (!canAccess)
        {
            return Result<QuizTakingDto>.Failure(Error.Forbidden("Quiz.Forbidden", "Bu quiz-də iştirak etmək üçün kursa qeydiyyatdan keçməlisiniz."));
        }

        // Cəhd limiti yoxlanışı
        var attemptCount = await _context.QuizAttempts
            .AsNoTracking()
            .CountAsync(a => a.QuizId == quizId && a.StudentId == studentId && a.Status == QuizAttemptStatus.Completed, cancellationToken);

        if (attemptCount >= quiz.MaxAttempts)
        {
            return Result<QuizTakingDto>.Failure(Error.Forbidden("Quiz.MaxAttempts", $"Siz bu quiz üçün təyin edilmiş maksimum cəhd sayını ({quiz.MaxAttempts}) tamamlamısınız."));
        }

        // TƏHLÜKƏSİZLİK: IsCorrect sahəsi student frontend-inə göndərilmir!
        var questions = quiz.Questions.OrderBy(q => q.OrderIndex).Select(q => new QuestionTakingDto(
            q.Id,
            q.Text,
            q.Points,
            q.OrderIndex,
            q.Options.Select(o => new AnswerOptionTakingDto(o.Id, o.Text)).ToList()
        )).ToList();

        var takingDto = new QuizTakingDto(
            quiz.Id,
            quiz.LessonId,
            quiz.Title,
            quiz.Description,
            quiz.PassingScorePercentage,
            quiz.TimeLimitMinutes,
            quiz.MaxAttempts,
            RemainingAttempts: quiz.MaxAttempts - attemptCount,
            questions
        );

        return Result<QuizTakingDto>.Success(takingDto);
    }

    public async Task<Result<QuizResultDto>> SubmitQuizAsync(Guid quizId, Guid studentId, SubmitQuizRequestDto dto, CancellationToken cancellationToken = default)
    {
        var quiz = await _context.Quizzes
            .Include(q => q.Questions)
            .ThenInclude(qu => qu.Options)
            .FirstOrDefaultAsync(q => q.Id == quizId, cancellationToken);

        if (quiz == null)
        {
            return Result<QuizResultDto>.Failure(Error.NotFound("Quiz.NotFound", "Quiz tapılmadı."));
        }

        var attemptCount = await _context.QuizAttempts
            .CountAsync(a => a.QuizId == quizId && a.StudentId == studentId && a.Status == QuizAttemptStatus.Completed, cancellationToken);

        if (attemptCount >= quiz.MaxAttempts)
        {
            return Result<QuizResultDto>.Failure(Error.Forbidden("Quiz.MaxAttempts", "Maksimum cəhd limitiniz bitmişdir."));
        }

        int totalQuestions = quiz.Questions.Count;
        int correctAnswers = 0;
        int earnedPoints = 0;
        int totalPoints = quiz.Questions.Sum(q => q.Points);

        var reviews = new List<QuestionReviewDto>();
        var attempt = new QuizAttempt
        {
            QuizId = quizId,
            StudentId = studentId,
            AttemptNumber = attemptCount + 1,
            TotalQuestions = totalQuestions,
            TotalPoints = totalPoints,
            StartedAtUtc = DateTime.UtcNow,
            CompletedAtUtc = DateTime.UtcNow,
            Status = QuizAttemptStatus.Completed
        };

        foreach (var question in quiz.Questions)
        {
            var studentAnswer = dto.Answers.FirstOrDefault(a => a.QuestionId == question.Id);
            var selectedOptionId = studentAnswer?.SelectedOptionId;

            var correctOption = question.Options.FirstOrDefault(o => o.IsCorrect);
            bool isCorrect = selectedOptionId.HasValue && correctOption != null && selectedOptionId.Value == correctOption.Id;

            if (isCorrect)
            {
                correctAnswers++;
                earnedPoints += question.Points;
            }

            attempt.Answers.Add(new QuizAttemptAnswer
            {
                QuestionId = question.Id,
                SelectedOptionId = selectedOptionId,
                IsCorrect = isCorrect
            });

            reviews.Add(new QuestionReviewDto(
                question.Id,
                question.Text,
                selectedOptionId,
                correctOption?.Id,
                isCorrect,
                question.Explanation
            ));
        }

        double scorePercentage = totalPoints > 0 ? Math.Round((double)earnedPoints / totalPoints * 100, 1) : 0.0;
        bool isPassed = scorePercentage >= quiz.PassingScorePercentage;

        attempt.CorrectAnswers = correctAnswers;
        attempt.EarnedPoints = earnedPoints;
        attempt.ScorePercentage = scorePercentage;
        attempt.IsPassed = isPassed;

        _context.QuizAttempts.Add(attempt);
        await _context.SaveChangesAsync(cancellationToken);

        var resultDto = new QuizResultDto(
            attempt.Id,
            quiz.Id,
            attempt.AttemptNumber,
            totalQuestions,
            correctAnswers,
            earnedPoints,
            totalPoints,
            scorePercentage,
            isPassed,
            attempt.Status,
            attempt.CompletedAtUtc.Value,
            reviews
        );

        return Result<QuizResultDto>.Success(resultDto);
    }

    public async Task<IReadOnlyCollection<QuizAttemptSummaryDto>> GetMyAttemptsAsync(Guid quizId, Guid studentId, CancellationToken cancellationToken = default)
    {
        return await _context.QuizAttempts
            .AsNoTracking()
            .Where(a => a.QuizId == quizId && a.StudentId == studentId && a.Status == QuizAttemptStatus.Completed)
            .OrderByDescending(a => a.AttemptNumber)
            .Select(a => new QuizAttemptSummaryDto(
                a.Id,
                a.AttemptNumber,
                a.ScorePercentage,
                a.IsPassed,
                a.CompletedAtUtc ?? a.CreatedAtUtc
            ))
            .ToListAsync(cancellationToken);
    }
}
