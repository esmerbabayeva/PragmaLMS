using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Pragma.Application.Common;
using Pragma.Application.Contracts;
using Pragma.Domain.Enums;

namespace Pragma.Infrastructure.Services;

public class ClosedXMLExcelExporter : IExcelExporter
{
    private readonly IAppDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserLookupService _userLookup;
    private readonly IAcademicGradeService _gradeService;
    private readonly IAttendanceService _attendanceService;
    private readonly ICertificateEligibilityPolicy _certificatePolicy;
    private readonly TimeProvider _timeProvider;

    public ClosedXMLExcelExporter(
        IAppDbContext context,
        ICurrentUserService currentUserService,
        IUserLookupService userLookup,
        IAcademicGradeService gradeService,
        IAttendanceService attendanceService,
        ICertificateEligibilityPolicy certificatePolicy,
        TimeProvider? timeProvider = null)
    {
        _context = context;
        _currentUserService = currentUserService;
        _userLookup = userLookup;
        _gradeService = gradeService;
        _attendanceService = attendanceService;
        _certificatePolicy = certificatePolicy;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<Result<ExcelFileResult>> ExportCourseGradebookAsync(Guid courseId, CancellationToken ct = default)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue)
            return Result<ExcelFileResult>.Failure(Error.Unauthorized("İstifadəçi autentifikasiyadan keçməyib."));

        var course = await _context.Courses
            .AsNoTracking()
            .Include(c => c.Modules.Where(m => !m.IsDeleted).OrderBy(m => m.OrderIndex))
                .ThenInclude(m => m.Lessons.Where(l => !l.IsDeleted).OrderBy(l => l.OrderIndex))
            .FirstOrDefaultAsync(c => c.Id == courseId && !c.IsDeleted, ct);

        if (course == null)
            return Result<ExcelFileResult>.Failure(Error.NotFound("Kurs tapılmadı."));

        var isAdmin = _currentUserService.IsInRole(UserRoles.Admin);
        var isInstructorOwner = _currentUserService.IsInRole(UserRoles.Instructor) && course.InstructorId == currentUserId.Value;

        if (!isAdmin && !isInstructorOwner)
            return Result<ExcelFileResult>.Failure(Error.Forbidden("Bu kursun qiymət cədvəlini ixrac etmək səlahiyyətiniz yoxdur."));

        // 1. Tələbələrin qeydiyyat məlumatları
        var enrollments = await _context.Enrollments
            .AsNoTracking()
            .Where(e => e.CourseId == courseId && !e.IsDeleted)
            .OrderByDescending(e => e.EnrolledAtUtc)
            .ToListAsync(ct);

        var studentIds = enrollments.Select(e => e.StudentId).Distinct().ToList();
        var userDict = await _userLookup.GetUsersByIdsAsync(studentIds, ct);

        // 2. Kviz nəticələri
        var courseLessonIds = course.Modules.SelectMany(m => m.Lessons).Select(l => l.Id).ToList();
        var quizzes = await _context.Quizzes
            .AsNoTracking()
            .Where(q => courseLessonIds.Contains(q.LessonId) && !q.IsDeleted)
            .ToListAsync(ct);

        var quizIds = quizzes.Select(q => q.Id).ToList();
        var attempts = await _context.QuizAttempts
            .AsNoTracking()
            .Where(a => quizIds.Contains(a.QuizId) && !a.IsDeleted)
            .OrderByDescending(a => a.CompletedAtUtc)
            .ToListAsync(ct);

        // 3. Dərs izlənmələri
        var progresses = await _context.LessonProgresses
            .AsNoTracking()
            .Where(p => courseLessonIds.Contains(p.LessonId) && !p.IsDeleted)
            .ToListAsync(ct);

        using var workbook = new XLWorkbook();

        // -------------------------------------------------------------
        // Vərəq 1: Tələbələr və Ümumi Proqres
        // -------------------------------------------------------------
        var sheetStudents = workbook.Worksheets.Add("Tələbələr və Proqres");
        SetupHeaderRow(sheetStudents, new[]
        {
            "Tələbə ID", "Ad və Soyad", "Email", "Qeydiyyat Tarixi", "Tamamlanma Faizi", "Status"
        });

        int rowIdx = 2;
        foreach (var enr in enrollments)
        {
            var user = userDict.TryGetValue(enr.StudentId, out var u) ? u : null;
            var fullName = user?.FullName ?? "Məlumat yoxdur";
            var email = user?.Email ?? "Məlumat yoxdur";

            SetTextSafe(sheetStudents.Cell(rowIdx, 1), enr.StudentId.ToString());
            SetTextSafe(sheetStudents.Cell(rowIdx, 2), fullName);
            SetTextSafe(sheetStudents.Cell(rowIdx, 3), email);
            sheetStudents.Cell(rowIdx, 4).SetValue(enr.EnrolledAtUtc.ToString("yyyy-MM-dd HH:mm"));
            sheetStudents.Cell(rowIdx, 5).SetValue($"{enr.ProgressPercentage}%");
            sheetStudents.Cell(rowIdx, 6).SetValue(enr.IsCompleted ? "Tamamlayıb" : "Davam edir");

            ApplyRowZebra(sheetStudents.Row(rowIdx), rowIdx);
            rowIdx++;
        }
        sheetStudents.Columns().AdjustToContents();

        // -------------------------------------------------------------
        // Vərəq 2: Kviz Nəticələri
        // -------------------------------------------------------------
        var sheetQuizzes = workbook.Worksheets.Add("Kviz Nəticələri");
        SetupHeaderRow(sheetQuizzes, new[]
        {
            "Tələbə Adı", "Email", "Kviz Başlığı", "Bal (%)", "Keçid Balı (%)", "Nəticə", "Tarix"
        });

        rowIdx = 2;
        var quizDict = quizzes.ToDictionary(q => q.Id);
        foreach (var att in attempts)
        {
            var user = userDict.TryGetValue(att.StudentId, out var u) ? u : null;
            var q = quizDict.TryGetValue(att.QuizId, out var quizItem) ? quizItem : null;

            SetTextSafe(sheetQuizzes.Cell(rowIdx, 1), user?.FullName ?? att.StudentId.ToString());
            SetTextSafe(sheetQuizzes.Cell(rowIdx, 2), user?.Email ?? "");
            SetTextSafe(sheetQuizzes.Cell(rowIdx, 3), q?.Title ?? "Kviz");
            sheetQuizzes.Cell(rowIdx, 4).SetValue($"{att.ScorePercentage}%");
            sheetQuizzes.Cell(rowIdx, 5).SetValue($"{q?.PassingScorePercentage ?? 70}%");
            sheetQuizzes.Cell(rowIdx, 6).SetValue(att.IsPassed ? "Uğurlu (Keçdi)" : "Uğursuz (Kəsildi)");
            sheetQuizzes.Cell(rowIdx, 7).SetValue(att.CompletedAtUtc?.ToString("yyyy-MM-dd HH:mm") ?? att.StartedAtUtc.ToString("yyyy-MM-dd HH:mm"));

            ApplyRowZebra(sheetQuizzes.Row(rowIdx), rowIdx);
            rowIdx++;
        }
        sheetQuizzes.Columns().AdjustToContents();

        // -------------------------------------------------------------
        // Vərəq 3: Dərs İzlənmələri
        // -------------------------------------------------------------
        var sheetLessons = workbook.Worksheets.Add("Dərs İzlənməsi");
        SetupHeaderRow(sheetLessons, new[]
        {
            "Tələbə Adı", "Modul", "Dərs", "Son Baxılan (saniyə)", "Maksimum (saniyə)", "Status", "Bitirilmə Tarixi"
        });

        rowIdx = 2;
        var lessonDict = course.Modules.SelectMany(m => m.Lessons.Select(l => new { Lesson = l, ModuleTitle = m.Title })).ToDictionary(x => x.Lesson.Id);

        foreach (var prog in progresses)
        {
            var user = userDict.TryGetValue(prog.StudentId, out var u) ? u : null;
            var lInfo = lessonDict.TryGetValue(prog.LessonId, out var li) ? li : null;

            SetTextSafe(sheetLessons.Cell(rowIdx, 1), user?.FullName ?? prog.StudentId.ToString());
            SetTextSafe(sheetLessons.Cell(rowIdx, 2), lInfo?.ModuleTitle ?? "Modul");
            SetTextSafe(sheetLessons.Cell(rowIdx, 3), lInfo?.Lesson.Title ?? "Dərs");
            sheetLessons.Cell(rowIdx, 4).SetValue(prog.LastPositionSeconds);
            sheetLessons.Cell(rowIdx, 5).SetValue(prog.MaxPositionSeconds);
            sheetLessons.Cell(rowIdx, 6).SetValue(prog.IsCompleted ? "Tamamlanıb" : "Davam edir");
            sheetLessons.Cell(rowIdx, 7).SetValue(prog.CompletedAtUtc?.ToString("yyyy-MM-dd HH:mm") ?? "-");

            ApplyRowZebra(sheetLessons.Row(rowIdx), rowIdx);
            rowIdx++;
        }
        sheetLessons.Columns().AdjustToContents();

        using var memoryStream = new MemoryStream();
        workbook.SaveAs(memoryStream);
        var bytes = memoryStream.ToArray();

        var fileName = $"Pragma_Gradebook_{course.Slug}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx";
        return Result<ExcelFileResult>.Success(new ExcelFileResult(
            bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName
        ));
    }

    public async Task<Result<ExcelFileResult>> ExportCohortJournalAsync(Guid cohortId, CancellationToken ct = default)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue)
            return Result<ExcelFileResult>.Failure(Error.Unauthorized("İstifadəçi autentifikasiyadan keçməyib."));

        var cohort = await _context.Cohorts
            .AsNoTracking()
            .Include(c => c.Course)
            .Include(c => c.Enrollments)
            .FirstOrDefaultAsync(c => c.Id == cohortId && !c.IsDeleted, ct);

        if (cohort == null)
            return Result<ExcelFileResult>.Failure(Error.NotFound("Cohort.NotFound", "Qrup (Kohort) tapılmadı."));

        var isAdmin = _currentUserService.IsInRole(UserRoles.Admin);
        var isTeacher = cohort.TeacherId == currentUserId.Value;
        var isInstructorOwner = cohort.Course != null && cohort.Course.InstructorId == currentUserId.Value;

        if (!isAdmin && !isTeacher && !isInstructorOwner)
            return Result<ExcelFileResult>.Failure(Error.Forbidden("Cohort.Forbidden", "Bu qrupun jurnalını ixrac etmək səlahiyyətiniz yoxdur."));

        // 1. Müəllim məlumatı
        var teacherUser = cohort.TeacherId != Guid.Empty ? await _userLookup.GetUserByIdAsync(cohort.TeacherId, ct) : null;

        // 2. Tələbələrin qiymət nəticələri (IAcademicGradeService)
        var gradesResult = await _gradeService.GetCohortGradesAsync(cohortId, currentUserId.Value, isAdmin, ct);
        var studentGrades = gradesResult.IsSuccess ? gradesResult.Value : new List<StudentAcademicGradeSummary>();

        // 3. Davamiyyət xülasəsi (IAttendanceService)
        var attSummaryResult = await _attendanceService.GetCohortAttendanceSummaryAsync(cohortId, currentUserId.Value, isAdmin, ct);
        var attSummaries = attSummaryResult.IsSuccess
            ? attSummaryResult.Value.ToDictionary(a => a.StudentId)
            : new Dictionary<Guid, StudentAttendanceSummary>();

        // 4. Gündəlik davamiyyət vərəqləri (IAttendanceService)
        var attSheetsResult = await _attendanceService.GetCohortAttendanceSheetsAsync(cohortId, currentUserId.Value, isAdmin, ct);
        var daySheets = attSheetsResult.IsSuccess
            ? attSheetsResult.Value.OrderBy(s => s.Date).ToList()
            : new List<AttendanceDaySheetDto>();

        using var workbook = new XLWorkbook();

        // -------------------------------------------------------------
        // Vərəq 1: "Akademik Jurnal" (Qiymətlər, Davamiyyət və Sertifikat Qərarı)
        // -------------------------------------------------------------
        var sheetGrades = workbook.Worksheets.Add("Akademik Jurnal");
        var headers1 = new[]
        {
            "№",
            "Tələbə (Ad və Soyad)",
            "Email",
            "Status",
            "Qayıb",
            "Gecikmə",
            $"Hesablanan Qayıb (Limit: {cohort.MaxAbsenceLimit})",
            $"Test ({cohort.QuizWeight:0}%)",
            $"Mini Layihə ({cohort.MiniProjectWeight:0}%)",
            $"Final ({cohort.FinalWeight:0}%)",
            "Yekun Bal",
            "Bal Statusu",
            "Sertifikat Qərarı"
        };
        SetupHeaderRow(sheetGrades, headers1);

        int rowIdx = 2;
        int rowNumber = 1;
        foreach (var grade in studentGrades)
        {
            attSummaries.TryGetValue(grade.StudentId, out var att);
            var effectiveAbsences = att?.EffectiveAbsences ?? 0;
            var enrollment = cohort.Enrollments.FirstOrDefault(e => e.StudentId == grade.StudentId && !e.IsDeleted);
            if (enrollment == null)
            {
                continue;
            }

            var eligibility = _certificatePolicy.Evaluate(
                enrollment,
                grade.OfficialScore,
                effectiveAbsences,
                cohort.MaxAbsenceLimit,
                cohort.Status == CohortStatus.Finished
            );

            string certDecision = eligibility.Result switch
            {
                CertificateEligibilityResult.Diploma => "Diplom",
                CertificateEligibilityResult.Attendance => "Dərsdə İştirak Sertifikatı",
                CertificateEligibilityResult.Denied => "Verilmir (Qayıb limiti aşıldı)",
                CertificateEligibilityResult.Pending => "Gözləmədə (Müvəqqəti bal və ya aktiv qrup)",
                _ => "Verilmir"
            };

            string studentStatus = enrollment?.Status switch
            {
                CohortEnrollmentStatus.Dropped => "Çıxarılıb (Dropped)",
                CohortEnrollmentStatus.Completed => "Tamamlayıb",
                _ => "Aktiv (Enrolled)"
            };

            bool hasAnyGrade = grade.QuizAverage.HasValue || grade.MiniProjectScore.HasValue || grade.FinalScore.HasValue;
            string scoreStatus = hasAnyGrade
                ? (grade.IsProvisional ? "Müvəqqəti" : "Rəsmi")
                : "Qiymətləndirilməyib";

            sheetGrades.Cell(rowIdx, 1).SetValue(rowNumber++);
            SetTextSafe(sheetGrades.Cell(rowIdx, 2), grade.StudentFullName);
            SetTextSafe(sheetGrades.Cell(rowIdx, 3), grade.StudentEmail);
            SetTextSafe(sheetGrades.Cell(rowIdx, 4), studentStatus);
            sheetGrades.Cell(rowIdx, 5).SetValue(att?.AbsentCount ?? 0);
            sheetGrades.Cell(rowIdx, 6).SetValue(att?.LateCount ?? 0);
            sheetGrades.Cell(rowIdx, 7).SetValue(effectiveAbsences);
            SetTextSafe(sheetGrades.Cell(rowIdx, 8), grade.QuizAverage.HasValue ? grade.QuizAverage.Value.ToString("0.0") : "-");
            SetTextSafe(sheetGrades.Cell(rowIdx, 9), grade.MiniProjectScore.HasValue ? grade.MiniProjectScore.Value.ToString("0.0") : "-");
            SetTextSafe(sheetGrades.Cell(rowIdx, 10), grade.FinalScore.HasValue ? grade.FinalScore.Value.ToString("0.0") : "-");
            SetTextSafe(sheetGrades.Cell(rowIdx, 11), hasAnyGrade ? grade.OfficialScore.ToString("0.0") : "-");
            SetTextSafe(sheetGrades.Cell(rowIdx, 12), scoreStatus);
            SetTextSafe(sheetGrades.Cell(rowIdx, 13), certDecision);

            // Xəbərdarlıq və ya Dropped üçün vizual vurğu
            if (effectiveAbsences > cohort.MaxAbsenceLimit || enrollment?.Status == CohortEnrollmentStatus.Dropped)
            {
                sheetGrades.Cell(rowIdx, 7).Style.Fill.BackgroundColor = XLColor.FromArgb(254, 226, 226); // Light Red
                sheetGrades.Cell(rowIdx, 7).Style.Font.FontColor = XLColor.FromArgb(185, 28, 28);
                sheetGrades.Cell(rowIdx, 7).Style.Font.Bold = true;
            }
            else if (effectiveAbsences >= cohort.MaxAbsenceLimit - 2)
            {
                sheetGrades.Cell(rowIdx, 7).Style.Fill.BackgroundColor = XLColor.FromArgb(254, 243, 199); // Light Yellow
                sheetGrades.Cell(rowIdx, 7).Style.Font.FontColor = XLColor.FromArgb(180, 83, 9);
            }

            ApplyRowZebra(sheetGrades.Row(rowIdx), rowIdx);
            rowIdx++;
        }
        sheetGrades.Columns().AdjustToContents();

        // -------------------------------------------------------------
        // Vərəq 2: "Davamiyyət Jurnalı" (Gündəlik İştirak Matrisi)
        // -------------------------------------------------------------
        var sheetAttendance = workbook.Worksheets.Add("Davamiyyət Jurnalı");
        var baseHeaders = new List<string> { "№", "Tələbə", "Email" };
        foreach (var day in daySheets)
        {
            baseHeaders.Add(day.Date.ToString("dd.MM.yyyy"));
        }
        baseHeaders.AddRange(new[] { "Qayıb", "Gecikmə", "Hesablanan Qayıb", "Davamiyyət Statusu" });
        SetupHeaderRow(sheetAttendance, baseHeaders.ToArray());

        rowIdx = 2;
        rowNumber = 1;
        foreach (var grade in studentGrades)
        {
            attSummaries.TryGetValue(grade.StudentId, out var att);
            var effectiveAbsences = att?.EffectiveAbsences ?? 0;

            sheetAttendance.Cell(rowIdx, 1).SetValue(rowNumber++);
            SetTextSafe(sheetAttendance.Cell(rowIdx, 2), grade.StudentFullName);
            SetTextSafe(sheetAttendance.Cell(rowIdx, 3), grade.StudentEmail);

            int colIdx = 4;
            foreach (var day in daySheets)
            {
                var rec = day.Records.FirstOrDefault(r => r.StudentId == grade.StudentId);
                string mark = "-";
                if (rec != null)
                {
                    mark = rec.Status switch
                    {
                        AttendanceStatus.Present => "İştirak",
                        AttendanceStatus.Absent => "Qayıb",
                        AttendanceStatus.Late => "Gecikmə",
                        AttendanceStatus.Excused => "Üzrlü",
                        _ => "-"
                    };
                }
                var cell = sheetAttendance.Cell(rowIdx, colIdx);
                SetTextSafe(cell, mark);
                if (rec?.Status == AttendanceStatus.Absent)
                {
                    cell.Style.Font.FontColor = XLColor.FromArgb(220, 38, 38);
                    cell.Style.Font.Bold = true;
                }
                else if (rec?.Status == AttendanceStatus.Late)
                {
                    cell.Style.Font.FontColor = XLColor.FromArgb(217, 119, 6);
                }
                else if (rec?.Status == AttendanceStatus.Present)
                {
                    cell.Style.Font.FontColor = XLColor.FromArgb(16, 185, 129);
                }
                colIdx++;
            }

            sheetAttendance.Cell(rowIdx, colIdx).SetValue(att?.AbsentCount ?? 0);
            sheetAttendance.Cell(rowIdx, colIdx + 1).SetValue(att?.LateCount ?? 0);
            sheetAttendance.Cell(rowIdx, colIdx + 2).SetValue(effectiveAbsences);

            string attStatus = effectiveAbsences > cohort.MaxAbsenceLimit
                ? "Kursdan çıxarılıb (Limit aşıldı)"
                : (effectiveAbsences >= cohort.MaxAbsenceLimit - 2 ? "Xəbərdarlıq" : "Normada");
            SetTextSafe(sheetAttendance.Cell(rowIdx, colIdx + 3), attStatus);

            ApplyRowZebra(sheetAttendance.Row(rowIdx), rowIdx);
            rowIdx++;
        }
        sheetAttendance.Columns().AdjustToContents();

        // -------------------------------------------------------------
        // Vərəq 3: "Kohort Məlumatı" (Metadata və Akademik Parametrlər)
        // -------------------------------------------------------------
        var sheetMeta = workbook.Worksheets.Add("Kohort Məlumatı");
        SetupHeaderRow(sheetMeta, new[] { "Parametr", "Dəyər" });

        var bakuNow = _timeProvider.GetBakuNow();
        var metaData = new List<(string, string)>
        {
            ("Kohort Adı", cohort.Name),
            ("Kurs Başlığı", cohort.Course?.Title ?? "-"),
            ("Müəllim", teacherUser?.FullName ?? (cohort.TeacherId != Guid.Empty ? cohort.TeacherId.ToString() : "-")),
            ("Qrup Statusu", cohort.Status.ToString()),
            ("Başlama Tarixi", cohort.StartDate.ToString("dd.MM.yyyy")),
            ("Bitmə Tarixi", cohort.EndDate?.ToString("dd.MM.yyyy") ?? "Təyin olunmayıb"),
            ("Ümumi Dərs Planı", $"{cohort.TotalHours:0.0} saat"),
            ("Keçilmiş Dərs Saatı", $"{cohort.CompletedHours:0.0} saat"),
            ("Qalan Dərs Saatı", $"{Math.Max(0.0, cohort.TotalHours - cohort.CompletedHours):0.0} saat"),
            ("Test Çəkisi", $"{cohort.QuizWeight:0}%"),
            ("Mini Layihə Çəkisi", $"{cohort.MiniProjectWeight:0}%"),
            ("Final Layihə Çəkisi", $"{cohort.FinalWeight:0}%"),
            ("Maksimum Qayıb Limiti", $"{cohort.MaxAbsenceLimit} qayıb"),
            ("Gecikmə / Qayıb Nisbəti", $"Hər {cohort.LateToAbsenceRatio} gecikmə = 1 qayıb"),
            ("Qeydiyyatlı Tələbə Sayı", studentGrades.Count.ToString()),
            ("İxrac Tarixi və Vaxtı", $"{bakuNow:dd.MM.yyyy HH:mm:ss} (Asia/Baku)")
        };

        rowIdx = 2;
        foreach (var (k, v) in metaData)
        {
            var cellK = sheetMeta.Cell(rowIdx, 1);
            SetTextSafe(cellK, k);
            cellK.Style.Font.Bold = true;

            var cellV = sheetMeta.Cell(rowIdx, 2);
            SetTextSafe(cellV, v);

            ApplyRowZebra(sheetMeta.Row(rowIdx), rowIdx);
            rowIdx++;
        }
        sheetMeta.Columns().AdjustToContents();

        using var memoryStream = new MemoryStream();
        workbook.SaveAs(memoryStream);
        var bytes = memoryStream.ToArray();

        var safeCohortName = SearchNormalizationHelper.ToAscii(cohort.Name)
            .Replace(" ", "_")
            .Replace(":", "")
            .Replace("/", "_");
        if (string.IsNullOrWhiteSpace(safeCohortName)) safeCohortName = cohort.Id.ToString("N")[..8];

        var fileName = $"Pragma_Journal_{safeCohortName}_{bakuNow:yyyyMMdd_HHmm}.xlsx";

        return Result<ExcelFileResult>.Success(new ExcelFileResult(
            bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName
        ));
    }

    private static void SetupHeaderRow(IXLWorksheet sheet, string[] headers)
    {
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = sheet.Cell(1, i + 1);
            cell.SetValue(headers[i]);
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromArgb(30, 41, 59); // Navy #1E293B
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }
        sheet.Row(1).Height = 24;
    }

    private static void ApplyRowZebra(IXLRow row, int rowIndex)
    {
        if (rowIndex % 2 == 1)
        {
            row.Style.Fill.BackgroundColor = XLColor.FromArgb(248, 250, 252); // Zebra #F8FAFC
        }
    }

    /// <summary>
    /// Excel Formula / CSV Injection təhlükəsizliyi:
    /// =, +, -, @ ilə başlayan mətnlər Excel açıldıqda komanda kimi icra oluna bilməz.
    /// </summary>
    private static void SetTextSafe(IXLCell cell, string rawValue)
    {
        if (string.IsNullOrEmpty(rawValue))
        {
            cell.SetValue(string.Empty);
            return;
        }

        var trimmed = rawValue.Trim();
        if (trimmed.StartsWith("=") || trimmed.StartsWith("+") || trimmed.StartsWith("-") || trimmed.StartsWith("@"))
        {
            trimmed = "'" + trimmed;
        }

        cell.SetValue(trimmed);
    }
}
