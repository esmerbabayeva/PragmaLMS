using ClosedXML.Excel;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Pragma.Application.Common;
using Pragma.Application.Contracts;
using Pragma.Application.DTOs.Admin;
using Pragma.Domain.Enums;
using Pragma.Infrastructure.Identity;
using Pragma.Infrastructure.Persistence;

namespace Pragma.Infrastructure.Services;

public class AdminService : IAdminService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly AppDbContext _context;
    private readonly IAuditLogService _auditLogService;

    public AdminService(
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        AppDbContext context,
        IAuditLogService auditLogService)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _context = context;
        _auditLogService = auditLogService;
    }

    public async Task<PagedResult<AdminUserListItemDto>> GetUsersAsync(
        string? search = null,
        string? role = null,
        int pageNumber = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = _userManager.Users.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(u =>
                (u.Email != null && u.Email.ToLower().Contains(s)) ||
                u.FirstName.ToLower().Contains(s) ||
                u.LastName.ToLower().Contains(s));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var users = await query
            .OrderByDescending(u => u.CreatedAtUtc)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var list = new List<AdminUserListItemDto>();
        foreach (var u in users)
        {
            var roles = await _userManager.GetRolesAsync(u);
            if (!string.IsNullOrWhiteSpace(role) && !roles.Contains(role, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            var isLocked = u.LockoutEnd.HasValue && u.LockoutEnd.Value > DateTimeOffset.UtcNow;
            list.Add(new AdminUserListItemDto(
                u.Id,
                u.Email ?? string.Empty,
                $"{u.FirstName} {u.LastName}".Trim(),
                roles.ToList(),
                isLocked,
                u.CreatedAtUtc
            ));
        }

        return new PagedResult<AdminUserListItemDto>(list, totalCount, pageNumber, pageSize);
    }

    public async Task<Result<Guid>> CreateUserAsync(
        CreateAdminUserDto dto,
        Guid currentAdminId,
        string currentAdminEmail,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password))
        {
            return Result<Guid>.Failure(Error.Validation("Admin.InvalidInput", "Email və şifrə tələb olunur."));
        }

        var existing = await _userManager.FindByEmailAsync(dto.Email.Trim());
        if (existing != null)
        {
            return Result<Guid>.Failure(Error.Conflict("Admin.UserExists", "Bu e-poçt ünvanı ilə artıq istifadəçi mövcuddur."));
        }

        var targetRole = string.IsNullOrWhiteSpace(dto.Role) ? UserRoles.Student : dto.Role.Trim();
        if (!await _roleManager.RoleExistsAsync(targetRole))
        {
            return Result<Guid>.Failure(Error.NotFound("Admin.RoleNotFound", $"'{targetRole}' rolu sistemdə mövcud deyil."));
        }

        var nameParts = dto.FullName.Trim().Split(' ', 2);
        var firstName = nameParts.Length > 0 ? nameParts[0] : dto.FullName.Trim();
        var lastName = nameParts.Length > 1 ? nameParts[1] : string.Empty;

        var user = new ApplicationUser
        {
            UserName = dto.Email.Trim(),
            Email = dto.Email.Trim(),
            EmailConfirmed = true,
            FirstName = firstName,
            LastName = lastName,
            CreatedAtUtc = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, dto.Password);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            return Result<Guid>.Failure(Error.Validation("Admin.CreateFailed", errors));
        }

        await _userManager.AddToRoleAsync(user, targetRole);

        await _auditLogService.LogAsync(
            currentAdminId,
            currentAdminEmail,
            "UserCreated",
            "User",
            user.Id.ToString(),
            $"Created user {user.Email} with role '{targetRole}'",
            cancellationToken: cancellationToken);

        return Result<Guid>.Success(user.Id);
    }

    public async Task<Result> UpdateUserAsync(
        Guid id,
        UpdateAdminUserDto dto,
        Guid currentAdminId,
        string currentAdminEmail,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user == null)
        {
            return Result.Failure(Error.NotFound("Admin.UserNotFound", "İstifadəçi tapılmadı."));
        }

        var nameParts = dto.FullName.Trim().Split(' ', 2);
        user.FirstName = nameParts.Length > 0 ? nameParts[0] : dto.FullName.Trim();
        user.LastName = nameParts.Length > 1 ? nameParts[1] : string.Empty;

        if (!string.IsNullOrWhiteSpace(dto.Email) && !dto.Email.Equals(user.Email, StringComparison.OrdinalIgnoreCase))
        {
            var existing = await _userManager.FindByEmailAsync(dto.Email.Trim());
            if (existing != null && existing.Id != user.Id)
            {
                return Result.Failure(Error.Conflict("Admin.EmailConflict", "Bu e-poçt ünvanı artıq başqa istifadəçi tərəfindən istifadə olunur."));
            }
            user.Email = dto.Email.Trim();
            user.UserName = dto.Email.Trim();
        }

        await _userManager.UpdateAsync(user);

        await _auditLogService.LogAsync(
            currentAdminId,
            currentAdminEmail,
            "UserUpdated",
            "User",
            user.Id.ToString(),
            $"Updated user info for {user.Email}",
            cancellationToken: cancellationToken);

        return Result.Success();
    }

    public async Task<Result> DeleteUserAsync(
        Guid id,
        Guid currentAdminId,
        string currentAdminEmail,
        CancellationToken cancellationToken = default)
    {
        if (id == currentAdminId)
        {
            return Result.Failure(Error.Forbidden("Admin.SelfDelete", "Admin öz hesabını silə bilməz."));
        }

        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user == null)
        {
            return Result.Failure(Error.NotFound("Admin.UserNotFound", "İstifadəçi tapılmadı."));
        }

        // Sonuncu aktiv admin yoxlanışı
        var isAdmin = await _userManager.IsInRoleAsync(user, UserRoles.Admin);
        if (isAdmin)
        {
            var adminUsers = await _userManager.GetUsersInRoleAsync(UserRoles.Admin);
            var activeAdmins = adminUsers.Where(u => !u.LockoutEnd.HasValue || u.LockoutEnd <= DateTimeOffset.UtcNow).ToList();
            if (activeAdmins.Count <= 1 && activeAdmins.Any(u => u.Id == id))
            {
                return Result.Failure(Error.Forbidden("Admin.LastAdmin", "Sistemdə ən azı bir aktiv Admin qalmalıdır; sonuncu Admin silinə bilməz."));
            }
        }

        var userEmail = user.Email;
        var result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded)
        {
            return Result.Failure(Error.Validation("Admin.DeleteFailed", "İstifadəçi silinə bilmədi."));
        }

        await _auditLogService.LogAsync(
            currentAdminId,
            currentAdminEmail,
            "UserDeleted",
            "User",
            id.ToString(),
            $"Deleted user {userEmail}",
            cancellationToken: cancellationToken);

        return Result.Success();
    }

    public async Task<Result<bool>> ToggleUserLockAsync(
        Guid id,
        Guid currentAdminId,
        string currentAdminEmail,
        CancellationToken cancellationToken = default)
    {
        if (id == currentAdminId)
        {
            return Result<bool>.Failure(Error.Forbidden("Admin.SelfLock", "Admin öz hesabını bloklaya bilməz."));
        }

        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user == null)
        {
            return Result<bool>.Failure(Error.NotFound("Admin.UserNotFound", "İstifadəçi tapılmadı."));
        }

        var isCurrentlyLocked = user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow;
        bool willBeLocked = !isCurrentlyLocked;

        // Əgər bloklanacaqsa və admindirsə, sonuncu aktiv admin olub-olmadığını yoxla
        if (willBeLocked && await _userManager.IsInRoleAsync(user, UserRoles.Admin))
        {
            var adminUsers = await _userManager.GetUsersInRoleAsync(UserRoles.Admin);
            var activeAdmins = adminUsers.Where(u => !u.LockoutEnd.HasValue || u.LockoutEnd <= DateTimeOffset.UtcNow).ToList();
            if (activeAdmins.Count <= 1 && activeAdmins.Any(u => u.Id == id))
            {
                return Result<bool>.Failure(Error.Forbidden("Admin.LastAdmin", "Sistemdə ən azı bir aktiv Admin qalmalıdır; sonuncu Admin bloklana bilməz."));
            }
        }

        if (willBeLocked)
        {
            user.LockoutEnd = DateTimeOffset.UtcNow.AddYears(100);
        }
        else
        {
            user.LockoutEnd = null;
        }

        await _userManager.UpdateAsync(user);

        await _auditLogService.LogAsync(
            currentAdminId,
            currentAdminEmail,
            willBeLocked ? "UserBlocked" : "UserUnblocked",
            "User",
            user.Id.ToString(),
            $"User {user.Email} status changed to: {(willBeLocked ? "Blocked" : "Active")}",
            cancellationToken: cancellationToken);

        return Result<bool>.Success(willBeLocked);
    }

    public async Task<Result> ResetUserPasswordAsync(
        Guid id,
        ResetUserPasswordDto dto,
        Guid currentAdminId,
        string currentAdminEmail,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dto.NewPassword))
        {
            return Result.Failure(Error.Validation("Admin.InvalidPassword", "Yeni şifrə boş ola bilməz."));
        }

        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user == null)
        {
            return Result.Failure(Error.NotFound("Admin.UserNotFound", "İstifadəçi tapılmadı."));
        }

        var removeResult = await _userManager.RemovePasswordAsync(user);
        var addResult = await _userManager.AddPasswordAsync(user, dto.NewPassword);
        if (!addResult.Succeeded)
        {
            var err = string.Join("; ", addResult.Errors.Select(e => e.Description));
            return Result.Failure(Error.Validation("Admin.ResetPasswordFailed", err));
        }

        await _auditLogService.LogAsync(
            currentAdminId,
            currentAdminEmail,
            "PasswordReset",
            "User",
            user.Id.ToString(),
            $"Password reset performed for user {user.Email}",
            cancellationToken: cancellationToken);

        return Result.Success();
    }

    public async Task<Result> AssignUserRolesAsync(
        Guid id,
        ChangeUserRolesDto dto,
        Guid currentAdminId,
        string currentAdminEmail,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user == null)
        {
            return Result.Failure(Error.NotFound("Admin.UserNotFound", "İstifadəçi tapılmadı."));
        }

        var currentRoles = await _userManager.GetRolesAsync(user);

        // Admin öz Admin rolunu ala bilməz
        if (id == currentAdminId && !dto.Roles.Contains(UserRoles.Admin, StringComparer.OrdinalIgnoreCase))
        {
            return Result.Failure(Error.Forbidden("Admin.SelfDemote", "Admin öz Admin rolunu ala bilməz."));
        }

        // Sonuncu Admin-in rolunu almaq qadağandır
        if (currentRoles.Contains(UserRoles.Admin) && !dto.Roles.Contains(UserRoles.Admin, StringComparer.OrdinalIgnoreCase))
        {
            var adminUsers = await _userManager.GetUsersInRoleAsync(UserRoles.Admin);
            var activeAdmins = adminUsers.Where(u => !u.LockoutEnd.HasValue || u.LockoutEnd <= DateTimeOffset.UtcNow).ToList();
            if (activeAdmins.Count <= 1 && activeAdmins.Any(u => u.Id == id))
            {
                return Result.Failure(Error.Forbidden("Admin.LastAdmin", "Sistemdə ən azı bir aktiv Admin qalmalıdır; sonuncu Admin rolu alına bilməz."));
            }
        }

        // Rolların mövcudluğunu yoxla
        foreach (var r in dto.Roles)
        {
            if (!await _roleManager.RoleExistsAsync(r))
            {
                return Result.Failure(Error.NotFound("Admin.RoleNotFound", $"'{r}' rolu sistemdə mövcud deyil."));
            }
        }

        await _userManager.RemoveFromRolesAsync(user, currentRoles);
        await _userManager.AddToRolesAsync(user, dto.Roles);

        await _auditLogService.LogAsync(
            currentAdminId,
            currentAdminEmail,
            "UserRolesChanged",
            "User",
            user.Id.ToString(),
            $"Old roles: [{string.Join(", ", currentRoles)}], New roles: [{string.Join(", ", dto.Roles)}]",
            cancellationToken: cancellationToken);

        return Result.Success();
    }

    public async Task<Result> AdjustStudentGradeAsync(
        AdjustGradeDto dto,
        Guid currentAdminId,
        string currentAdminEmail,
        CancellationToken cancellationToken = default)
    {
        var enrollment = await _context.Enrollments
            .Include(e => e.Course)
            .FirstOrDefaultAsync(e => e.Id == dto.EnrollmentId, cancellationToken);

        if (enrollment == null)
        {
            return Result.Failure(Error.NotFound("Enrollment.NotFound", "Qeydiyyat tapılmadı."));
        }

        var oldMini = enrollment.MiniProjectGrade;
        var oldFinal = enrollment.FinalGrade;
        var oldAbsent = enrollment.AbsentCount;
        var oldLate = enrollment.LateCount;

        if (dto.MiniProjectGrade.HasValue) enrollment.MiniProjectGrade = Math.Clamp(dto.MiniProjectGrade.Value, 0, 100);
        if (dto.FinalGrade.HasValue) enrollment.FinalGrade = Math.Clamp(dto.FinalGrade.Value, 0, 100);
        if (dto.AbsentCount.HasValue) enrollment.AbsentCount = Math.Max(0, dto.AbsentCount.Value);
        if (dto.LateCount.HasValue) enrollment.LateCount = Math.Max(0, dto.LateCount.Value);

        enrollment.TeacherFeedback = $"[Admin Düzəlişi]: {dto.Reason}";
        enrollment.GradedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            currentAdminId,
            currentAdminEmail,
            "GradeAdjusted",
            "Enrollment",
            enrollment.Id.ToString(),
            $"Student: {enrollment.StudentId}, Course: {enrollment.Course?.Title}. " +
            $"Old: (Mini: {oldMini}, Final: {oldFinal}, Absent: {oldAbsent}, Late: {oldLate}) -> " +
            $"New: (Mini: {enrollment.MiniProjectGrade}, Final: {enrollment.FinalGrade}, Absent: {enrollment.AbsentCount}, Late: {enrollment.LateCount}). " +
            $"Reason: {dto.Reason}",
            cancellationToken: cancellationToken);

        return Result.Success();
    }

    public async Task<SystemStatisticsDto> GetSystemStatisticsAsync(CancellationToken cancellationToken = default)
    {
        var totalUsers = await _userManager.Users.CountAsync(cancellationToken);
        var totalStudents = (await _userManager.GetUsersInRoleAsync(UserRoles.Student)).Count;
        var totalInstructors = (await _userManager.GetUsersInRoleAsync(UserRoles.Instructor)).Count;
        var totalTeachers = (await _userManager.GetUsersInRoleAsync(UserRoles.Teacher)).Count;

        var totalCourses = await _context.Courses.CountAsync(cancellationToken);
        var publishedCourses = await _context.Courses.CountAsync(c => c.Status == CourseStatus.Published, cancellationToken);
        var totalCategories = await _context.Categories.CountAsync(cancellationToken);
        var totalEnrollments = await _context.Enrollments.CountAsync(cancellationToken);
        var totalCompletedCourses = await _context.Enrollments.CountAsync(e => e.ProgressPercentage >= 100, cancellationToken);
        var totalQuizzes = await _context.Quizzes.CountAsync(cancellationToken);

        var quizAverages = await _context.QuizAttempts
            .Where(a => a.Status == QuizAttemptStatus.Completed)
            .Select(a => (double)a.ScorePercentage)
            .ToListAsync(cancellationToken);

        var avgScore = quizAverages.Count > 0 ? (decimal)Math.Round(quizAverages.Average(), 1) : 0m;

        return new SystemStatisticsDto(
            totalUsers,
            totalStudents,
            totalInstructors,
            totalTeachers,
            totalCourses,
            publishedCourses,
            totalCategories,
            totalEnrollments,
            totalCompletedCourses,
            totalQuizzes,
            avgScore
        );
    }

    public async Task<byte[]> ExportSystemReportExcelAsync(CancellationToken cancellationToken = default)
    {
        var stats = await GetSystemStatisticsAsync(cancellationToken);
        var courses = await _context.Courses
            .AsNoTracking()
            .Include(c => c.Category)
            .Include(c => c.Enrollments)
            .OrderByDescending(c => c.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        using var workbook = new XLWorkbook();

        // Sheet 1: Xülasə Göstəricilər
        var wsSummary = workbook.Worksheets.Add("Sistem Xülasəsi");
        wsSummary.Cell(1, 1).Value = "PRAGMA LMS — SİSTEM HESABATI VƏ ANALİTİK GÖSTƏRİCİLƏR";
        wsSummary.Cell(1, 1).Style.Font.Bold = true;
        wsSummary.Cell(1, 1).Style.Font.FontSize = 14;
        wsSummary.Cell(1, 1).Style.Font.FontColor = XLColor.White;
        wsSummary.Range(1, 1, 1, 3).Merge().Style.Fill.BackgroundColor = XLColor.FromArgb(17, 24, 39);

        wsSummary.Cell(2, 1).Value = $"Generasiya tarixi: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC";
        wsSummary.Cell(2, 1).Style.Font.Italic = true;

        var row = 4;
        void AddMetric(string label, object val)
        {
            wsSummary.Cell(row, 1).Value = label;
            wsSummary.Cell(row, 1).Style.Font.Bold = true;
            wsSummary.Cell(row, 2).Value = val.ToString();
            wsSummary.Cell(row, 2).Style.Font.FontColor = XLColor.FromArgb(30, 64, 175);
            row++;
        }

        AddMetric("Cəmi İstifadəçilər", stats.TotalUsers);
        AddMetric("Tələbələr", stats.TotalStudents);
        AddMetric("Instructor-lar", stats.TotalInstructors);
        AddMetric("Müəllimlər (Teacher)", stats.TotalTeachers);
        AddMetric("Cəmi Kurslar", stats.TotalCourses);
        AddMetric("Dərc Edilmiş Kurslar", stats.PublishedCourses);
        AddMetric("Fəal Kateqoriyalar", stats.TotalCategories);
        AddMetric("Cəmi Kurs Qeydiyyatları (Enrollments)", stats.TotalEnrollments);
        AddMetric("Kursu Tamamlayanlar (100%)", stats.TotalCompletedCourses);
        AddMetric("Yaradılmış Kvizlər", stats.TotalQuizzes);
        AddMetric("Orta Kviz Nəticəsi (%)", $"{stats.AverageScore}%");

        wsSummary.Columns().AdjustToContents();

        // Sheet 2: Kurslar və Qeydiyyatlar
        var wsCourses = workbook.Worksheets.Add("Kurslar Analitikası");
        wsCourses.Cell(1, 1).Value = "Kurs Adı";
        wsCourses.Cell(1, 2).Value = "Kateqoriya";
        wsCourses.Cell(1, 3).Value = "Qiymət";
        wsCourses.Cell(1, 4).Value = "Status";
        wsCourses.Cell(1, 5).Value = "Tələbə Sayı";
        wsCourses.Cell(1, 6).Value = "Yaradılma Tarixi";

        var headerRange = wsCourses.Range(1, 1, 1, 6);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.FromArgb(79, 70, 229);
        headerRange.Style.Font.FontColor = XLColor.White;

        int cRow = 2;
        foreach (var c in courses)
        {
            wsCourses.Cell(cRow, 1).Value = c.Title;
            wsCourses.Cell(cRow, 2).Value = c.Category?.Name ?? "-";
            wsCourses.Cell(cRow, 3).Value = c.IsFree ? "Pulsuz" : $"{c.Price} AZN";
            wsCourses.Cell(cRow, 4).Value = c.Status.ToString();
            wsCourses.Cell(cRow, 5).Value = c.Enrollments.Count;
            wsCourses.Cell(cRow, 6).Value = c.CreatedAtUtc.ToString("yyyy-MM-dd");
            cRow++;
        }

        wsCourses.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
