using Pragma.Application.Common;
using Pragma.Application.DTOs.AiMentor;

namespace Pragma.Application.Contracts;

public interface IAiMentorService
{
    Task<Result<AiAskResponseDto>> AskLessonQuestionAsync(Guid lessonId, string question, CancellationToken cancellationToken = default);
    Task<Result<AiAskResponseDto>> AskGlobalQuestionAsync(string question, CancellationToken cancellationToken = default);
    Task<Result<AiQuizHintResponseDto>> GetQuizQuestionHintAsync(Guid quizId, Guid questionId, CancellationToken cancellationToken = default);
}
