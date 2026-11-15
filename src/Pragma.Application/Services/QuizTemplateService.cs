using System.Text.Encodings.Web;
using System.Text.Json;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Pragma.Application.Common;
using Pragma.Application.Contracts;
using Pragma.Application.DTOs.Quizzes;
using Pragma.Domain.Entities;

namespace Pragma.Application.Services;

public class QuizTemplateService : IQuizTemplateService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly IAppDbContext _context;
    private readonly IValidator<CreateQuizTemplateDto> _createValidator;

    public QuizTemplateService(
        IAppDbContext context,
        IValidator<CreateQuizTemplateDto> createValidator)
    {
        _context = context;
        _createValidator = createValidator;
    }

    public async Task<Result<Guid>> CreateTemplateAsync(CreateQuizTemplateDto dto, Guid teacherId, bool isAdmin, CancellationToken ct = default)
    {
        var validation = await _createValidator.ValidateAsync(dto, ct);
        if (!validation.IsValid)
        {
            return Result<Guid>.Failure(Error.Validation("QuizTemplate.Validation", string.Join("; ", validation.Errors.Select(e => e.ErrorMessage))));
        }

        var questionsJson = JsonSerializer.Serialize(dto.Questions, JsonOptions);

        var template = new QuizTemplate
        {
            TeacherId = teacherId,
            Title = dto.Title.Trim(),
            Description = dto.Description?.Trim(),
            DefaultTimeLimitMinutes = dto.DefaultTimeLimitMinutes,
            DefaultPassingScorePercentage = dto.DefaultPassingScorePercentage,
            DefaultPointsPerQuestion = dto.DefaultPointsPerQuestion,
            DefaultMaxAttempts = dto.DefaultMaxAttempts,
            CourseId = dto.CourseId,
            QuestionsJson = questionsJson
        };

        _context.QuizTemplates.Add(template);
        await _context.SaveChangesAsync(ct);

        return Result<Guid>.Success(template.Id);
    }

    public async Task<Result<IReadOnlyCollection<QuizTemplateSummaryDto>>> GetTemplatesAsync(Guid teacherId, bool isAdmin, Guid? courseId = null, CancellationToken ct = default)
    {
        var query = _context.QuizTemplates
            .AsNoTracking()
            .Include(t => t.Course)
            .Include(t => t.Assignments.Where(a => !a.IsDeleted))
            .Where(t => !t.IsDeleted);

        if (!isAdmin)
        {
            query = query.Where(t => t.TeacherId == teacherId);
        }

        if (courseId.HasValue && courseId.Value != Guid.Empty)
        {
            query = query.Where(t => t.CourseId == courseId.Value);
        }

        var templates = await query
            .OrderByDescending(t => t.CreatedAtUtc)
            .ToListAsync(ct);

        var result = templates.Select(t =>
        {
            var questions = DeserializeQuestions(t.QuestionsJson);
            var questionCount = questions.Count;
            var totalPoints = questions.Sum(q => q.Points);

            return new QuizTemplateSummaryDto(
                t.Id,
                t.TeacherId,
                t.Title,
                t.Description,
                t.DefaultTimeLimitMinutes,
                t.DefaultPassingScorePercentage,
                t.DefaultPointsPerQuestion,
                t.DefaultMaxAttempts,
                t.CourseId,
                t.Course?.Title,
                questionCount,
                totalPoints,
                t.Assignments.Count,
                t.CreatedAtUtc
            );
        }).ToList();

        return Result<IReadOnlyCollection<QuizTemplateSummaryDto>>.Success(result);
    }

    public async Task<Result<QuizTemplateDto>> GetTemplateByIdAsync(Guid id, Guid teacherId, bool isAdmin, CancellationToken ct = default)
    {
        var template = await _context.QuizTemplates
            .AsNoTracking()
            .Include(t => t.Course)
            .FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted, ct);

        if (template == null)
        {
            return Result<QuizTemplateDto>.Failure(Error.NotFound("QuizTemplate.NotFound", "Quiz şablonu tapılmadı."));
        }

        if (!isAdmin && template.TeacherId != teacherId)
        {
            return Result<QuizTemplateDto>.Failure(Error.Forbidden("QuizTemplate.Forbidden", "Bu şablona baxmaq səlahiyyətiniz yoxdur."));
        }

        var questions = DeserializeQuestions(template.QuestionsJson);

        var dto = new QuizTemplateDto(
            template.Id,
            template.TeacherId,
            template.Title,
            template.Description,
            template.DefaultTimeLimitMinutes,
            template.DefaultPassingScorePercentage,
            template.DefaultPointsPerQuestion,
            template.DefaultMaxAttempts,
            template.CourseId,
            template.Course?.Title,
            questions.Count,
            questions.Sum(q => q.Points),
            template.CreatedAtUtc,
            template.UpdatedAtUtc,
            questions
        );

        return Result<QuizTemplateDto>.Success(dto);
    }

    public async Task<Result<Guid>> UpdateTemplateAsync(Guid id, UpdateQuizTemplateDto dto, Guid teacherId, bool isAdmin, CancellationToken ct = default)
    {
        var template = await _context.QuizTemplates
            .FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted, ct);

        if (template == null)
        {
            return Result<Guid>.Failure(Error.NotFound("QuizTemplate.NotFound", "Yenilənəcək şablon tapılmadı."));
        }

        if (!isAdmin && template.TeacherId != teacherId)
        {
            return Result<Guid>.Failure(Error.Forbidden("QuizTemplate.Forbidden", "Bu şablonu redaktə etmək səlahiyyətiniz yoxdur."));
        }

        if (string.IsNullOrWhiteSpace(dto.Title))
        {
            return Result<Guid>.Failure(Error.Validation("QuizTemplate.Title", "Şablon başlığı boş ola bilməz."));
        }

        if (dto.Questions == null || dto.Questions.Count == 0)
        {
            return Result<Guid>.Failure(Error.Validation("QuizTemplate.Questions", "Ən azı 1 sual daxil edilməlidir."));
        }

        template.Title = dto.Title.Trim();
        template.Description = dto.Description?.Trim();
        template.DefaultTimeLimitMinutes = dto.DefaultTimeLimitMinutes;
        template.DefaultPassingScorePercentage = dto.DefaultPassingScorePercentage;
        template.DefaultPointsPerQuestion = dto.DefaultPointsPerQuestion;
        template.DefaultMaxAttempts = dto.DefaultMaxAttempts;
        template.CourseId = dto.CourseId;
        template.QuestionsJson = JsonSerializer.Serialize(dto.Questions, JsonOptions);

        await _context.SaveChangesAsync(ct);
        return Result<Guid>.Success(template.Id);
    }

    public async Task<Result<bool>> DeleteTemplateAsync(Guid id, Guid teacherId, bool isAdmin, CancellationToken ct = default)
    {
        var template = await _context.QuizTemplates
            .FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted, ct);

        if (template == null)
        {
            return Result<bool>.Failure(Error.NotFound("QuizTemplate.NotFound", "Silinəcək şablon tapılmadı."));
        }

        if (!isAdmin && template.TeacherId != teacherId)
        {
            return Result<bool>.Failure(Error.Forbidden("QuizTemplate.Forbidden", "Bu şablonu silmək səlahiyyətiniz yoxdur."));
        }

        template.MarkDeleted();
        await _context.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
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
