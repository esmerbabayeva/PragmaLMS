using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pragma.Application.Common;
using Pragma.Application.Contracts;
using Pragma.Application.DTOs.LessonQa;

namespace Pragma.API.Controllers;

[Authorize]
[ApiController]
public class LessonQaController : BaseApiController
{
    private readonly ILessonQaService _qaService;
    private readonly ICurrentUserService _currentUserService;

    public LessonQaController(
        ILessonQaService qaService,
        ICurrentUserService currentUserService)
    {
        _qaService = qaService;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Dərs üzrə verilmiş suallar və onların cavabları siyahısı (səhifələnmiş).
    /// </summary>
    [HttpGet("api/v1/lessons/{lessonId:guid}/questions")]
    [ProducesResponseType(typeof(PagedResult<LessonQuestionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<LessonQuestionDto>>> GetLessonQuestions(
        Guid lessonId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _qaService.GetLessonQuestionsAsync(lessonId, page, pageSize, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Dərs üzrə yeni sual vermək (Video vaxtı - PositionSeconds ilə).
    /// </summary>
    [HttpPost("api/v1/lessons/{lessonId:guid}/questions")]
    [ProducesResponseType(typeof(LessonQuestionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LessonQuestionDto>> AskQuestion(
        Guid lessonId,
        [FromBody] CreateLessonQuestionDto dto,
        CancellationToken ct = default)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue) return Unauthorized();

        var result = await _qaService.AskQuestionAsync(lessonId, userId.Value, dto, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Mövcud suala cavab yazmaq (Müəllim və ya digər tələbələr tərəfindən).
    /// </summary>
    [HttpPost("api/v1/questions/{questionId:guid}/answers")]
    [ProducesResponseType(typeof(LessonAnswerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LessonAnswerDto>> AnswerQuestion(
        Guid questionId,
        [FromBody] CreateLessonAnswerDto dto,
        CancellationToken ct = default)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue) return Unauthorized();

        var isInstructor = User.IsInRole("Admin") || User.IsInRole("Teacher") || User.IsInRole("Instructor");
        var result = await _qaService.AnswerQuestionAsync(questionId, userId.Value, isInstructor, dto, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Sualı həll edilmiş (Resolved) kimi qeyd etmək və ya statusunu dəyişmək.
    /// </summary>
    [HttpPut("api/v1/questions/{questionId:guid}/resolve")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<bool>> ResolveQuestion(
        Guid questionId,
        CancellationToken ct = default)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue) return Unauthorized();

        var isInstructor = User.IsInRole("Admin") || User.IsInRole("Teacher") || User.IsInRole("Instructor");
        var result = await _qaService.ResolveQuestionAsync(questionId, userId.Value, isInstructor, ct);
        return HandleResult(result);
    }
}
