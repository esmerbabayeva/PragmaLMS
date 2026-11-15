using Pragma.Application.Common;
using Pragma.Application.DTOs.Quizzes;

namespace Pragma.Application.Contracts;

public interface IQuizTemplateService
{
    Task<Result<Guid>> CreateTemplateAsync(CreateQuizTemplateDto dto, Guid teacherId, bool isAdmin, CancellationToken ct = default);
    Task<Result<IReadOnlyCollection<QuizTemplateSummaryDto>>> GetTemplatesAsync(Guid teacherId, bool isAdmin, Guid? courseId = null, CancellationToken ct = default);
    Task<Result<QuizTemplateDto>> GetTemplateByIdAsync(Guid id, Guid teacherId, bool isAdmin, CancellationToken ct = default);
    Task<Result<Guid>> UpdateTemplateAsync(Guid id, UpdateQuizTemplateDto dto, Guid teacherId, bool isAdmin, CancellationToken ct = default);
    Task<Result<bool>> DeleteTemplateAsync(Guid id, Guid teacherId, bool isAdmin, CancellationToken ct = default);
}
