using Microsoft.EntityFrameworkCore;
using Pragma.Application.Common;
using Pragma.Application.Contracts;
using Pragma.Domain.Entities;
using Pragma.Domain.Enums;

namespace Pragma.Application.Services;

public class AttendanceService : IAttendanceService
{
    private readonly IAppDbContext _context;
    private readonly IUserLookupService _userLookup;
    private readonly IAuditLogService _auditLog;
    private readonly TimeProvider _timeProvider;

    public AttendanceService(
        IAppDbContext context,
        IUserLookupService userLookup,
        IAuditLogService auditLog,
        TimeProvider? timeProvider = null)
    {
        _context = context;
        _userLookup = userLookup;
        _auditLog = auditLog;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<Result<bool>> RecordBulkAttendanceAsync(
        Guid cohortId,
        BulkAttendanceDto dto,
        Guid currentUserId,
        bool isAdmin,
        CancellationToken ct = default)
    {
        var cohort = await _context.Cohorts
            .Include(c => c.Course)
            .Include(c => c.Schedules)
            .Include(c => c.Enrollments)
            .FirstOrDefaultAsync(c => c.Id == cohortId && !c.IsDeleted, ct);

        if (cohort == null)
        {
            return Result<bool>.Failure(Error.NotFound("Cohort.NotFound", "Kohort tapılmadı."));
        }

        // 1. Dərs günü yoxlanışı (Date validation: Tarix cədvəldəki dərs günlərinə uyğun olmalıdır)
        var scheduledDays = cohort.Schedules.Where(s => !s.IsDeleted).Select(s => s.DayOfWeek).Distinct().ToList();
        if (scheduledDays.Count > 0 && !scheduledDays.Contains(dto.Date.DayOfWeek))
        {
            return Result<bool>.Failure(Error.Validation("Attendance.InvalidScheduleDay",
                $"Seçilmiş tarix ({dto.Date:yyyy-MM-dd}, {dto.Date.DayOfWeek}) kohortun dərs cədvəlinə uyğun deyil. Cədvəl günləri: {string.Join(", ", scheduledDays)}."));
        }

        // 2. 7 günlük pəncərə qaydası (TimeProvider ilə Bakı vaxtı üzrə)
        var bakuNow = _timeProvider.GetBakuNow();
        var lessonDateTimeBaku = dto.Date.ToDateTime(TimeOnly.MinValue);
        var dateAge = bakuNow.Date - lessonDateTimeBaku.Date;

        if (!isAdmin)
        {
            // Müəllim yalnız özünə təyin olunmuş kohortda davamiyyət yaza bilər
            if (cohort.TeacherId != currentUserId && cohort.Course?.InstructorId != currentUserId)
            {
                return Result<bool>.Failure(Error.Forbidden("Attendance.Forbidden", "Bu kohorta davamiyyət yazmaq səlahiyyətiniz yoxdur."));
            }

            // 7 günlük redaktə məhdudiyyəti
            if (dateAge.TotalDays > 7)
            {
                return Result<bool>.Failure(Error.Forbidden("Attendance.WindowExpired",
                    "Müəllim dərsin tarixindən etibarən yalnız 7 gün ərzində davamiyyət yaza və ya düzəliş edə bilər. 7 gündən köhnə dərslərə yalnız Admin düzəliş edə bilər."));
            }
        }
        else
        {
            // Admin 7 gündən köhnə tarixə düzəliş edirsə AuditLog yazılır
            if (dateAge.TotalDays > 7)
            {
                await _auditLog.LogAsync(
                    userId: currentUserId,
                    userEmail: "Admin",
                    action: "AdminAttendanceOverride",
                    entityName: nameof(Cohort),
                    entityId: cohortId.ToString(),
                    details: $"Admin 7 günlük müəllim pəncərəsini keçmiş tarix ({dto.Date:yyyy-MM-dd}) üçün davamiyyətə düzəliş etdi.",
                    cancellationToken: ct
                );
            }
        }

        // 3. İdempotent Bulk Upsert ((CohortId, StudentId, Tarix) unikal indeks qorunur)
        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        var existingRecords = await _context.AttendanceRecords
            .Where(a => a.CohortId == cohortId && a.Date == dto.Date && !a.IsDeleted)
            .ToListAsync(ct);

        var existingDict = existingRecords.ToDictionary(r => r.StudentId);

        foreach (var entry in dto.Entries)
        {
            if (existingDict.TryGetValue(entry.StudentId, out var existing))
            {
                existing.Status = entry.Status;
                existing.Note = entry.Note?.Trim();
                existing.RecordedByUserId = currentUserId;
                existing.RecordedAtUtc = utcNow;
            }
            else
            {
                var record = new AttendanceRecord
                {
                    CohortId = cohortId,
                    StudentId = entry.StudentId,
                    Date = dto.Date,
                    Status = entry.Status,
                    Note = entry.Note?.Trim(),
                    RecordedByUserId = currentUserId,
                    RecordedAtUtc = utcNow
                };
                _context.AttendanceRecords.Add(record);
            }
        }

        await _context.SaveChangesAsync(ct);

        // 4. Bütün daxil edilən tələbələrin qayıb saylarının və Dropped/Enrolled statuslarının yenilənməsi
        var allCohortRecords = await _context.AttendanceRecords
            .AsNoTracking()
            .Where(a => a.CohortId == cohortId && !a.IsDeleted)
            .ToListAsync(ct);

        var affectedStudentIds = dto.Entries.Select(e => e.StudentId).Distinct().ToList();

        foreach (var studentId in affectedStudentIds)
        {
            var enrollment = cohort.Enrollments.FirstOrDefault(e => e.StudentId == studentId && !e.IsDeleted);
            if (enrollment == null) continue;

            var studentRecords = allCohortRecords.Where(r => r.StudentId == studentId).ToList();
            var absentCount = studentRecords.Count(r => r.Status == AttendanceStatus.Absent);
            var lateCount = studentRecords.Count(r => r.Status == AttendanceStatus.Late);

            // Düstur: Qayıb = Absent + floor(Late / LateToAbsenceRatio)
            var effectiveAbsences = absentCount + (lateCount / cohort.LateToAbsenceRatio);

            // 4-cü qayıbda (və ya limitə 2 vahid qalanda) xəbərdarlıq flag-ı
            enrollment.HasAbsenceWarning = effectiveAbsences >= (cohort.MaxAbsenceLimit - 2);

            // Qayıb > MaxAbsenceLimit olduqda avtomatik Dropped
            if (effectiveAbsences > cohort.MaxAbsenceLimit)
            {
                if (enrollment.Status != CohortEnrollmentStatus.Dropped)
                {
                    enrollment.Status = CohortEnrollmentStatus.Dropped;
                    enrollment.DroppedAtUtc = utcNow;
                    enrollment.DroppedReason = $"Qayıb limiti aşıldı ({effectiveAbsences}/{cohort.MaxAbsenceLimit}).";
                }
            }
            else
            {
                // QAYDA: Admin düzəlişindən sonra qayıb sayı limitə düşərsə, status Dropped-dan Enrolled-ə qayıdır
                if (isAdmin && enrollment.Status == CohortEnrollmentStatus.Dropped)
                {
                    enrollment.Status = CohortEnrollmentStatus.Enrolled;
                    enrollment.DroppedAtUtc = null;
                    enrollment.DroppedReason = null;

                    await _auditLog.LogAsync(
                        userId: currentUserId,
                        userEmail: "Admin",
                        action: "StudentRestoredFromDropped",
                        entityName: nameof(CohortEnrollment),
                        entityId: enrollment.Id.ToString(),
                        details: $"Admin davamiyyət düzəlişindən sonra qayıb sayı ({effectiveAbsences} <= {cohort.MaxAbsenceLimit}) düşdüyü üçün tələbə Dropped-dan Enrolled statusuna bərpa edildi.",
                        cancellationToken: ct
                    );
                }
            }
        }

        await _context.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }

    public async Task<Result<IReadOnlyCollection<StudentAttendanceSummary>>> GetCohortAttendanceSummaryAsync(
        Guid cohortId,
        Guid currentUserId,
        bool isAdmin,
        CancellationToken ct = default)
    {
        var cohort = await _context.Cohorts
            .AsNoTracking()
            .Include(c => c.Course)
            .Include(c => c.Enrollments)
            .Include(c => c.AttendanceRecords)
            .FirstOrDefaultAsync(c => c.Id == cohortId && !c.IsDeleted, ct);

        if (cohort == null)
        {
            return Result<IReadOnlyCollection<StudentAttendanceSummary>>.Failure(Error.NotFound("Cohort.NotFound", "Kohort tapılmadı."));
        }

        // Səlahiyyət yoxlanışı
        if (!isAdmin)
        {
            var isCourseInstructor = cohort.Course != null && cohort.Course.InstructorId == currentUserId;
            var isAssignedTeacher = cohort.TeacherId == currentUserId;
            var isEnrolledStudent = cohort.Enrollments.Any(e => e.StudentId == currentUserId && !e.IsDeleted);

            if (!isCourseInstructor && !isAssignedTeacher && !isEnrolledStudent)
            {
                return Result<IReadOnlyCollection<StudentAttendanceSummary>>.Failure(Error.Forbidden("Attendance.Forbidden", "Bu kohortun davamiyyət jurnalına baxmaq səlahiyyətiniz yoxdur."));
            }

            // Tələbə yalnız öz davamiyyətini görə bilər
            if (isEnrolledStudent && !isCourseInstructor && !isAssignedTeacher)
            {
                var single = await GetStudentAttendanceAsync(cohortId, currentUserId, currentUserId, isAdmin, ct);
                if (single.IsFailure) return Result<IReadOnlyCollection<StudentAttendanceSummary>>.Failure(single.Error);
                return Result<IReadOnlyCollection<StudentAttendanceSummary>>.Success([single.Value]);
            }
        }

        var enrollments = cohort.Enrollments.Where(e => !e.IsDeleted).ToList();
        var studentIds = enrollments.Select(e => e.StudentId).Distinct().ToList();
        var userDict = (await _userLookup.GetUsersByIdsAsync(studentIds, ct)) ?? new Dictionary<Guid, UserBasicInfoDto>();

        var list = new List<StudentAttendanceSummary>();
        foreach (var enr in enrollments)
        {
            if (!userDict.TryGetValue(enr.StudentId, out var user) || user == null)
            {
                user = await _userLookup.GetUserByIdAsync(enr.StudentId, ct);
            }
            var records = cohort.AttendanceRecords.Where(r => r.StudentId == enr.StudentId && !r.IsDeleted).ToList();

            var present = records.Count(r => r.Status == AttendanceStatus.Present);
            var absent = records.Count(r => r.Status == AttendanceStatus.Absent);
            var excused = records.Count(r => r.Status == AttendanceStatus.Excused);
            var late = records.Count(r => r.Status == AttendanceStatus.Late);

            var effective = absent + (late / cohort.LateToAbsenceRatio);

            list.Add(new StudentAttendanceSummary(
                StudentId: enr.StudentId,
                FullName: user?.FullName ?? "Tələbə",
                Email: user?.Email ?? string.Empty,
                PresentCount: present,
                AbsentCount: absent,
                ExcusedCount: excused,
                LateCount: late,
                EffectiveAbsences: effective,
                MaxAbsenceLimit: cohort.MaxAbsenceLimit,
                HasWarning: enr.HasAbsenceWarning,
                Status: enr.Status
            ));
        }

        return Result<IReadOnlyCollection<StudentAttendanceSummary>>.Success(list);
    }

    public async Task<Result<IReadOnlyCollection<AttendanceDaySheetDto>>> GetCohortAttendanceSheetsAsync(
        Guid cohortId,
        Guid currentUserId,
        bool isAdmin,
        CancellationToken ct = default)
    {
        var cohort = await _context.Cohorts
            .AsNoTracking()
            .Include(c => c.Course)
            .Include(c => c.AttendanceRecords)
            .FirstOrDefaultAsync(c => c.Id == cohortId && !c.IsDeleted, ct);

        if (cohort == null)
        {
            return Result<IReadOnlyCollection<AttendanceDaySheetDto>>.Failure(Error.NotFound("Cohort.NotFound", "Kohort tapılmadı."));
        }

        if (!isAdmin && cohort.TeacherId != currentUserId && cohort.Course?.InstructorId != currentUserId)
        {
            return Result<IReadOnlyCollection<AttendanceDaySheetDto>>.Failure(Error.Forbidden("Attendance.Forbidden", "Jurnala baxmaq səlahiyyətiniz yoxdur."));
        }

        var records = cohort.AttendanceRecords.Where(r => !r.IsDeleted).OrderByDescending(r => r.Date).ToList();
        var studentIds = records.Select(r => r.StudentId).Distinct().ToList();
        var userDict = (await _userLookup.GetUsersByIdsAsync(studentIds, ct)) ?? new Dictionary<Guid, UserBasicInfoDto>();

        var bakuNow = _timeProvider.GetBakuNow();

        var sheets = records
            .GroupBy(r => r.Date)
            .OrderByDescending(g => g.Key)
            .Select(g =>
            {
                var lessonDate = g.Key;
                var lessonDateTimeBaku = lessonDate.ToDateTime(TimeOnly.MinValue);
                var isEditable = (bakuNow.Date - lessonDateTimeBaku.Date).TotalDays <= 7;

                var details = g.Select(r =>
                {
                    userDict.TryGetValue(r.StudentId, out var user);
                    return new AttendanceRecordDetailDto(
                        RecordId: r.Id,
                        StudentId: r.StudentId,
                        StudentFullName: user?.FullName ?? "Tələbə",
                        Status: r.Status,
                        Note: r.Note,
                        RecordedAtUtc: r.RecordedAtUtc,
                        IsEditableByTeacher: isEditable
                    );
                }).ToList();

                return new AttendanceDaySheetDto(
                    Date: lessonDate,
                    DayOfWeek: lessonDate.DayOfWeek,
                    Records: details
                );
            }).ToList();

        return Result<IReadOnlyCollection<AttendanceDaySheetDto>>.Success(sheets);
    }

    public async Task<Result<StudentAttendanceSummary>> GetStudentAttendanceAsync(
        Guid cohortId,
        Guid studentId,
        Guid currentUserId,
        bool isAdmin,
        CancellationToken ct = default)
    {
        var cohort = await _context.Cohorts
            .AsNoTracking()
            .Include(c => c.Course)
            .Include(c => c.Enrollments)
            .Include(c => c.AttendanceRecords)
            .FirstOrDefaultAsync(c => c.Id == cohortId && !c.IsDeleted, ct);

        if (cohort == null) return Result<StudentAttendanceSummary>.Failure(Error.NotFound("Cohort.NotFound", "Kohort tapılmadı."));

        // Əgər tələbədirsə, başqasının davamiyyətinə baxa bilməz (403 Forbidden)
        if (!isAdmin && cohort.TeacherId != currentUserId && cohort.Course?.InstructorId != currentUserId && currentUserId != studentId)
        {
            return Result<StudentAttendanceSummary>.Failure(Error.Forbidden("Attendance.Forbidden", "Başqa tələbənin davamiyyət məlumatına baxmaq qadağandır."));
        }

        var enrollment = cohort.Enrollments.FirstOrDefault(e => e.StudentId == studentId && !e.IsDeleted);
        if (enrollment == null) return Result<StudentAttendanceSummary>.Failure(Error.NotFound("Enrollment.NotFound", "Tələbə bu kohortda qeydiyyatda deyil."));

        var user = await _userLookup.GetUserByIdAsync(studentId, ct);
        var records = cohort.AttendanceRecords.Where(r => r.StudentId == studentId && !r.IsDeleted).ToList();

        var present = records.Count(r => r.Status == AttendanceStatus.Present);
        var absent = records.Count(r => r.Status == AttendanceStatus.Absent);
        var excused = records.Count(r => r.Status == AttendanceStatus.Excused);
        var late = records.Count(r => r.Status == AttendanceStatus.Late);
        var effective = absent + (late / cohort.LateToAbsenceRatio);

        var summary = new StudentAttendanceSummary(
            StudentId: studentId,
            FullName: user?.FullName ?? "Tələbə",
            Email: user?.Email ?? string.Empty,
            PresentCount: present,
            AbsentCount: absent,
            ExcusedCount: excused,
            LateCount: late,
            EffectiveAbsences: effective,
            MaxAbsenceLimit: cohort.MaxAbsenceLimit,
            HasWarning: enrollment.HasAbsenceWarning,
            Status: enrollment.Status
        );

        return Result<StudentAttendanceSummary>.Success(summary);
    }
}
