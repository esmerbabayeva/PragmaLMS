using Pragma.Application.Common;
using Pragma.Application.DTOs.Quizzes;

namespace Pragma.Application.Contracts;

public interface IQuizService
{
    Task<Result<Guid>> CreateQuizAsync(Guid lessonId, CreateQuizDto dto, Guid userId, bool isAdmin, CancellationToken cancellationToken = default);
    Task<Result<QuizDetailDto>> GetQuizForManagementAsync(Guid quizId, Guid userId, bool isAdmin, CancellationToken cancellationToken = default);
    Task<Result<Guid>> UpdateQuizAsync(Guid quizId, UpdateQuizDto dto, Guid userId, bool isAdmin, CancellationToken cancellationToken = default);
    Task<Result<bool>> DeleteQuizAsync(Guid quizId, Guid userId, bool isAdmin, CancellationToken cancellationToken = default);
    Task<Result<QuizTakingDto>> GetQuizForTakingAsync(Guid quizId, Guid studentId, CancellationToken cancellationToken = default);
    Task<Result<QuizTakingDto>> GetQuizByLessonIdAsync(Guid lessonId, Guid studentId, CancellationToken cancellationToken = default);
    Task<Result<QuizResultDto>> SubmitQuizAsync(Guid quizId, Guid studentId, SubmitQuizRequestDto dto, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<QuizAttemptSummaryDto>> GetMyAttemptsAsync(Guid quizId, Guid studentId, CancellationToken cancellationToken = default);
}
