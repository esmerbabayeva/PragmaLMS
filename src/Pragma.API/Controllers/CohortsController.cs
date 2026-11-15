using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pragma.Application.Common;
using Pragma.Application.Contracts;
using Pragma.Application.DTOs.Cohorts;
using Pragma.Domain.Enums;

namespace Pragma.API.Controllers;

/// <summary>
/// Kohort (Qrup əsaslı tədris axını), cədvəl, davamiyyət və qiymətləndirmə API nəzarətçisi.
/// </summary>
[Authorize]
[ApiController]
[Route("api/cohorts")]
public class CohortsController : BaseApiController
{
    private readonly ICohortService _cohortService;
    private readonly IAttendanceService _attendanceService;
    private readonly IAcademicGradeService _gradeService;
    private readonly ICohortNoteService _cohortNoteService;
    private readonly IExcelExporter _excelExporter;
    private readonly ICurrentUserService _currentUserService;

    public CohortsController(
        ICohortService cohortService,
        IAttendanceService attendanceService,
        IAcademicGradeService gradeService,
        ICohortNoteService cohortNoteService,
        IExcelExporter excelExporter,
        ICurrentUserService currentUserService)
    {
        _cohortService = cohortService;
        _attendanceService = attendanceService;
        _gradeService = gradeService;
        _cohortNoteService = cohortNoteService;
        _excelExporter = excelExporter;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Yeni tədris kohortu yaradır (Yalnız Admin və kursun Instructor-u).
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.CanManageCohorts)]
    [HttpPost]
    public async Task<ActionResult<Guid>> CreateCohort([FromBody] CreateCohortDto dto, CancellationToken ct)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue) return Unauthorized();

        var isAdmin = _currentUserService.IsInRole(UserRoles.Admin);
        var result = await _cohortService.CreateCohortAsync(dto, currentUserId.Value, isAdmin, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Kohortların səhifələnmiş və filtrlənmiş siyahısını qaytarır (PagedResult obyekti).
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<CohortSummaryDto>>> GetCohorts(
        [FromQuery] Guid? courseId,
        [FromQuery] Guid? teacherId,
        [FromQuery] CohortStatus? status,
        [FromQuery] int? pageNumber,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken ct = default)
    {
        var effectivePageNumber = pageNumber ?? page ?? 1;
        var effectivePageSize = pageSize ?? 50;

        if (effectivePageNumber < 1)
        {
            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Validation Error",
                Detail = "Səhifə nömrəsi (pageNumber) 1 və ya daha böyük olmalıdır.",
                Instance = HttpContext.Request.Path
            };
            problem.Extensions["traceId"] = HttpContext.TraceIdentifier;
            return BadRequest(problem);
        }

        if (effectivePageSize < 1)
        {
            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Validation Error",
                Detail = "Səhifə ölçüsü (pageSize) 1 və ya daha böyük olmalıdır.",
                Instance = HttpContext.Request.Path
            };
            problem.Extensions["traceId"] = HttpContext.TraceIdentifier;
            return BadRequest(problem);
        }

        if (effectivePageSize > 100)
        {
            effectivePageSize = 100;
        }

        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue) return Unauthorized();

        var isAdmin = _currentUserService.IsInRole(UserRoles.Admin);
        var result = await _cohortService.GetCohortsAsync(courseId, teacherId, status, effectivePageNumber, effectivePageSize, currentUserId.Value, isAdmin, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Kohortun tam detallarını, cədvəlini və tələbə sayını qaytarır.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CohortDetailDto>> GetCohortById(Guid id, CancellationToken ct)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue) return Unauthorized();

        var isAdmin = _currentUserService.IsInRole(UserRoles.Admin);
        var result = await _cohortService.GetCohortByIdAsync(id, currentUserId.Value, isAdmin, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Kohort məlumatlarını və çəkilərini redaktə edir (Admin və ya Instructor).
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.CanManageCohorts)]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<bool>> UpdateCohort(Guid id, [FromBody] UpdateCohortDto dto, CancellationToken ct)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue) return Unauthorized();

        var isAdmin = _currentUserService.IsInRole(UserRoles.Admin);
        var result = await _cohortService.UpdateCohortAsync(id, dto, currentUserId.Value, isAdmin, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Kohortu silir (Soft delete).
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.CanManageCohorts)]
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<bool>> DeleteCohort(Guid id, CancellationToken ct)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue) return Unauthorized();

        var isAdmin = _currentUserService.IsInRole(UserRoles.Admin);
        var result = await _cohortService.DeleteCohortAsync(id, currentUserId.Value, isAdmin, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Kohorta həftəlik dərs cədvəli əlavə edir.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.CanManageCohorts)]
    [HttpPost("{id:guid}/schedules")]
    public async Task<ActionResult<Guid>> AddSchedule(Guid id, [FromBody] CreateCohortScheduleDto dto, CancellationToken ct)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue) return Unauthorized();

        var isAdmin = _currentUserService.IsInRole(UserRoles.Admin);
        var result = await _cohortService.AddScheduleAsync(id, dto, currentUserId.Value, isAdmin, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Kohortdan cədvəl dərsini silir.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.CanManageCohorts)]
    [HttpDelete("{id:guid}/schedules/{scheduleId:guid}")]
    public async Task<ActionResult<bool>> DeleteSchedule(Guid id, Guid scheduleId, CancellationToken ct)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue) return Unauthorized();

        var isAdmin = _currentUserService.IsInRole(UserRoles.Admin);
        var result = await _cohortService.DeleteScheduleAsync(id, scheduleId, currentUserId.Value, isAdmin, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Tələbəni kohorta qeydiyyatdan keçirir.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.CanManageCohorts)]
    [HttpPost("{id:guid}/students")]
    public async Task<ActionResult<bool>> EnrollStudent(Guid id, [FromBody] EnrollStudentInCohortDto dto, CancellationToken ct)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue) return Unauthorized();

        var isAdmin = _currentUserService.IsInRole(UserRoles.Admin);
        var result = await _cohortService.EnrollStudentAsync(id, dto.StudentId, currentUserId.Value, isAdmin, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Tələbəni kohortdan çıxarır (Dropped statusuna keçirir).
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.CanManageCohorts)]
    [HttpDelete("{id:guid}/students/{studentId:guid}")]
    public async Task<ActionResult<bool>> RemoveStudent(Guid id, Guid studentId, CancellationToken ct)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue) return Unauthorized();

        var isAdmin = _currentUserService.IsInRole(UserRoles.Admin);
        var result = await _cohortService.RemoveStudentAsync(id, studentId, currentUserId.Value, isAdmin, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Kohortun tələbələrini və qayıb/bal göstəricilərini qaytarır.
    /// </summary>
    [HttpGet("{id:guid}/students")]
    public async Task<ActionResult<IReadOnlyCollection<CohortStudentDto>>> GetCohortStudents(Guid id, CancellationToken ct)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue) return Unauthorized();

        var isAdmin = _currentUserService.IsInRole(UserRoles.Admin);
        var result = await _cohortService.GetCohortStudentsAsync(id, currentUserId.Value, isAdmin, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Kohortu bitirir: bütün balları rəsmiləşdirir (IsProvisional=false), statusları Completed edir və sertifikat siyasətini işə salır.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.CanManageCohorts)]
    [HttpPost("{id:guid}/finish")]
    public async Task<ActionResult<FinishCohortResultDto>> FinishCohort(Guid id, CancellationToken ct)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue) return Unauthorized();

        var isAdmin = _currentUserService.IsInRole(UserRoles.Admin);
        var result = await _cohortService.FinishCohortAsync(id, currentUserId.Value, isAdmin, ct);
        return HandleResult(result);
    }

    // =========================================================================
    // DAVAMİYYƏT (ATTENDANCE) ENDPOINT-LƏRİ
    // =========================================================================

    /// <summary>
    /// Dərs tarixi üzrə bulk davamiyyət yazır və ya yeniləyir (Müəllim üçün 7 günlük pəncərə, Admin üçün limitsiz).
    /// </summary>
    [Authorize(Roles = $"{UserRoles.Teacher},{UserRoles.Admin}")]
    [HttpPost("{id:guid}/attendance")]
    public async Task<ActionResult<bool>> RecordBulkAttendance(Guid id, [FromBody] BulkAttendanceDto dto, CancellationToken ct)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue) return Unauthorized();

        var isAdmin = _currentUserService.IsInRole(UserRoles.Admin);
        var result = await _attendanceService.RecordBulkAttendanceAsync(id, dto, currentUserId.Value, isAdmin, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Kohort üzrə tələbələrin ümumi davamiyyət xülasəsini (qayıblar, gecikmələr, xəbərdarlıqlar) qaytarır.
    /// </summary>
    [HttpGet("{id:guid}/attendance/summary")]
    public async Task<ActionResult<IReadOnlyCollection<StudentAttendanceSummary>>> GetAttendanceSummary(Guid id, CancellationToken ct)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue) return Unauthorized();

        var isAdmin = _currentUserService.IsInRole(UserRoles.Admin);
        var result = await _attendanceService.GetCohortAttendanceSummaryAsync(id, currentUserId.Value, isAdmin, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Kohort üzrə keçirilmiş dərslərin və davamiyyət vərəqələrinin tarixçəsini qaytarır.
    /// </summary>
    [HttpGet("{id:guid}/attendance")]
    public async Task<ActionResult<IReadOnlyCollection<AttendanceDaySheetDto>>> GetAttendanceSheets(Guid id, CancellationToken ct)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue) return Unauthorized();

        var isAdmin = _currentUserService.IsInRole(UserRoles.Admin);
        var result = await _attendanceService.GetCohortAttendanceSheetsAsync(id, currentUserId.Value, isAdmin, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Tələbənin fərdi davamiyyət xülasəsini qaytarır (Tələbə yalnız özünə baxa bilər).
    /// </summary>
    [HttpGet("{id:guid}/attendance/students/{studentId:guid}")]
    public async Task<ActionResult<StudentAttendanceSummary>> GetStudentAttendance(Guid id, Guid studentId, CancellationToken ct)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue) return Unauthorized();

        var isAdmin = _currentUserService.IsInRole(UserRoles.Admin);
        var result = await _attendanceService.GetStudentAttendanceAsync(id, studentId, currentUserId.Value, isAdmin, ct);
        return HandleResult(result);
    }

    // =========================================================================
    // QİYMƏTLƏNDİRMƏ (GRADES) ENDPOINT-LƏRİ
    // =========================================================================

    /// <summary>
    /// Tələbə üçün komponent balı daxil edir (MiniProject, Final).
    /// </summary>
    [Authorize(Roles = $"{UserRoles.Teacher},{UserRoles.Admin}")]
    [HttpPost("{id:guid}/grades")]
    public async Task<ActionResult<bool>> SubmitComponentGrade(Guid id, [FromBody] SubmitGradeDto dto, CancellationToken ct)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue) return Unauthorized();

        var isAdmin = _currentUserService.IsInRole(UserRoles.Admin);
        var result = await _gradeService.SubmitComponentGradeAsync(
            id, dto.StudentId, dto.ComponentType, dto.Score, currentUserId.Value, isAdmin, dto.Notes, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Kohort üzrə tələbələrin qiymətlərini bulk şəkildə daxil edir və ya yeniləyir.
    /// </summary>
    [Authorize(Roles = $"{UserRoles.Teacher},{UserRoles.Admin}")]
    [HttpPost("{id:guid}/grades/bulk")]
    public async Task<ActionResult<bool>> SubmitBulkGrades(Guid id, [FromBody] BulkSubmitGradesDto dto, CancellationToken ct)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue) return Unauthorized();

        var isAdmin = _currentUserService.IsInRole(UserRoles.Admin);
        var result = await _gradeService.SubmitBulkGradesAsync(id, dto, currentUserId.Value, isAdmin, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Kohortun rəsmi qiymət cədvəlini (Quiz ortalaması, Mini layihə, Final və Yekun bal) qaytarır.
    /// </summary>
    [HttpGet("{id:guid}/grades")]
    public async Task<ActionResult<IReadOnlyCollection<StudentAcademicGradeSummary>>> GetCohortGrades(Guid id, CancellationToken ct)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue) return Unauthorized();

        var isAdmin = _currentUserService.IsInRole(UserRoles.Admin);
        var result = await _gradeService.GetCohortGradesAsync(id, currentUserId.Value, isAdmin, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Tələbənin fərdi akademik balını və çəkili komponent xülasəsini qaytarır.
    /// </summary>
    [HttpGet("{id:guid}/grades/students/{studentId:guid}")]
    public async Task<ActionResult<StudentAcademicGradeSummary>> GetStudentGrade(Guid id, Guid studentId, CancellationToken ct)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue) return Unauthorized();

        var isAdmin = _currentUserService.IsInRole(UserRoles.Admin);
        if (!isAdmin && currentUserId.Value != studentId)
        {
            // Yalnız Admin, həmin kohortun müəllimi və ya tələbənin özü baxa bilər
            var cohortRes = await _cohortService.GetCohortByIdAsync(id, currentUserId.Value, isAdmin, ct);
            if (cohortRes.IsFailure) return HandleResult(Result<StudentAcademicGradeSummary>.Failure(cohortRes.Error));

            var isCohortTeacher = cohortRes.Value.TeacherId == currentUserId.Value;
            if (!isCohortTeacher)
            {
                return HandleResult(Result<StudentAcademicGradeSummary>.Failure(
                    Error.Forbidden("Cohort.Forbidden", "Yalnız Admin, həmin kohortun müəllimi və ya tələbənin özü bu qiymətə baxa bilər.")));
            }
        }

        var result = await _gradeService.CalculateStudentGradeAsync(id, studentId, ct);
        return HandleResult(result);
    }

    // =========================================================================
    // QEYDLƏR VƏ ŞƏRHLƏR (COHORT NOTES & FEEDBACK) ENDPOINT-LƏRİ
    // =========================================================================

    /// <summary>
    /// Kohortda tələbə üzrə bütün qeydləri (Feedback və Daxili qeydlər) qaytarır (Yalnız Teacher və Admin).
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.CanGradeStudents)]
    [HttpGet("{id:guid}/students/{studentId:guid}/notes")]
    public async Task<ActionResult<IReadOnlyCollection<CohortNoteDto>>> GetStudentNotes(Guid id, Guid studentId, CancellationToken ct)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue) return Unauthorized();

        var isAdmin = _currentUserService.IsInRole(UserRoles.Admin);
        var result = await _cohortNoteService.GetNotesForStudentAsync(id, studentId, currentUserId.Value, isAdmin, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Kohortda tələbə üçün yeni qeyd və ya rəy yazır (Feedback və ya Private).
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.CanGradeStudents)]
    [HttpPost("{id:guid}/students/{studentId:guid}/notes")]
    public async Task<ActionResult<CohortNoteDto>> CreateStudentNote(Guid id, Guid studentId, [FromBody] CreateCohortNoteDto dto, CancellationToken ct)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue) return Unauthorized();

        var isAdmin = _currentUserService.IsInRole(UserRoles.Admin);
        var result = await _cohortNoteService.CreateNoteAsync(id, studentId, dto, currentUserId.Value, isAdmin, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Müəllimin öz qeydini redaktə edir və ya növünü dəyişir (Admin istənilən qeydi).
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.CanGradeStudents)]
    [HttpPut("{id:guid}/notes/{noteId:guid}")]
    public async Task<ActionResult<CohortNoteDto>> UpdateNote(Guid id, Guid noteId, [FromBody] UpdateCohortNoteDto dto, CancellationToken ct)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue) return Unauthorized();

        var isAdmin = _currentUserService.IsInRole(UserRoles.Admin);
        var result = await _cohortNoteService.UpdateNoteAsync(id, noteId, dto, currentUserId.Value, isAdmin, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Qeydi birdəfəlik silir (Hard delete, audit log-a qeyd olunur).
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.CanGradeStudents)]
    [HttpDelete("{id:guid}/notes/{noteId:guid}")]
    public async Task<ActionResult<bool>> DeleteNote(Guid id, Guid noteId, CancellationToken ct)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue) return Unauthorized();

        var isAdmin = _currentUserService.IsInRole(UserRoles.Admin);
        var result = await _cohortNoteService.DeleteNoteAsync(id, noteId, currentUserId.Value, isAdmin, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Qrupun (Kohortun) rəsmi akademik jurnalını və davamiyyətini Excel (.xlsx) olaraq ixrac edir.
    /// </summary>
    [HttpGet("{id:guid}/export-excel")]
    [Authorize(Policy = AuthorizationPolicies.CanViewResults)]
    public async Task<IActionResult> ExportCohortJournal(Guid id, CancellationToken ct)
    {
        var result = await _excelExporter.ExportCohortJournalAsync(id, ct);
        if (result.IsFailure)
            return HandleResult(result);

        var fileResult = result.Value;
        return File(fileResult.Content, fileResult.ContentType, fileResult.FileName);
    }

    /// <summary>
    /// Kohort üçün Final layihənin təhvil müddətini təyin edir və ya təmizləyir.
    /// </summary>
    [HttpPut("{id:guid}/final-deadline")]
    public async Task<ActionResult<SetFinalDeadlineResultDto>> SetFinalDeadline(
        Guid id,
        [FromBody] SetCohortFinalDeadlineDto dto,
        CancellationToken ct)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue) return Unauthorized();

        var isAdmin = _currentUserService.IsInRole(UserRoles.Admin);
        var result = await _cohortService.SetFinalDeadlineAsync(id, dto, currentUserId.Value, isAdmin, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Tələbənin Final layihə linkini göndərməsi və ya yeniləməsi (Yalnız aktiv tələbə özü üçün).
    /// </summary>
    [HttpPut("{id:guid}/final-submission")]
    public async Task<ActionResult<StudentFinalSubmissionDto>> SubmitFinalProject(
        Guid id,
        [FromBody] SubmitFinalProjectDto dto,
        CancellationToken ct)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue) return Unauthorized();

        var result = await _cohortService.SubmitFinalProjectAsync(id, dto, currentUserId.Value, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Tələbənin öz Final layihə təhvili məlumatlarını gətirir.
    /// </summary>
    [HttpGet("{id:guid}/final-submission")]
    public async Task<ActionResult<StudentFinalSubmissionDto>> GetMyFinalSubmission(
        Guid id,
        CancellationToken ct)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue) return Unauthorized();

        var result = await _cohortService.GetMyFinalSubmissionAsync(id, currentUserId.Value, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Kohort üzrə bütün tələbələrin Final layihə təhvili siyahısını gətirir (Admin, Teacher, Instructor).
    /// </summary>
    [HttpGet("{id:guid}/final-submissions")]
    public async Task<ActionResult<PagedResult<CohortFinalSubmissionItemDto>>> GetCohortFinalSubmissions(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue) return Unauthorized();

        var isAdmin = _currentUserService.IsInRole(UserRoles.Admin);
        var result = await _cohortService.GetCohortFinalSubmissionsAsync(id, page, pageSize, currentUserId.Value, isAdmin, ct);
        return HandleResult(result);
    }
}
