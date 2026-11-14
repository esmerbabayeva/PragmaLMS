using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pragma.Application.Common;
using Pragma.Application.Contracts;
using Pragma.Application.DTOs.Gradebook;
using Pragma.Application.DTOs.Teacher;
using Pragma.Domain.Enums;

namespace Pragma.API.Controllers;

/// <summary>
/// Müəllim Kabineti: Yalnız təyin olunmuş qrupların, tələbələrin idarəsi, qiymətləndirmə və kviz nəzarəti.
/// </summary>
[Authorize]
[ApiController]
[Route("api/teacher")]
public class TeacherController : BaseApiController
{
    private readonly ITeacherService _teacherService;
    private readonly IGradebookService _gradebookService;
    private readonly IExcelExporter _excelExporter;
    private readonly ICurrentUserService _currentUserService;

    public TeacherController(
        ITeacherService teacherService,
        IGradebookService gradebookService,
        IExcelExporter excelExporter,
        ICurrentUserService currentUserService)
    {
        _teacherService = teacherService;
        _gradebookService = gradebookService;
        _excelExporter = excelExporter;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Müəllimə təyin olunmuş qrupların və kursların siyahısını qaytarır (dərs saatları daxil).
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.CanViewGroups)]
    [HttpGet("groups")]
    public async Task<ActionResult<IReadOnlyCollection<AssignedGroupSummaryDto>>> GetAssignedGroups(CancellationToken ct)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue) return Unauthorized();

        var isAdmin = _currentUserService.IsInRole(UserRoles.Admin);
        var result = await _teacherService.GetAssignedGroupsAsync(currentUserId.Value, isAdmin, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Təyin olunmuş qrupdakı tələbələrin siyahısını, ballarını və davamiyyətini qaytarır.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.CanViewGroups)]
    [HttpGet("groups/{groupId:guid}/students")]
    public async Task<ActionResult<IReadOnlyCollection<GroupStudentDto>>> GetGroupStudents(Guid groupId, CancellationToken ct)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue) return Unauthorized();

        var isAdmin = _currentUserService.IsInRole(UserRoles.Admin);
        var result = await _teacherService.GetGroupStudentsAsync(groupId, currentUserId.Value, isAdmin, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Tələbəyə bal (mini layihə, final) qoyur və ya davamiyyət (qayıb/gecikmə) qeyd edir.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.CanGradeStudents)]
    [HttpPost("grades")]
    public async Task<ActionResult<bool>> AssignGrade([FromBody] AssignGradeDto dto, CancellationToken ct)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue) return Unauthorized();

        var isAdmin = _currentUserService.IsInRole(UserRoles.Admin);
        var result = await _teacherService.AssignGradeAsync(dto, currentUserId.Value, isAdmin, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Təyin olunmuş qrupun kvizlər siyahısını qaytarır.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.CanManageQuiz)]
    [HttpGet("groups/{groupId:guid}/quizzes")]
    public async Task<ActionResult<IReadOnlyCollection<TeacherQuizSummaryDto>>> GetGroupQuizzes(Guid groupId, CancellationToken ct)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue) return Unauthorized();

        var isAdmin = _currentUserService.IsInRole(UserRoles.Admin);
        var result = await _teacherService.GetGroupQuizzesAsync(groupId, currentUserId.Value, isAdmin, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Qrupun qiymət cədvəlini (Gradebook) qaytarır.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.CanViewResults)]
    [HttpGet("groups/{groupId:guid}/gradebook")]
    public async Task<ActionResult<CourseGradebookDto>> GetGroupGradebook(Guid groupId, CancellationToken ct)
    {
        var result = await _gradebookService.GetCourseGradebookAsync(groupId, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Qrupun (Kohortun) rəsmi akademik qiymət cədvəlini və davamiyyətini Excel (.xlsx) olaraq ixrac edir.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.CanViewResults)]
    [HttpGet("groups/{groupId:guid}/export-excel")]
    public async Task<IActionResult> ExportGroupExcel(Guid groupId, CancellationToken ct)
    {
        var cohortResult = await _excelExporter.ExportCohortJournalAsync(groupId, ct);
        if (cohortResult.IsSuccess)
        {
            var file = cohortResult.Value;
            return File(file.Content, file.ContentType, file.FileName);
        }

        // Əgər qrup (kohort) tapılmayıbsa, köhnə kurs cədvəli fallback yoxlanılır
        if (cohortResult.Error.Code == "Cohort.NotFound")
        {
            var courseResult = await _excelExporter.ExportCourseGradebookAsync(groupId, ct);
            if (courseResult.IsSuccess)
            {
                var courseFile = courseResult.Value;
                return File(courseFile.Content, courseFile.ContentType, courseFile.FileName);
            }
        }

        return HandleResult(cohortResult);
    }
}
