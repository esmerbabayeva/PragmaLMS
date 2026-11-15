using Pragma.Application.Common;
using Pragma.Application.DTOs.LessonQa;

namespace Pragma.Application.Contracts;

public interface ILessonQaService
{
    Task<Result<PagedResult<LessonQuestionDto>>> GetLessonQuestionsAsync(
        Guid lessonId,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default);

    Task<Result<LessonQuestionDto>> AskQuestionAsync(
        Guid lessonId,
        Guid studentId,
        CreateLessonQuestionDto dto,
        CancellationToken ct = default);

    Task<Result<LessonAnswerDto>> AnswerQuestionAsync(
        Guid questionId,
        Guid authorId,
        bool isInstructor,
        CreateLessonAnswerDto dto,
        CancellationToken ct = default);

    Task<Result<bool>> ResolveQuestionAsync(
        Guid questionId,
        Guid currentUserId,
        bool isInstructor,
        CancellationToken ct = default);
}
