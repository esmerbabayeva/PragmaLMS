using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pragma.Application.Common;
using Pragma.Application.Contracts;
using Pragma.Application.DTOs.Quizzes;
using Pragma.Domain.Enums;

namespace Pragma.API.Controllers;

/// <summary>
/// Təyin edilmiş imtahanlar (Quiz Assignments / Publishing) idarəetməsi.
/// Şablondan seçilərək qrup və ya tələbələrə vaxt limiti, bal qaydaları və deadline ilə təyin olunur.
/// </summary>
[Authorize]
[ApiController]
[Route("api/quiz-assignments")]
public class QuizAssignmentsController : BaseApiController
{
    private readonly IQuizAssignmentService _assignmentService;
    private readonly ICurrentUserService _currentUserService;

    public QuizAssignmentsController(
        IQuizAssignmentService assignmentService,
        ICurrentUserService currentUserService)
    {
        _assignmentService = assignmentService;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Şablon əsasında qrupa və ya tələbələrə yeni imtahan təyin edir (Publish).
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.CanManageQuiz)]
    [HttpPost]
    public async Task<ActionResult<Guid>> CreateAssignment([FromBody] CreateQuizAssignmentDto dto, CancellationToken ct)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue) return Unauthorized();

        var isAdmin = _currentUserService.IsInRole(UserRoles.Admin);
        var result = await _assignmentService.CreateAssignmentAsync(dto, currentUserId.Value, isAdmin, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Müəllimin təyin etdiyi aktiv imtahanların siyahısını qaytarır.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.CanManageQuiz)]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<QuizAssignmentDto>>> GetTeacherAssignments([FromQuery] Guid? cohortId, CancellationToken ct)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue) return Unauthorized();

        var isAdmin = _currentUserService.IsInRole(UserRoles.Admin);
        var result = await _assignmentService.GetAssignmentsForTeacherAsync(currentUserId.Value, isAdmin, cohortId, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Təyin edilmiş imtahanın detallarını gətirir.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<QuizAssignmentDto>> GetAssignmentById(Guid id, CancellationToken ct)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue) return Unauthorized();

        var isAdmin = _currentUserService.IsInRole(UserRoles.Admin);
        var result = await _assignmentService.GetAssignmentByIdAsync(id, currentUserId.Value, isAdmin, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// İmtahanın aktivlik statusunu dəyişir (Pause / Activate).
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.CanManageQuiz)]
    [HttpPut("{id:guid}/toggle-active")]
    public async Task<ActionResult<bool>> ToggleActive(Guid id, CancellationToken ct)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue) return Unauthorized();

        var isAdmin = _currentUserService.IsInRole(UserRoles.Admin);
        var result = await _assignmentService.ToggleActiveStatusAsync(id, currentUserId.Value, isAdmin, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Təyin edilmiş imtahanı silir.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.CanManageQuiz)]
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<bool>> DeleteAssignment(Guid id, CancellationToken ct)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue) return Unauthorized();

        var isAdmin = _currentUserService.IsInRole(UserRoles.Admin);
        var result = await _assignmentService.DeleteAssignmentAsync(id, currentUserId.Value, isAdmin, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Tələbəyə təyin edilmiş bütün aktiv imtahanların siyahısı (Deadline və nəticələrlə).
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.CanTakeQuiz)]
    [HttpGet("my-assigned")]
    public async Task<ActionResult<IReadOnlyCollection<StudentQuizAssignmentDto>>> GetMyAssignedQuizzes(CancellationToken ct)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue) return Unauthorized();

        var result = await _assignmentService.GetActiveAssignmentsForStudentAsync(currentUserId.Value, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Tələbənin təyin edilmiş imtahanı verməsi üçün suallar (isCorrect gizlədilir).
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.CanTakeQuiz)]
    [HttpGet("{id:guid}/take")]
    public async Task<ActionResult<QuizTakingDto>> GetAssignmentForTaking(Guid id, CancellationToken ct)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue) return Unauthorized();

        var result = await _assignmentService.GetAssignmentForTakingAsync(id, currentUserId.Value, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Tələbə təyin edilmiş imtahanı bitirir və cavablarını təhvil verir (Avtomatik qiymətləndirmə).
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.CanTakeQuiz)]
    [HttpPost("{id:guid}/submit")]
    public async Task<ActionResult<QuizResultDto>> SubmitAssignment(Guid id, [FromBody] SubmitQuizRequestDto dto, CancellationToken ct)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue) return Unauthorized();

        var result = await _assignmentService.SubmitAssignmentAsync(id, currentUserId.Value, dto, ct);
        return HandleResult(result);
    }
}
