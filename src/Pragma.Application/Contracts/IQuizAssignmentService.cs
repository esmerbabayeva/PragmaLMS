using Pragma.Application.Common;
using Pragma.Application.DTOs.Quizzes;

namespace Pragma.Application.Contracts;

public interface IQuizAssignmentService
{
    Task<Result<Guid>> CreateAssignmentAsync(CreateQuizAssignmentDto dto, Guid teacherId, bool isAdmin, CancellationToken ct = default);
    Task<Result<IReadOnlyCollection<QuizAssignmentDto>>> GetAssignmentsForTeacherAsync(Guid teacherId, bool isAdmin, Guid? cohortId = null, CancellationToken ct = default);
    Task<Result<QuizAssignmentDto>> GetAssignmentByIdAsync(Guid id, Guid currentUserId, bool isAdmin, CancellationToken ct = default);
    Task<Result<bool>> ToggleActiveStatusAsync(Guid id, Guid teacherId, bool isAdmin, CancellationToken ct = default);
    Task<Result<bool>> DeleteAssignmentAsync(Guid id, Guid teacherId, bool isAdmin, CancellationToken ct = default);

    // Tələbə əməliyyatları
    Task<Result<IReadOnlyCollection<StudentQuizAssignmentDto>>> GetActiveAssignmentsForStudentAsync(Guid studentId, CancellationToken ct = default);
    Task<Result<QuizTakingDto>> GetAssignmentForTakingAsync(Guid assignmentId, Guid studentId, CancellationToken ct = default);
    Task<Result<QuizResultDto>> SubmitAssignmentAsync(Guid assignmentId, Guid studentId, SubmitQuizRequestDto dto, CancellationToken ct = default);
}
