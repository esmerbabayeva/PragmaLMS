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

    public ClosedXMLExcelExporter(
        IAppDbContext context,
        ICurrentUserService currentUserService,
        IUserLookupService userLookup)
    {
        _context = context;
        _currentUserService = currentUserService;
        _userLookup = userLookup;
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
