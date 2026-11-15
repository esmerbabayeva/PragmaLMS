using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pragma.Application.Common;
using Pragma.Application.Contracts;
using Pragma.Application.DTOs.Quizzes;
using Pragma.Domain.Enums;

namespace Pragma.API.Controllers;

/// <summary>
/// Quiz Şablonları (Sual Bankı / Kitabxana) İdarəetməsi.
/// Müəllim burada sualları hazırlayıb şablon olaraq yadda saxlayır.
/// </summary>
[Authorize]
[ApiController]
[Route("api/quiz-templates")]
public class QuizTemplatesController : BaseApiController
{
    private readonly IQuizTemplateService _templateService;
    private readonly ICurrentUserService _currentUserService;

    public QuizTemplatesController(
        IQuizTemplateService templateService,
        ICurrentUserService currentUserService)
    {
        _templateService = templateService;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Yeni quiz şablonu yaradır (Sual bankına əlavə edir).
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.CanManageQuiz)]
    [HttpPost]
    public async Task<ActionResult<Guid>> CreateTemplate([FromBody] CreateQuizTemplateDto dto, CancellationToken ct)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue) return Unauthorized();

        var isAdmin = _currentUserService.IsInRole(UserRoles.Admin);
        var result = await _templateService.CreateTemplateAsync(dto, currentUserId.Value, isAdmin, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Müəllimin hazırladığı hazır quiz şablonlarının siyahısını qaytarır.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.CanManageQuiz)]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<QuizTemplateSummaryDto>>> GetTemplates([FromQuery] Guid? courseId, CancellationToken ct)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue) return Unauthorized();

        var isAdmin = _currentUserService.IsInRole(UserRoles.Admin);
        var result = await _templateService.GetTemplatesAsync(currentUserId.Value, isAdmin, courseId, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Konkret şablonun detallarını və suallarını gətirir.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.CanManageQuiz)]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<QuizTemplateDto>> GetTemplateById(Guid id, CancellationToken ct)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue) return Unauthorized();

        var isAdmin = _currentUserService.IsInRole(UserRoles.Admin);
        var result = await _templateService.GetTemplateByIdAsync(id, currentUserId.Value, isAdmin, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Mövcud quiz şablonunu redaktə edir. Artıq təyin edilmiş QuizAssignment-lar bundan təsirlənmir.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.CanManageQuiz)]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<Guid>> UpdateTemplate(Guid id, [FromBody] UpdateQuizTemplateDto dto, CancellationToken ct)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue) return Unauthorized();

        var isAdmin = _currentUserService.IsInRole(UserRoles.Admin);
        var result = await _templateService.UpdateTemplateAsync(id, dto, currentUserId.Value, isAdmin, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Quiz şablonunu silir (Soft delete).
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.CanManageQuiz)]
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<bool>> DeleteTemplate(Guid id, CancellationToken ct)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue) return Unauthorized();

        var isAdmin = _currentUserService.IsInRole(UserRoles.Admin);
        var result = await _templateService.DeleteTemplateAsync(id, currentUserId.Value, isAdmin, ct);
        return HandleResult(result);
    }
}
