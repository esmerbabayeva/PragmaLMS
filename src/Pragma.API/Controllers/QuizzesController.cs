using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pragma.Application.Contracts;
using Pragma.Application.DTOs.Quizzes;
using Pragma.Domain.Enums;

namespace Pragma.API.Controllers;

[Authorize]
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
    /// Dərs üçün yeni Quiz yaradılması və sualların bağlanması (Təlimçi və ya Admin).
    /// </summary>
    [Authorize(Roles = $"{UserRoles.Instructor},{UserRoles.Admin}")]
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
    /// İmtahan vermək üçün quiz sualları və variantları.
    /// TƏHLÜKƏSİZLİK: IsCorrect sahəsi student frontend-inə göndərilmir!
    /// </summary>
    [HttpGet("{id:guid}/for-taking")]
    public async Task<ActionResult<QuizTakingDto>> GetQuizForTaking(Guid id, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue) return Unauthorized();

        var result = await _quizService.GetQuizForTakingAsync(id, currentUserId.Value, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Tələbənin imtahan cavablarını təhvil verməsi.
    /// Cavablar serverdə təhlükəsiz şəkildə yoxlanılır, bal hesablanır və cəhd qeydə alınır.
    /// </summary>
    [HttpPost("{id:guid}/submit")]
    public async Task<ActionResult<QuizResultDto>> SubmitQuiz(Guid id, [FromBody] SubmitQuizRequestDto dto, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue) return Unauthorized();

        var result = await _quizService.SubmitQuizAsync(id, currentUserId.Value, dto, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Tələbənin bu quiz üzrə keçmiş imtahan cəhdləri və nəticələri.
    /// </summary>
    [HttpGet("{id:guid}/my-attempts")]
    public async Task<ActionResult<IReadOnlyCollection<QuizAttemptSummaryDto>>> GetMyAttempts(Guid id, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue) return Unauthorized();

        var attempts = await _quizService.GetMyAttemptsAsync(id, currentUserId.Value, cancellationToken);
        return Ok(attempts);
    }
}
