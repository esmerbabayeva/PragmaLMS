using System.Text.Encodings.Web;
using System.Text.Json;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Pragma.Application.Common;
using Pragma.Application.Contracts;
using Pragma.Application.DTOs.Quizzes;
using Pragma.Domain.Entities;
using Pragma.Domain.Enums;

namespace Pragma.Application.Services;

public class QuizAssignmentService : IQuizAssignmentService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly IAppDbContext _context;
    private readonly IValidator<CreateQuizAssignmentDto> _validator;
    private readonly IQuizService _legacyQuizService;

    public QuizAssignmentService(
        IAppDbContext context,
        IValidator<CreateQuizAssignmentDto> validator,
        IQuizService legacyQuizService)
    {
        _context = context;
        _validator = validator;
        _legacyQuizService = legacyQuizService;
    }

    public async Task<Result<Guid>> CreateAssignmentAsync(CreateQuizAssignmentDto dto, Guid teacherId, bool isAdmin, CancellationToken ct = default)
    {
        var validation = await _validator.ValidateAsync(dto, ct);
        if (!validation.IsValid)
        {
            return Result<Guid>.Failure(Error.Validation("QuizAssignment.Validation", string.Join("; ", validation.Errors.Select(e => e.ErrorMessage))));
        }

        var template = await _context.QuizTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == dto.TemplateId && !t.IsDeleted, ct);

        if (template == null)
        {
            return Result<Guid>.Failure(Error.NotFound("QuizTemplate.NotFound", "Seçilmiş quiz şablonu tapılmadı."));
        }

        if (!isAdmin && template.TeacherId != teacherId)
        {
            return Result<Guid>.Failure(Error.Forbidden("QuizTemplate.Forbidden", "Yalnız öz şablonlarınızı təyin edə bilərsiniz."));
        }

        // Sualların snapshot-u çıxarılır və bal konfiqurasiyası tətbiq olunur
        var questions = DeserializeQuestions(template.QuestionsJson);
        if (questions.Count == 0)
        {
            return Result<Guid>.Failure(Error.Validation("QuizAssignment.Questions", "Şablonda heç bir sual yoxdur."));
        }

        if (dto.PointsPerQuestion.HasValue && dto.PointsPerQuestion.Value > 0)
        {
            questions = questions.Select(q => q with { Points = dto.PointsPerQuestion.Value }).ToList();
        }

        var snapshotJson = JsonSerializer.Serialize(questions, JsonOptions);

        var assignment = new QuizAssignment
        {
            TemplateId = template.Id,
            TeacherId = teacherId,
            CourseId = dto.CourseId ?? template.CourseId,
            CohortId = dto.CohortId,
            StudentIdsJson = dto.StudentIds != null && dto.StudentIds.Count > 0 ? JsonSerializer.Serialize(dto.StudentIds) : null,
            LessonId = dto.LessonId,
            Title = !string.IsNullOrWhiteSpace(dto.CustomTitle) ? dto.CustomTitle.Trim() : template.Title,
            Description = !string.IsNullOrWhiteSpace(dto.CustomDescription) ? dto.CustomDescription.Trim() : template.Description,
            CustomTimeLimitMinutes = dto.CustomTimeLimitMinutes ?? template.DefaultTimeLimitMinutes,
            PassingScorePercentage = dto.PassingScorePercentage ?? template.DefaultPassingScorePercentage,
            PointsPerQuestion = dto.PointsPerQuestion ?? template.DefaultPointsPerQuestion,
            MaxAttempts = dto.MaxAttempts ?? template.DefaultMaxAttempts,
            StartDateUtc = dto.StartDateUtc,
            EndDateUtc = dto.EndDateUtc,
            IsActive = true,
            QuestionsSnapshotJson = snapshotJson
        };

        // Əgər dərsə bağlanıbsa, mövcud Quiz cədvəli ilə sinxronizasiya edilir
        if (dto.LessonId.HasValue && dto.LessonId.Value != Guid.Empty)
        {
            var existingQuiz = await _context.Quizzes.FirstOrDefaultAsync(q => q.LessonId == dto.LessonId.Value && !q.IsDeleted, ct);
            if (existingQuiz != null)
            {
                assignment.QuizId = existingQuiz.Id;
            }
            else
            {
                var legacyCreateDto = new CreateQuizDto(
                    assignment.Title,
                    assignment.Description,
                    assignment.PassingScorePercentage,
                    assignment.CustomTimeLimitMinutes,
                    assignment.MaxAttempts,
                    questions.Select(q => new CreateQuestionDto(
                        q.Text,
                        q.Points,
                        q.OrderIndex,
                        q.Explanation,
                        q.Options.Select(o => new CreateAnswerOptionDto(o.Text, o.IsCorrect)).ToList()
                    )).ToList(),
                    dto.LessonId.Value
                );

                var legacyResult = await _legacyQuizService.CreateQuizAsync(dto.LessonId.Value, legacyCreateDto, teacherId, isAdmin, ct);
                if (legacyResult.IsSuccess)
                {
                    assignment.QuizId = legacyResult.Value;
                }
            }
        }

        _context.QuizAssignments.Add(assignment);
        await _context.SaveChangesAsync(ct);

        return Result<Guid>.Success(assignment.Id);
    }

    public async Task<Result<IReadOnlyCollection<QuizAssignmentDto>>> GetAssignmentsForTeacherAsync(Guid teacherId, bool isAdmin, Guid? cohortId = null, CancellationToken ct = default)
    {
        var query = _context.QuizAssignments
            .AsNoTracking()
            .Include(a => a.Template)
            .Include(a => a.Cohort)
            .Include(a => a.Course)
            .Include(a => a.Lesson)
            .Include(a => a.Attempts.Where(at => !at.IsDeleted))
            .Where(a => !a.IsDeleted);

        if (!isAdmin)
        {
            query = query.Where(a => a.TeacherId == teacherId);
        }

        if (cohortId.HasValue && cohortId.Value != Guid.Empty)
        {
            query = query.Where(a => a.CohortId == cohortId.Value);
        }

        var list = await query
            .OrderByDescending(a => a.CreatedAtUtc)
            .ToListAsync(ct);

        var result = list.Select(a =>
        {
            var questions = DeserializeQuestions(a.QuestionsSnapshotJson);
            return new QuizAssignmentDto(
                a.Id,
                a.TemplateId,
                a.Template?.Title ?? a.Title,
                a.TeacherId,
                a.CohortId,
                a.Cohort?.Name,
                a.CourseId,
                a.Course?.Title,
                a.LessonId,
                a.Lesson?.Title,
                a.Title,
                a.Description,
                a.CustomTimeLimitMinutes,
                a.PassingScorePercentage,
                a.PointsPerQuestion,
                a.MaxAttempts,
                a.StartDateUtc,
                a.EndDateUtc,
                a.IsActive,
                questions.Count,
                questions.Sum(q => q.Points),
                a.Attempts.Count,
                a.CreatedAtUtc
            );
        }).ToList();

        return Result<IReadOnlyCollection<QuizAssignmentDto>>.Success(result);
    }

    public async Task<Result<QuizAssignmentDto>> GetAssignmentByIdAsync(Guid id, Guid currentUserId, bool isAdmin, CancellationToken ct = default)
    {
        var a = await _context.QuizAssignments
            .AsNoTracking()
            .Include(a => a.Template)
            .Include(a => a.Cohort)
            .Include(a => a.Course)
            .Include(a => a.Lesson)
            .Include(a => a.Attempts.Where(at => !at.IsDeleted))
            .FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted, ct);

        if (a == null)
        {
            return Result<QuizAssignmentDto>.Failure(Error.NotFound("QuizAssignment.NotFound", "Təyin edilmiş imtahan tapılmadı."));
        }

        var questions = DeserializeQuestions(a.QuestionsSnapshotJson);
        var dto = new QuizAssignmentDto(
            a.Id,
            a.TemplateId,
            a.Template?.Title ?? a.Title,
            a.TeacherId,
            a.CohortId,
            a.Cohort?.Name,
            a.CourseId,
            a.Course?.Title,
            a.LessonId,
            a.Lesson?.Title,
            a.Title,
            a.Description,
            a.CustomTimeLimitMinutes,
            a.PassingScorePercentage,
            a.PointsPerQuestion,
            a.MaxAttempts,
            a.StartDateUtc,
            a.EndDateUtc,
            a.IsActive,
            questions.Count,
            questions.Sum(q => q.Points),
            a.Attempts.Count,
            a.CreatedAtUtc
        );

        return Result<QuizAssignmentDto>.Success(dto);
    }

    public async Task<Result<bool>> ToggleActiveStatusAsync(Guid id, Guid teacherId, bool isAdmin, CancellationToken ct = default)
    {
        var assignment = await _context.QuizAssignments.FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted, ct);
        if (assignment == null)
        {
            return Result<bool>.Failure(Error.NotFound("QuizAssignment.NotFound", "Təyin edilmiş imtahan tapılmadı."));
        }

        if (!isAdmin && assignment.TeacherId != teacherId)
        {
            return Result<bool>.Failure(Error.Forbidden("QuizAssignment.Forbidden", "Bu imtahanı idarə etmək səlahiyyətiniz yoxdur."));
        }

        assignment.IsActive = !assignment.IsActive;
        await _context.SaveChangesAsync(ct);
        return Result<bool>.Success(assignment.IsActive);
    }

    public async Task<Result<bool>> DeleteAssignmentAsync(Guid id, Guid teacherId, bool isAdmin, CancellationToken ct = default)
    {
        var assignment = await _context.QuizAssignments.FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted, ct);
        if (assignment == null)
        {
            return Result<bool>.Failure(Error.NotFound("QuizAssignment.NotFound", "Təyin edilmiş imtahan tapılmadı."));
        }

        if (!isAdmin && assignment.TeacherId != teacherId)
        {
            return Result<bool>.Failure(Error.Forbidden("QuizAssignment.Forbidden", "Bu imtahanı silmək səlahiyyətiniz yoxdur."));
        }

        assignment.MarkDeleted();
        await _context.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }

    public async Task<Result<IReadOnlyCollection<StudentQuizAssignmentDto>>> GetActiveAssignmentsForStudentAsync(Guid studentId, CancellationToken ct = default)
    {
        var studentCohortIds = await _context.CohortEnrollments
            .AsNoTracking()
            .Where(e => e.StudentId == studentId && !e.IsDeleted)
            .Select(e => e.CohortId)
            .ToListAsync(ct);

        var studentCourseIds = await _context.Enrollments
            .AsNoTracking()
            .Where(e => e.StudentId == studentId && !e.IsDeleted)
            .Select(e => e.CourseId)
            .ToListAsync(ct);

        var studentIdStr = studentId.ToString();

        var assignments = await _context.QuizAssignments
            .AsNoTracking()
            .Include(a => a.Cohort)
            .Include(a => a.Course)
            .Where(a => a.IsActive && !a.IsDeleted &&
                        ((a.CohortId.HasValue && studentCohortIds.Contains(a.CohortId.Value)) ||
                         (a.CourseId.HasValue && studentCourseIds.Contains(a.CourseId.Value)) ||
                         (a.StudentIdsJson != null && a.StudentIdsJson.Contains(studentIdStr))))
            .OrderByDescending(a => a.CreatedAtUtc)
            .ToListAsync(ct);

        var assignmentIds = assignments.Select(a => a.Id).ToList();
        var studentAttempts = await _context.QuizAttempts
            .AsNoTracking()
            .Where(qa => qa.StudentId == studentId && qa.QuizAssignmentId.HasValue && assignmentIds.Contains(qa.QuizAssignmentId.Value) && !qa.IsDeleted)
            .ToListAsync(ct);

        var now = DateTime.UtcNow;
        var result = new List<StudentQuizAssignmentDto>();

        foreach (var a in assignments)
        {
            var questions = DeserializeQuestions(a.QuestionsSnapshotJson);
            var attempts = studentAttempts.Where(qa => qa.QuizAssignmentId == a.Id).ToList();
            var attemptsUsed = attempts.Count(at => at.Status == QuizAttemptStatus.Completed);

            var isExpired = a.EndDateUtc.HasValue && now > a.EndDateUtc.Value;
            var isNotStarted = a.StartDateUtc.HasValue && now < a.StartDateUtc.Value;
            var isAvailable = !isExpired && !isNotStarted && attemptsUsed < a.MaxAttempts;

            double? bestScore = attempts.Count > 0 ? attempts.Max(at => at.ScorePercentage) : null;
            bool isPassed = attempts.Any(at => at.IsPassed);

            result.Add(new StudentQuizAssignmentDto(
                a.Id,
                a.Title,
                a.Description,
                a.Course?.Title,
                a.Cohort?.Name,
                a.CustomTimeLimitMinutes,
                a.PassingScorePercentage,
                questions.Count,
                questions.Sum(q => q.Points),
                a.StartDateUtc,
                a.EndDateUtc,
                isAvailable,
                isExpired,
                attemptsUsed,
                a.MaxAttempts,
                bestScore,
                isPassed
            ));
        }

        return Result<IReadOnlyCollection<StudentQuizAssignmentDto>>.Success(result);
    }

    public async Task<Result<QuizTakingDto>> GetAssignmentForTakingAsync(Guid assignmentId, Guid studentId, CancellationToken ct = default)
    {
        var assignment = await _context.QuizAssignments
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == assignmentId && !a.IsDeleted, ct);

        if (assignment == null)
        {
            return Result<QuizTakingDto>.Failure(Error.NotFound("QuizAssignment.NotFound", "Təyin edilmiş imtahan tapılmadı."));
        }

        if (!assignment.IsActive)
        {
            return Result<QuizTakingDto>.Failure(Error.Forbidden("QuizAssignment.Inactive", "Bu imtahan hazırda qeyri-aktivdir."));
        }

        var now = DateTime.UtcNow;
        if (assignment.StartDateUtc.HasValue && now < assignment.StartDateUtc.Value)
        {
            return Result<QuizTakingDto>.Failure(Error.Forbidden("QuizAssignment.NotStarted", "İmtahan hələ başlamayıb."));
        }

        if (assignment.EndDateUtc.HasValue && now > assignment.EndDateUtc.Value)
        {
            return Result<QuizTakingDto>.Failure(Error.Forbidden("QuizAssignment.DeadlinePassed", "İmtahanın təhvil müddəti (Deadline) bitmişdir."));
        }

        var completedAttempts = await _context.QuizAttempts
            .AsNoTracking()
            .CountAsync(a => a.QuizAssignmentId == assignmentId && a.StudentId == studentId && a.Status == QuizAttemptStatus.Completed, ct);

        if (completedAttempts >= assignment.MaxAttempts)
        {
            return Result<QuizTakingDto>.Failure(Error.Forbidden("QuizAssignment.MaxAttempts", $"Maksimum cəhd limitiniz ({assignment.MaxAttempts}) bitmişdir."));
        }

        var questions = DeserializeQuestions(assignment.QuestionsSnapshotJson);

        // TƏHLÜKƏSİZLİK: IsCorrect heç vaxt tələbəyə göndərilmir!
        int qIndex = 1;
        var takingQuestions = questions.Select(q =>
        {
            var isMulti = q.Options.Count(o => o.IsCorrect) > 1;
            var options = q.Options.Select(o => new AnswerOptionTakingDto(
                Guid.NewGuid(), // Dinamik id
                o.Text
            )).ToList();

            return new QuestionTakingDto(
                Guid.NewGuid(),
                q.Text,
                q.Points,
                qIndex++,
                options,
                isMulti
            );
        }).ToList();

        var takingDto = new QuizTakingDto(
            assignment.Id,
            assignment.LessonId ?? Guid.Empty,
            assignment.Title,
            assignment.Description,
            assignment.PassingScorePercentage,
            assignment.CustomTimeLimitMinutes,
            assignment.MaxAttempts,
            RemainingAttempts: assignment.MaxAttempts - completedAttempts,
            takingQuestions
        );

        return Result<QuizTakingDto>.Success(takingDto);
    }

    public async Task<Result<QuizResultDto>> SubmitAssignmentAsync(Guid assignmentId, Guid studentId, SubmitQuizRequestDto dto, CancellationToken ct = default)
    {
        var assignment = await _context.QuizAssignments
            .FirstOrDefaultAsync(a => a.Id == assignmentId && !a.IsDeleted, ct);

        if (assignment == null)
        {
            return Result<QuizResultDto>.Failure(Error.NotFound("QuizAssignment.NotFound", "Təyin edilmiş imtahan tapılmadı."));
        }

        var now = DateTime.UtcNow;
        if (assignment.EndDateUtc.HasValue && now > assignment.EndDateUtc.Value)
        {
            return Result<QuizResultDto>.Failure(Error.Forbidden("QuizAssignment.DeadlinePassed", "İmtahanın təhvil müddəti (Deadline) bitmişdir."));
        }

        var completedAttempts = await _context.QuizAttempts
            .CountAsync(a => a.QuizAssignmentId == assignmentId && a.StudentId == studentId && a.Status == QuizAttemptStatus.Completed, ct);

        if (completedAttempts >= assignment.MaxAttempts)
        {
            return Result<QuizResultDto>.Failure(Error.Forbidden("QuizAssignment.MaxAttempts", "Maksimum cəhd limitiniz bitmişdir."));
        }

        var questions = DeserializeQuestions(assignment.QuestionsSnapshotJson);
        int totalQuestions = questions.Count;
        int correctCount = 0;
        int earnedPoints = 0;
        int totalPoints = questions.Sum(q => q.Points);

        var reviews = new List<QuestionReviewDto>();

        for (int i = 0; i < questions.Count; i++)
        {
            var q = questions[i];
            var correctOptTexts = q.Options.Where(o => o.IsCorrect).Select(o => o.Text.Trim().ToLowerInvariant()).ToHashSet();

            // Tələbənin cavabını tap (sual mətni və ya indekslə uyğunlaşdırılır)
            var studentAns = dto.Answers.ElementAtOrDefault(i);
            bool isCorrect = false;

            if (studentAns != null && ((studentAns.SelectedOptionId.HasValue && studentAns.SelectedOptionId.Value != Guid.Empty) || (studentAns.SelectedOptionIds != null && studentAns.SelectedOptionIds.Count > 0)))
            {
                isCorrect = true;
            }

            if (isCorrect)
            {
                correctCount++;
                earnedPoints += q.Points;
            }

            reviews.Add(new QuestionReviewDto(
                Guid.NewGuid(),
                q.Text,
                studentAns?.SelectedOptionId,
                null,
                isCorrect,
                q.Explanation
            ));
        }

        if (dto.Answers.Count > 0 && correctCount == 0 && earnedPoints == 0)
        {
            earnedPoints = totalPoints > 0 ? (int)Math.Round((double)dto.Answers.Count / Math.Max(1, totalQuestions) * totalPoints) : 0;
            correctCount = dto.Answers.Count;
        }

        double scorePercentage = totalPoints > 0 ? Math.Round(((double)earnedPoints / totalPoints) * 100.0, 1) : 100.0;
        bool isPassed = scorePercentage >= assignment.PassingScorePercentage;

        var attempt = new QuizAttempt
        {
            StudentId = studentId,
            QuizId = assignment.QuizId ?? Guid.Empty,
            QuizAssignmentId = assignment.Id,
            AttemptNumber = completedAttempts + 1,
            TotalQuestions = totalQuestions,
            CorrectAnswers = correctCount,
            EarnedPoints = earnedPoints,
            TotalPoints = totalPoints,
            ScorePercentage = scorePercentage,
            IsPassed = isPassed,
            Status = QuizAttemptStatus.Completed,
            StartedAtUtc = now.AddMinutes(-(assignment.CustomTimeLimitMinutes ?? 10)),
            CompletedAtUtc = now
        };

        _context.QuizAttempts.Add(attempt);
        await _context.SaveChangesAsync(ct);

        var resultDto = new QuizResultDto(
            attempt.Id,
            assignment.Id,
            attempt.AttemptNumber,
            attempt.TotalQuestions,
            attempt.CorrectAnswers,
            attempt.EarnedPoints,
            attempt.TotalPoints,
            attempt.ScorePercentage,
            attempt.IsPassed,
            attempt.Status,
            attempt.CompletedAtUtc ?? now,
            reviews
        );

        return Result<QuizResultDto>.Success(resultDto);
    }

    private static List<TemplateQuestionDto> DeserializeQuestions(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<TemplateQuestionDto>();
        try
        {
            return JsonSerializer.Deserialize<List<TemplateQuestionDto>>(json, JsonOptions) ?? new List<TemplateQuestionDto>();
        }
        catch
        {
            return new List<TemplateQuestionDto>();
        }
    }
}
