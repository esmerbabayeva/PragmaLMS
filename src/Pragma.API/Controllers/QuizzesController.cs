using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pragma.Application.Common;
using Pragma.Application.Contracts;
using Pragma.Application.DTOs.Quizzes;
using Pragma.Domain.Enums;

namespace Pragma.API.Controllers;

/// <summary>
/// Kviz qurucusu, imtahan vermə və avtomatik qiymətləndirmə.
/// </summary>
[Authorize]
[Route("api/quizzes")]
public class QuizzesController : BaseApiController
{
    private readonly IQuizService _quizService;
    private readonly ICurrentUserService _currentUserService;

    public QuizzesController(
        IQuizService quizService,
        ICurrentUserService currentUserService)
    {
        _quizService = quizService;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Dərs üçün yeni Quiz yaradır və sualları bağlayır (Müəllim və ya Admin üçün).
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.CanManageQuiz)]
    [HttpPost("/api/lessons/{lessonId:guid}/quiz")]
    public async Task<ActionResult<Guid>> CreateQuiz(Guid lessonId, [FromBody] CreateQuizDto dto, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue) return Unauthorized();

        var isAdmin = _currentUserService.IsInRole(UserRoles.Admin);
        var result = await _quizService.CreateQuizAsync(lessonId, dto, currentUserId.Value, isAdmin, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Dərsə aid kvizi imtahan vermək üçün əldə edir (Yalnız Student).
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.CanTakeQuiz)]
    [HttpGet("/api/lessons/{lessonId:guid}/quiz")]
    public async Task<ActionResult<QuizTakingDto>> GetQuizByLessonId(Guid lessonId, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue) return Unauthorized();

        var result = await _quizService.GetQuizByLessonIdAsync(lessonId, currentUserId.Value, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Yeni Quiz yaradır və seçilmiş dərslə bağlayır (Müəllim və ya Admin üçün).
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.CanManageQuiz)]
    [HttpPost]
    public async Task<ActionResult<Guid>> CreateQuizDirect([FromBody] CreateQuizDto dto, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue) return Unauthorized();

        var targetLessonId = dto.LessonId;
        if (!targetLessonId.HasValue || targetLessonId.Value == Guid.Empty)
        {
            return HandleResult(Result<Guid>.Failure(Error.Validation("Quiz.InvalidLesson", "Kviz yaratmaq üçün dərs (LessonId) mütləq seçilməlidir.")));
        }

        var isAdmin = _currentUserService.IsInRole(UserRoles.Admin);
        var result = await _quizService.CreateQuizAsync(targetLessonId.Value, dto, currentUserId.Value, isAdmin, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Kviz idarəetməsi üçün tam məlumatları (düzgün cavablar daxil) qaytarır.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.CanManageQuiz)]
    [HttpGet("{id:guid}/manage")]
    public async Task<ActionResult<QuizDetailDto>> GetQuizForManagement(Guid id, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue) return Unauthorized();

        var isAdmin = _currentUserService.IsInRole(UserRoles.Admin);
        var result = await _quizService.GetQuizForManagementAsync(id, currentUserId.Value, isAdmin, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Mövcud kvizin suallarını və parametrlərini redaktə edir.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.CanManageQuiz)]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<Guid>> UpdateQuiz(Guid id, [FromBody] UpdateQuizDto dto, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue) return Unauthorized();

        var isAdmin = _currentUserService.IsInRole(UserRoles.Admin);
        var result = await _quizService.UpdateQuizAsync(id, dto, currentUserId.Value, isAdmin, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Kvizi silir (Soft delete).
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.CanManageQuiz)]
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> DeleteQuiz(Guid id, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue) return Unauthorized();

        var isAdmin = _currentUserService.IsInRole(UserRoles.Admin);
        var result = await _quizService.DeleteQuizAsync(id, currentUserId.Value, isAdmin, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Tələbənin imtahan verməsi üçün quiz sualları və variantları (isCorrect gizlədilir).
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.CanTakeQuiz)]
    [HttpGet("{id:guid}")]
    [HttpGet("{id:guid}/for-taking")]
    public async Task<ActionResult<QuizTakingDto>> GetQuizForTaking(Guid id, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue) return Unauthorized();

        var result = await _quizService.GetQuizForTakingAsync(id, currentUserId.Value, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Tələbənin imtahan cavablarını qəbul edir və server tərəfindən avtomatik qiymətləndirir.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.CanTakeQuiz)]
    [HttpPost("{id:guid}/submit")]
    public async Task<ActionResult<QuizResultDto>> SubmitQuiz(Guid id, [FromBody] SubmitQuizRequestDto dto, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue) return Unauthorized();

        var result = await _quizService.SubmitQuizAsync(id, currentUserId.Value, dto, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Tələbənin bu kviz üzrə keçmiş imtahan cəhdlərinin siyahısını qaytarır.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.CanViewOwnGrades)]
    [HttpGet("{id:guid}/my-attempts")]
    public async Task<ActionResult<IReadOnlyCollection<QuizAttemptSummaryDto>>> GetMyAttempts(Guid id, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue) return Unauthorized();

        var attempts = await _quizService.GetMyAttemptsAsync(id, currentUserId.Value, cancellationToken);
        return Ok(attempts);
    }
}
