using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Pragma.Application.Common;
using Pragma.Application.Contracts;
using Pragma.Application.DTOs.Cohorts;
using Pragma.Domain.Entities;
using Pragma.Domain.Enums;

namespace Pragma.Application.Services;

public class CohortService : ICohortService
{
    private readonly IAppDbContext _context;
    private readonly IUserLookupService _userLookup;
    private readonly IValidator<CreateCohortDto> _createValidator;
    private readonly IValidator<UpdateCohortDto> _updateValidator;
    private readonly IValidator<CreateCohortScheduleDto> _scheduleValidator;
    private readonly IAcademicGradeService _gradeService;
    private readonly ICertificateEligibilityPolicy _eligibilityPolicy;

    public CohortService(
        IAppDbContext context,
        IUserLookupService userLookup,
        IValidator<CreateCohortDto> createValidator,
        IValidator<UpdateCohortDto> updateValidator,
        IValidator<CreateCohortScheduleDto> scheduleValidator,
        IAcademicGradeService gradeService,
        ICertificateEligibilityPolicy eligibilityPolicy)
    {
        _context = context;
        _userLookup = userLookup;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _scheduleValidator = scheduleValidator;
        _gradeService = gradeService;
        _eligibilityPolicy = eligibilityPolicy;
    }

    public async Task<Result<Guid>> CreateCohortAsync(
        CreateCohortDto dto,
        Guid currentUserId,
        bool isAdmin,
        CancellationToken ct = default)
    {
        var validation = await _createValidator.ValidateAsync(dto, ct);
        if (!validation.IsValid)
        {
            return Result<Guid>.Failure(Error.Validation("Cohort.Validation", string.Join("; ", validation.Errors.Select(e => e.ErrorMessage))));
        }

        var course = await _context.Courses
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == dto.CourseId && !c.IsDeleted, ct);

        if (course == null)
        {
            return Result<Guid>.Failure(Error.NotFound("Course.NotFound", "Kurs tapılmadı."));
        }

        // Qayda: Cohort yalnız DeliveryMode=Cohort olan kurs üçün yaradıla bilər
        if (course.DeliveryMode != DeliveryMode.Cohort)
        {
            return Result<Guid>.Failure(Error.Validation("Cohort.DeliveryModeMismatch", "Kohort yalnız tədris modeli 'Cohort' olan kurslar üçün yaradıla bilər."));
        }

        // Səlahiyyət: Yalnız Admin və həmin kursun Instructor-u kohort yarada bilər
        if (!isAdmin && course.InstructorId != currentUserId)
        {
            return Result<Guid>.Failure(Error.Forbidden("Cohort.Forbidden", "Bu kurs üçün kohort yaratmaq səlahiyyətiniz yoxdur."));
        }

        var cohort = new Cohort
        {
            Name = dto.Name.Trim(),
            CourseId = dto.CourseId,
            TeacherId = dto.TeacherId,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            TotalHours = dto.TotalHours,
            CompletedHours = 0.0,
            QuizWeight = dto.QuizWeight,
            MiniProjectWeight = dto.MiniProjectWeight,
            FinalWeight = dto.FinalWeight,
            MaxAbsenceLimit = dto.MaxAbsenceLimit,
            LateToAbsenceRatio = dto.LateToAbsenceRatio,
            Status = CohortStatus.Upcoming
        };

        _context.Cohorts.Add(cohort);
        await _context.SaveChangesAsync(ct);

        return Result<Guid>.Success(cohort.Id);
    }

    public async Task<Result<CohortDetailDto>> GetCohortByIdAsync(
        Guid id,
        Guid currentUserId,
        bool isAdmin,
        CancellationToken ct = default)
    {
        var cohort = await _context.Cohorts
            .AsNoTracking()
            .Include(c => c.Course)
            .Include(c => c.Schedules)
            .Include(c => c.Enrollments)
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted, ct);

        if (cohort == null)
        {
            return Result<CohortDetailDto>.Failure(Error.NotFound("Cohort.NotFound", "Kohort tapılmadı."));
        }

        // Resource-based icazə yoxlanışı
        if (!isAdmin)
        {
            var isCourseInstructor = cohort.Course != null && cohort.Course.InstructorId == currentUserId;
            var isAssignedTeacher = cohort.TeacherId == currentUserId;
            var isEnrolledStudent = cohort.Enrollments.Any(e => e.StudentId == currentUserId && !e.IsDeleted);

            if (!isCourseInstructor && !isAssignedTeacher && !isEnrolledStudent)
            {
                return Result<CohortDetailDto>.Failure(Error.Forbidden("Cohort.Forbidden", "Bu kohorta baxmaq səlahiyyətiniz yoxdur."));
            }
        }

        var teacherInfo = await _userLookup.GetUserByIdAsync(cohort.TeacherId, ct);
        var teacherName = teacherInfo?.FullName ?? "Təyin edilməyib";

        var schedules = cohort.Schedules
            .Where(s => !s.IsDeleted)
            .OrderBy(s => s.DayOfWeek)
            .ThenBy(s => s.StartTime)
            .Select(s => new CohortScheduleDto(s.Id, s.DayOfWeek, s.StartTime, s.EndTime, s.RoomOrMeetingUrl))
            .ToList();

        var deadlineUtc = FinalDeadlineHelper.GetFinalDeadlineUtc(cohort);
        var dto = new CohortDetailDto(
            Id: cohort.Id,
            Name: cohort.Name,
            CourseId: cohort.CourseId,
            CourseTitle: cohort.Course?.Title ?? string.Empty,
            TeacherId: cohort.TeacherId,
            TeacherFullName: teacherName,
            StartDate: cohort.StartDate,
            EndDate: cohort.EndDate,
            TotalHours: cohort.TotalHours,
            CompletedHours: cohort.CompletedHours,
            QuizWeight: cohort.QuizWeight,
            MiniProjectWeight: cohort.MiniProjectWeight,
            FinalWeight: cohort.FinalWeight,
            MaxAbsenceLimit: cohort.MaxAbsenceLimit,
            LateToAbsenceRatio: cohort.LateToAbsenceRatio,
            Status: cohort.Status,
            EnrolledStudentCount: cohort.Enrollments.Count(e => !e.IsDeleted),
            Schedules: schedules,
            FinalDeadlineUtc: deadlineUtc,
            FinalDeadlineLocal: FinalDeadlineHelper.ToBakuLocalString(deadlineUtc),
            FinalDeadlineIsEndOfDay: FinalDeadlineHelper.IsEndOfDay(deadlineUtc)
        );

        return Result<CohortDetailDto>.Success(dto);
    }

    public async Task<Result<PagedResult<CohortSummaryDto>>> GetCohortsAsync(
        Guid? courseId,
        Guid? teacherId,
        CohortStatus? status,
        int page,
        int pageSize,
        Guid currentUserId,
        bool isAdmin,
        CancellationToken ct = default)
    {
        var query = _context.Cohorts
            .AsNoTracking()
            .Include(c => c.Course)
            .Include(c => c.Enrollments)
            .Where(c => !c.IsDeleted);

        // Rol və resurs filtrləri
        if (!isAdmin)
        {
            var isTeacherRole = await IsUserInRoleAsync(currentUserId, UserRoles.Teacher, ct);
            var isInstructorRole = await IsUserInRoleAsync(currentUserId, UserRoles.Instructor, ct);

            if (isTeacherRole && !isInstructorRole)
            {
                query = query.Where(c => c.TeacherId == currentUserId);
            }
            else if (isInstructorRole && !isTeacherRole)
            {
                query = query.Where(c => c.Course != null && c.Course.InstructorId == currentUserId);
            }
            else if (!isTeacherRole && !isInstructorRole)
            {
                // Tələbə yalnız yazıldığı kohortları görür
                query = query.Where(c => c.Enrollments.Any(e => e.StudentId == currentUserId && !e.IsDeleted));
            }
        }

        if (courseId.HasValue) query = query.Where(c => c.CourseId == courseId.Value);
        if (teacherId.HasValue) query = query.Where(c => c.TeacherId == teacherId.Value);
        if (status.HasValue) query = query.Where(c => c.Status == status.Value);

        var totalCount = await query.CountAsync(ct);
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var cohorts = await query
            .OrderByDescending(c => c.StartDate)
            .ThenBy(c => c.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var teacherIds = cohorts.Select(c => c.TeacherId).Distinct().ToList();
        var teacherDict = await _userLookup.GetUsersByIdsAsync(teacherIds, ct);

        var items = cohorts.Select(c =>
        {
            var teacher = teacherDict.TryGetValue(c.TeacherId, out var u) ? u : null;
            var deadlineUtc = FinalDeadlineHelper.GetFinalDeadlineUtc(c);
            return new CohortSummaryDto(
                Id: c.Id,
                Name: c.Name,
                CourseId: c.CourseId,
                CourseTitle: c.Course?.Title ?? string.Empty,
                TeacherId: c.TeacherId,
                TeacherFullName: teacher?.FullName ?? "Təyin edilməyib",
                Status: c.Status,
                EnrolledStudentCount: c.Enrollments.Count(e => !e.IsDeleted),
                CompletedHours: c.CompletedHours,
                TotalHours: c.TotalHours,
                StartDate: c.StartDate,
                FinalDeadlineUtc: deadlineUtc,
                FinalDeadlineLocal: FinalDeadlineHelper.ToBakuLocalString(deadlineUtc),
                FinalDeadlineIsEndOfDay: FinalDeadlineHelper.IsEndOfDay(deadlineUtc)
            );
        }).ToList();

        var result = new PagedResult<CohortSummaryDto>(items, totalCount, page, pageSize);
        return Result<PagedResult<CohortSummaryDto>>.Success(result);
    }

    public async Task<Result<bool>> UpdateCohortAsync(
        Guid id,
        UpdateCohortDto dto,
        Guid currentUserId,
        bool isAdmin,
        CancellationToken ct = default)
    {
        var validation = await _updateValidator.ValidateAsync(dto, ct);
        if (!validation.IsValid)
        {
            return Result<bool>.Failure(Error.Validation("Cohort.Validation", string.Join("; ", validation.Errors.Select(e => e.ErrorMessage))));
        }

        var cohort = await _context.Cohorts
            .Include(c => c.Course)
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted, ct);

        if (cohort == null)
        {
            return Result<bool>.Failure(Error.NotFound("Cohort.NotFound", "Kohort tapılmadı."));
        }

        // Səlahiyyət: Yalnız Admin və ya həmin kursun Instructor-u redaktə edə bilər (Teacher redaktə edə bilməz)
        if (!isAdmin && cohort.Course?.InstructorId != currentUserId)
        {
            return Result<bool>.Failure(Error.Forbidden("Cohort.Forbidden", "Kohort parametrini redaktə etmək səlahiyyətiniz yoxdur."));
        }

        cohort.Name = dto.Name.Trim();
        cohort.TeacherId = dto.TeacherId;
        cohort.StartDate = dto.StartDate;
        cohort.EndDate = dto.EndDate;
        cohort.TotalHours = dto.TotalHours;
        cohort.QuizWeight = dto.QuizWeight;
        cohort.MiniProjectWeight = dto.MiniProjectWeight;
        cohort.FinalWeight = dto.FinalWeight;
        cohort.MaxAbsenceLimit = dto.MaxAbsenceLimit;
        cohort.LateToAbsenceRatio = dto.LateToAbsenceRatio;
        cohort.Status = dto.Status;

        await _context.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }

    public async Task<Result<bool>> DeleteCohortAsync(
        Guid id,
        Guid currentUserId,
        bool isAdmin,
        CancellationToken ct = default)
    {
        var cohort = await _context.Cohorts
            .Include(c => c.Course)
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted, ct);

        if (cohort == null)
        {
            return Result<bool>.Failure(Error.NotFound("Cohort.NotFound", "Kohort tapılmadı."));
        }

        if (!isAdmin && cohort.Course?.InstructorId != currentUserId)
        {
            return Result<bool>.Failure(Error.Forbidden("Cohort.Forbidden", "Kohortu silmək səlahiyyətiniz yoxdur."));
        }

        cohort.MarkDeleted();
        await _context.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }

    public async Task<Result<Guid>> AddScheduleAsync(
        Guid cohortId,
        CreateCohortScheduleDto dto,
        Guid currentUserId,
        bool isAdmin,
        CancellationToken ct = default)
    {
        var validation = await _scheduleValidator.ValidateAsync(dto, ct);
        if (!validation.IsValid)
        {
            return Result<Guid>.Failure(Error.Validation("Schedule.Validation", string.Join("; ", validation.Errors.Select(e => e.ErrorMessage))));
        }

        var cohort = await _context.Cohorts
            .Include(c => c.Course)
            .FirstOrDefaultAsync(c => c.Id == cohortId && !c.IsDeleted, ct);

        if (cohort == null)
        {
            return Result<Guid>.Failure(Error.NotFound("Cohort.NotFound", "Kohort tapılmadı."));
        }

        if (!isAdmin && cohort.Course?.InstructorId != currentUserId)
        {
            return Result<Guid>.Failure(Error.Forbidden("Cohort.Forbidden", "Cədvəl əlavə etmək səlahiyyətiniz yoxdur."));
        }

        var schedule = new CohortSchedule
        {
            CohortId = cohortId,
            DayOfWeek = dto.DayOfWeek,
            StartTime = dto.StartTime,
            EndTime = dto.EndTime,
            RoomOrMeetingUrl = dto.RoomOrMeetingUrl?.Trim()
        };

        _context.CohortSchedules.Add(schedule);
        await _context.SaveChangesAsync(ct);

        return Result<Guid>.Success(schedule.Id);
    }

    public async Task<Result<bool>> DeleteScheduleAsync(
        Guid cohortId,
        Guid scheduleId,
        Guid currentUserId,
        bool isAdmin,
        CancellationToken ct = default)
    {
        var cohort = await _context.Cohorts
            .Include(c => c.Course)
            .FirstOrDefaultAsync(c => c.Id == cohortId && !c.IsDeleted, ct);

        if (cohort == null) return Result<bool>.Failure(Error.NotFound("Cohort.NotFound", "Kohort tapılmadı."));

        if (!isAdmin && cohort.Course?.InstructorId != currentUserId)
        {
            return Result<bool>.Failure(Error.Forbidden("Cohort.Forbidden", "Cədvəli silmək səlahiyyətiniz yoxdur."));
        }

        var schedule = await _context.CohortSchedules
            .FirstOrDefaultAsync(s => s.Id == scheduleId && s.CohortId == cohortId && !s.IsDeleted, ct);

        if (schedule == null) return Result<bool>.Failure(Error.NotFound("Schedule.NotFound", "Cədvəl dərsi tapılmadı."));

        schedule.MarkDeleted();
        await _context.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }

    public async Task<Result<bool>> EnrollStudentAsync(
        Guid cohortId,
        Guid studentId,
        Guid currentUserId,
        bool isAdmin,
        CancellationToken ct = default)
    {
        var cohort = await _context.Cohorts
            .Include(c => c.Course)
            .FirstOrDefaultAsync(c => c.Id == cohortId && !c.IsDeleted, ct);

        if (cohort == null) return Result<bool>.Failure(Error.NotFound("Cohort.NotFound", "Kohort tapılmadı."));

        // Səlahiyyət: Yalnız Admin və kurs Instructor-u tələbə qeydiyyatı edə bilər (Teacher edə bilməz)
        if (!isAdmin && cohort.Course?.InstructorId != currentUserId)
        {
            return Result<bool>.Failure(Error.Forbidden("Cohort.Forbidden", "Kohorta tələbə əlavə etmək səlahiyyətiniz yoxdur."));
        }

        var existingEnrollment = await _context.CohortEnrollments
            .FirstOrDefaultAsync(ce => ce.CohortId == cohortId && ce.StudentId == studentId && !ce.IsDeleted, ct);

        if (existingEnrollment != null)
        {
            if (existingEnrollment.Status == CohortEnrollmentStatus.Dropped)
            {
                existingEnrollment.Status = CohortEnrollmentStatus.Enrolled;
                existingEnrollment.DroppedAtUtc = null;
                existingEnrollment.DroppedReason = null;
                await _context.SaveChangesAsync(ct);
                return Result<bool>.Success(true);
            }
            return Result<bool>.Failure(Error.Conflict("Cohort.AlreadyEnrolled", "Tələbə artıq bu kohortda qeydiyyatdadır."));
        }

        var enrollment = new CohortEnrollment
        {
            CohortId = cohortId,
            StudentId = studentId,
            Status = CohortEnrollmentStatus.Enrolled,
            IsProvisional = true,
            EnrolledAtUtc = DateTime.UtcNow
        };

        _context.CohortEnrollments.Add(enrollment);

        // Kurs səviyyəsində də Enrollment mövcud deyilsə yaradırıq (ümumi LMS ilə sinxronluq üçün)
        var courseEnrollment = await _context.Enrollments
            .FirstOrDefaultAsync(e => e.CourseId == cohort.CourseId && e.StudentId == studentId && !e.IsDeleted, ct);

        if (courseEnrollment == null)
        {
            _context.Enrollments.Add(new Enrollment
            {
                CourseId = cohort.CourseId,
                StudentId = studentId,
                EnrolledAtUtc = DateTime.UtcNow,
                ProgressPercentage = 0.0
            });
        }

        await _context.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }

    public async Task<Result<bool>> RemoveStudentAsync(
        Guid cohortId,
        Guid studentId,
        Guid currentUserId,
        bool isAdmin,
        CancellationToken ct = default)
    {
        var cohort = await _context.Cohorts
            .Include(c => c.Course)
            .FirstOrDefaultAsync(c => c.Id == cohortId && !c.IsDeleted, ct);

        if (cohort == null) return Result<bool>.Failure(Error.NotFound("Cohort.NotFound", "Kohort tapılmadı."));

        if (!isAdmin && cohort.Course?.InstructorId != currentUserId)
        {
            return Result<bool>.Failure(Error.Forbidden("Cohort.Forbidden", "Tələbəni kohortdan kənarlaşdırmaq səlahiyyətiniz yoxdur."));
        }

        var enrollment = await _context.CohortEnrollments
            .FirstOrDefaultAsync(ce => ce.CohortId == cohortId && ce.StudentId == studentId && !ce.IsDeleted, ct);

        if (enrollment == null) return Result<bool>.Failure(Error.NotFound("Cohort.StudentNotFound", "Tələbə bu kohortda tapılmadı."));

        enrollment.Status = CohortEnrollmentStatus.Dropped;
        enrollment.DroppedAtUtc = DateTime.UtcNow;
        enrollment.DroppedReason = "Rəhbərlik tərəfindən qrupdan çıxarıldı.";

        await _context.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }

    public async Task<Result<IReadOnlyCollection<CohortStudentDto>>> GetCohortStudentsAsync(
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

        if (cohort == null) return Result<IReadOnlyCollection<CohortStudentDto>>.Failure(Error.NotFound("Cohort.NotFound", "Kohort tapılmadı."));

        // Səlahiyyət yoxlanışı
        if (!isAdmin)
        {
            var isCourseInstructor = cohort.Course != null && cohort.Course.InstructorId == currentUserId;
            var isAssignedTeacher = cohort.TeacherId == currentUserId;
            var isEnrolledStudent = cohort.Enrollments.Any(e => e.StudentId == currentUserId && !e.IsDeleted);

            if (!isCourseInstructor && !isAssignedTeacher && !isEnrolledStudent)
            {
                return Result<IReadOnlyCollection<CohortStudentDto>>.Failure(Error.Forbidden("Cohort.Forbidden", "Bu kohortun tələbələrinə baxmaq səlahiyyətiniz yoxdur."));
            }

            // Tələbə yalnız öz məlumatını görə bilər
            if (isEnrolledStudent && !isCourseInstructor && !isAssignedTeacher)
            {
                return await GetSingleCohortStudentAsync(cohort, currentUserId, ct);
            }
        }

        var activeEnrollments = cohort.Enrollments.Where(e => !e.IsDeleted).ToList();
        var studentIds = activeEnrollments.Select(e => e.StudentId).Distinct().ToList();
        var userDict = await _userLookup.GetUsersByIdsAsync(studentIds, ct);

        var list = new List<CohortStudentDto>();
        foreach (var enr in activeEnrollments)
        {
            var user = userDict.TryGetValue(enr.StudentId, out var u) ? u : null;
            var records = cohort.AttendanceRecords.Where(a => a.StudentId == enr.StudentId && !a.IsDeleted).ToList();

            var absent = records.Count(r => r.Status == AttendanceStatus.Absent);
            var late = records.Count(r => r.Status == AttendanceStatus.Late);
            var effectiveAbsence = absent + (late / cohort.LateToAbsenceRatio);

            list.Add(new CohortStudentDto(
                StudentId: enr.StudentId,
                FullName: user?.FullName ?? "Tələbə",
                Email: user?.Email ?? string.Empty,
                Status: enr.Status,
                EnrolledAtUtc: enr.EnrolledAtUtc,
                OfficialFinalScore: enr.OfficialFinalScore,
                IsProvisional: enr.IsProvisional,
                HasAbsenceWarning: enr.HasAbsenceWarning,
                EffectiveAbsences: effectiveAbsence
            ));
        }

        return Result<IReadOnlyCollection<CohortStudentDto>>.Success(list);
    }

    public async Task<Result<FinishCohortResultDto>> FinishCohortAsync(
        Guid cohortId,
        Guid currentUserId,
        bool isAdmin,
        CancellationToken ct = default)
    {
        var cohort = await _context.Cohorts
            .Include(c => c.Course)
            .Include(c => c.Enrollments)
            .Include(c => c.AttendanceRecords)
            .FirstOrDefaultAsync(c => c.Id == cohortId && !c.IsDeleted, ct);

        if (cohort == null) return Result<FinishCohortResultDto>.Failure(Error.NotFound("Cohort.NotFound", "Kohort tapılmadı."));

        // Səlahiyyət: Yalnız Admin və ya Course Instructor kohortu bitirə bilər (Teacher edə bilməz)
        if (!isAdmin && cohort.Course?.InstructorId != currentUserId)
        {
            return Result<FinishCohortResultDto>.Failure(Error.Forbidden("Cohort.Forbidden", "Kohortu bitirmək səlahiyyətiniz yoxdur."));
        }

        // Kohort statusunu Finished edirik
        cohort.Status = CohortStatus.Finished;
        cohort.EndDate ??= DateTime.UtcNow;

        // Balların rəsmiləşdirilməsi
        await _gradeService.FinalizeCohortGradesAsync(cohortId, currentUserId, isAdmin, ct);

        // Hər bir tələbə üçün sertifikat siyasətinin qiymətləndirilməsi
        var enrollments = cohort.Enrollments.Where(e => !e.IsDeleted).ToList();
        var studentIds = enrollments.Select(e => e.StudentId).Distinct().ToList();
        var studentDict = await _userLookup.GetUsersByIdsAsync(studentIds, ct);

        int completedCount = 0;
        int droppedCount = 0;
        int diplomasIssued = 0;
        int attendancesIssued = 0;

        foreach (var enrollment in enrollments)
        {
            var records = cohort.AttendanceRecords.Where(r => r.StudentId == enrollment.StudentId && !r.IsDeleted).ToList();
            var absents = records.Count(r => r.Status == AttendanceStatus.Absent);
            var lates = records.Count(r => r.Status == AttendanceStatus.Late);
            var effectiveAbsences = absents + (lates / cohort.LateToAbsenceRatio);

            var score = enrollment.OfficialFinalScore ?? 0m;
            var eval = _eligibilityPolicy.Evaluate(enrollment, score, effectiveAbsences, cohort.MaxAbsenceLimit, isCohortFinished: true);

            if (effectiveAbsences > cohort.MaxAbsenceLimit || enrollment.Status == CohortEnrollmentStatus.Dropped)
            {
                enrollment.Status = CohortEnrollmentStatus.Dropped;
                enrollment.DroppedAtUtc ??= DateTime.UtcNow;
                enrollment.DroppedReason ??= "Limitdən artıq qayıb səbəbilə bitirmə zamanı Dropped qeydə alındı.";
                droppedCount++;
            }
            else
            {
                enrollment.Status = CohortEnrollmentStatus.Completed;
                enrollment.CompletedAtUtc = DateTime.UtcNow;
                completedCount++;

                if (eval.Result == CertificateEligibilityResult.Diploma || eval.Result == CertificateEligibilityResult.Attendance)
                {
                    var certType = eval.Result == CertificateEligibilityResult.Diploma ? CertificateType.Diploma : CertificateType.Attendance;
                    var existingCert = await _context.Certificates
                        .FirstOrDefaultAsync(c => c.CohortId == cohortId && c.StudentId == enrollment.StudentId && !c.IsDeleted, ct);

                    if (existingCert == null)
                    {
                        var studentName = studentDict.TryGetValue(enrollment.StudentId, out var s) ? s.FullName : "Məzun";
                        var randomCode = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
                        var verifySecret = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N")[..8];
                        var prefix = certType == CertificateType.Diploma ? "PRAGMA-DIP" : "PRAGMA-ATT";
                        var certCode = $"{prefix}-{DateTime.UtcNow.Year}-{randomCode}";

                        var certificate = new Certificate
                        {
                            StudentId = enrollment.StudentId,
                            CourseId = cohort.CourseId,
                            CohortId = cohortId,
                            Type = certType,
                            CertificateCode = certCode,
                            VerificationCode = verifySecret,
                            StudentFullName = studentName,
                            CourseTitle = cohort.Course?.Title ?? cohort.Name,
                            GradePercentage = (double)(enrollment.OfficialFinalScore ?? eval.OfficialGrade),
                            IssuedAtUtc = DateTime.UtcNow
                        };

                        _context.Certificates.Add(certificate);
                        if (certType == CertificateType.Diploma) diplomasIssued++;
                        else attendancesIssued++;
                    }
                }
            }
        }

        await _context.SaveChangesAsync(ct);

        return Result<FinishCohortResultDto>.Success(new FinishCohortResultDto(
            CohortId: cohort.Id,
            CohortName: cohort.Name,
            TotalStudents: enrollments.Count,
            CompletedStudents: completedCount,
            DroppedStudents: droppedCount,
            DiplomasIssued: diplomasIssued,
            AttendancesIssued: attendancesIssued
        ));
    }

    private async Task<Result<IReadOnlyCollection<CohortStudentDto>>> GetSingleCohortStudentAsync(
        Cohort cohort,
        Guid studentId,
        CancellationToken ct)
    {
        var enrollment = cohort.Enrollments.FirstOrDefault(e => e.StudentId == studentId && !e.IsDeleted);
        if (enrollment == null)
        {
            return Result<IReadOnlyCollection<CohortStudentDto>>.Success([]);
        }

        var user = await _userLookup.GetUserByIdAsync(studentId, ct);
        var records = cohort.AttendanceRecords.Where(a => a.StudentId == studentId && !a.IsDeleted).ToList();
        var absent = records.Count(r => r.Status == AttendanceStatus.Absent);
        var late = records.Count(r => r.Status == AttendanceStatus.Late);
        var effectiveAbsence = absent + (late / cohort.LateToAbsenceRatio);

        var dto = new CohortStudentDto(
            StudentId: enrollment.StudentId,
            FullName: user?.FullName ?? "Tələbə",
            Email: user?.Email ?? string.Empty,
            Status: enrollment.Status,
            EnrolledAtUtc: enrollment.EnrolledAtUtc,
            OfficialFinalScore: enrollment.OfficialFinalScore,
            IsProvisional: enrollment.IsProvisional,
            HasAbsenceWarning: enrollment.HasAbsenceWarning,
            EffectiveAbsences: effectiveAbsence
        );

        return Result<IReadOnlyCollection<CohortStudentDto>>.Success([dto]);
    }

    public async Task<Result<SetFinalDeadlineResultDto>> SetFinalDeadlineAsync(
        Guid cohortId,
        SetCohortFinalDeadlineDto dto,
        Guid currentUserId,
        bool isAdmin,
        CancellationToken ct = default)
    {
        var cohort = await _context.Cohorts
            .Include(c => c.Course)
            .FirstOrDefaultAsync(c => c.Id == cohortId && !c.IsDeleted, ct);

        if (cohort == null)
        {
            return Result<SetFinalDeadlineResultDto>.Failure(Error.NotFound("Cohort.NotFound", "Kohort tapılmadı."));
        }

        if (!isAdmin && cohort.TeacherId != currentUserId && cohort.Course?.InstructorId != currentUserId)
        {
            return Result<SetFinalDeadlineResultDto>.Failure(Error.Forbidden("Cohort.Forbidden", "Final müddətini təyin etmək səlahiyyətiniz yoxdur."));
        }

        if (cohort.Status == CohortStatus.Finished)
        {
            return Result<SetFinalDeadlineResultDto>.Failure(Error.Conflict("Cohort.AlreadyFinished", "Finalize olunmuş kohortun təhvil müddəti dəyişdirilə bilməz."));
        }

        // Əgər tarix boşdursa, xüsusi müddət təmizlənir və standart EndDate əsas götürülür
        if (string.IsNullOrWhiteSpace(dto.Date))
        {
            cohort.FinalDeadlineUtc = null;
            cohort.MarkUpdated();
            await _context.SaveChangesAsync(ct);

            var defaultDeadlineUtc = FinalDeadlineHelper.GetFinalDeadlineUtc(cohort);
            return Result<SetFinalDeadlineResultDto>.Success(new SetFinalDeadlineResultDto(
                FinalDeadlineUtc: defaultDeadlineUtc,
                FinalDeadlineLocal: FinalDeadlineHelper.ToBakuLocalString(defaultDeadlineUtc),
                IsEndOfDay: FinalDeadlineHelper.IsEndOfDay(defaultDeadlineUtc),
                IsPastWarning: false,
                WarningMessage: "Müddət təmizləndi və standart EndDate tarixinə qaytarıldı."
            ));
        }

        if (!DateOnly.TryParseExact(dto.Date.Trim(), "yyyy-MM-dd", out var date))
        {
            return Result<SetFinalDeadlineResultDto>.Failure(Error.Validation("FinalDeadline.InvalidDate", "Tarix 'yyyy-MM-dd' formatında olmalıdır."));
        }

        TimeOnly time = new TimeOnly(23, 59, 59);
        if (!string.IsNullOrWhiteSpace(dto.Time))
        {
            if (TimeOnly.TryParseExact(dto.Time.Trim(), "HH:mm", out var parsedTime) ||
                TimeOnly.TryParseExact(dto.Time.Trim(), "HH:mm:ss", out parsedTime))
            {
                time = parsedTime;
            }
            else
            {
                return Result<SetFinalDeadlineResultDto>.Failure(Error.Validation("FinalDeadline.InvalidTime", "Saat 'HH:mm' formatında olmalıdır."));
            }
        }

        var startBaku = DateOnly.FromDateTime(FinalDeadlineHelper.ToBakuLocal(cohort.StartDate));
        if (date < startBaku)
        {
            return Result<SetFinalDeadlineResultDto>.Failure(Error.Validation("FinalDeadline.BeforeStart", "Final müddəti kohortun başlama tarixindən əvvəl ola bilməz."));
        }

        var deadlineUtc = FinalDeadlineHelper.ConvertBakuToUtc(date, time);
        bool isPastWarning = deadlineUtc < DateTime.UtcNow;
        string? warningMessage = isPastWarning ? "Xəbərdarlıq: Təyin edilmiş müddət keçmiş zamana təsadüf edir." : null;

        cohort.FinalDeadlineUtc = deadlineUtc;
        cohort.MarkUpdated();
        await _context.SaveChangesAsync(ct);

        return Result<SetFinalDeadlineResultDto>.Success(new SetFinalDeadlineResultDto(
            FinalDeadlineUtc: cohort.FinalDeadlineUtc,
            FinalDeadlineLocal: FinalDeadlineHelper.ToBakuLocalString(cohort.FinalDeadlineUtc),
            IsEndOfDay: FinalDeadlineHelper.IsEndOfDay(cohort.FinalDeadlineUtc),
            IsPastWarning: isPastWarning,
            WarningMessage: warningMessage
        ));
    }

    public async Task<Result<StudentFinalSubmissionDto>> SubmitFinalProjectAsync(
        Guid cohortId,
        SubmitFinalProjectDto dto,
        Guid studentId,
        CancellationToken ct = default)
    {
        var cohort = await _context.Cohorts
            .Include(c => c.Enrollments)
            .FirstOrDefaultAsync(c => c.Id == cohortId && !c.IsDeleted, ct);

        if (cohort == null)
        {
            return Result<StudentFinalSubmissionDto>.Failure(Error.NotFound("Cohort.NotFound", "Kohort tapılmadı."));
        }

        var enrollment = cohort.Enrollments.FirstOrDefault(e => e.StudentId == studentId && !e.IsDeleted);
        if (enrollment == null)
        {
            return Result<StudentFinalSubmissionDto>.Failure(Error.Forbidden("Cohort.NotEnrolled", "Tələbə bu kohortda qeydiyyatda deyil."));
        }

        if (enrollment.Status == CohortEnrollmentStatus.Dropped)
        {
            return Result<StudentFinalSubmissionDto>.Failure(Error.Forbidden("Cohort.Dropped", "Qrupdan xaric edilmiş (Dropped) tələbələr final layihə təhvil verə bilməz."));
        }

        if (FinalDeadlineHelper.IsFinalDeadlinePassed(cohort, DateTime.UtcNow))
        {
            return Result<StudentFinalSubmissionDto>.Failure(Error.Conflict("FinalSubmission.DeadlinePassed", "Təhvil müddəti bitib."));
        }

        var submission = await _context.FinalSubmissions
            .FirstOrDefaultAsync(s => s.CohortId == cohortId && s.StudentId == studentId && !s.IsDeleted, ct);

        var now = DateTime.UtcNow;
        if (submission != null)
        {
            submission.Url = dto.Url.Trim();
            submission.MarkUpdated();
        }
        else
        {
            submission = new FinalSubmission
            {
                CohortId = cohortId,
                StudentId = studentId,
                Url = dto.Url.Trim(),
                SubmittedAtUtc = now
            };
            _context.FinalSubmissions.Add(submission);
        }

        await _context.SaveChangesAsync(ct);

        var gradeEntry = await _context.CohortGradeEntries
            .FirstOrDefaultAsync(g => g.CohortId == cohortId && g.StudentId == studentId && g.ComponentType == GradeComponentType.Final && !g.IsDeleted, ct);

        bool isGraded = gradeEntry != null;
        decimal? finalGrade = gradeEntry?.Score;
        string status = isGraded ? "Graded" : "Submitted";
        var deadlineUtc = FinalDeadlineHelper.GetFinalDeadlineUtc(cohort);

        return Result<StudentFinalSubmissionDto>.Success(new StudentFinalSubmissionDto(
            Url: submission.Url,
            SubmittedAtUtc: submission.SubmittedAtUtc,
            UpdatedAtUtc: submission.UpdatedAtUtc,
            FinalDeadlineLocal: FinalDeadlineHelper.ToBakuLocalString(deadlineUtc),
            IsDeadlinePassed: false,
            IsGraded: isGraded,
            FinalGrade: finalGrade,
            Status: status
        ));
    }

    public async Task<Result<StudentFinalSubmissionDto>> GetMyFinalSubmissionAsync(
        Guid cohortId,
        Guid studentId,
        CancellationToken ct = default)
    {
        var cohort = await _context.Cohorts
            .Include(c => c.Enrollments)
            .FirstOrDefaultAsync(c => c.Id == cohortId && !c.IsDeleted, ct);

        if (cohort == null)
        {
            return Result<StudentFinalSubmissionDto>.Failure(Error.NotFound("Cohort.NotFound", "Kohort tapılmadı."));
        }

        var enrollment = cohort.Enrollments.FirstOrDefault(e => e.StudentId == studentId && !e.IsDeleted);
        if (enrollment == null)
        {
            return Result<StudentFinalSubmissionDto>.Failure(Error.Forbidden("Cohort.NotEnrolled", "Tələbə bu kohortda qeydiyyatda deyil."));
        }

        var submission = await _context.FinalSubmissions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.CohortId == cohortId && s.StudentId == studentId && !s.IsDeleted, ct);

        var gradeEntry = await _context.CohortGradeEntries
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.CohortId == cohortId && g.StudentId == studentId && g.ComponentType == GradeComponentType.Final && !g.IsDeleted, ct);

        bool isGraded = gradeEntry != null;
        decimal? finalGrade = gradeEntry?.Score;
        bool isDeadlinePassed = FinalDeadlineHelper.IsFinalDeadlinePassed(cohort, DateTime.UtcNow);

        string status;
        if (isGraded)
        {
            status = "Graded";
        }
        else if (submission != null && !string.IsNullOrWhiteSpace(submission.Url))
        {
            status = "Submitted";
        }
        else if (isDeadlinePassed)
        {
            status = "Missed";
        }
        else
        {
            status = "NotSubmitted";
        }

        var deadlineUtc = FinalDeadlineHelper.GetFinalDeadlineUtc(cohort);

        return Result<StudentFinalSubmissionDto>.Success(new StudentFinalSubmissionDto(
            Url: submission?.Url,
            SubmittedAtUtc: submission?.SubmittedAtUtc,
            UpdatedAtUtc: submission?.UpdatedAtUtc,
            FinalDeadlineLocal: FinalDeadlineHelper.ToBakuLocalString(deadlineUtc),
            IsDeadlinePassed: isDeadlinePassed,
            IsGraded: isGraded,
            FinalGrade: finalGrade,
            Status: status
        ));
    }

    public async Task<Result<PagedResult<CohortFinalSubmissionItemDto>>> GetCohortFinalSubmissionsAsync(
        Guid cohortId,
        int page,
        int pageSize,
        Guid currentUserId,
        bool isAdmin,
        CancellationToken ct = default)
    {
        var cohort = await _context.Cohorts
            .Include(c => c.Course)
            .Include(c => c.Enrollments)
            .FirstOrDefaultAsync(c => c.Id == cohortId && !c.IsDeleted, ct);

        if (cohort == null)
        {
            return Result<PagedResult<CohortFinalSubmissionItemDto>>.Failure(Error.NotFound("Cohort.NotFound", "Kohort tapılmadı."));
        }

        if (!isAdmin && cohort.TeacherId != currentUserId && cohort.Course?.InstructorId != currentUserId)
        {
            return Result<PagedResult<CohortFinalSubmissionItemDto>>.Failure(Error.Forbidden("Cohort.Forbidden", "Final təhvili siyahısına baxmaq səlahiyyətiniz yoxdur."));
        }

        if (pageSize > 100) pageSize = 100;
        if (pageSize < 1) pageSize = 20;
        if (page < 1) page = 1;

        var activeEnrollments = cohort.Enrollments
            .Where(e => !e.IsDeleted)
            .OrderByDescending(e => e.EnrolledAtUtc)
            .ToList();

        var totalCount = activeEnrollments.Count;
        var pagedEnrollments = activeEnrollments
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var pagedStudentIds = pagedEnrollments.Select(e => e.StudentId).ToList();
        var userDict = await _userLookup.GetUsersByIdsAsync(pagedStudentIds, ct);

        var submissions = await _context.FinalSubmissions
            .AsNoTracking()
            .Where(s => s.CohortId == cohortId && pagedStudentIds.Contains(s.StudentId) && !s.IsDeleted)
            .ToListAsync(ct);
        var subDict = submissions.ToDictionary(s => s.StudentId);

        var grades = await _context.CohortGradeEntries
            .AsNoTracking()
            .Where(g => g.CohortId == cohortId && pagedStudentIds.Contains(g.StudentId) && g.ComponentType == GradeComponentType.Final && !g.IsDeleted)
            .ToListAsync(ct);
        var gradeDict = grades.ToDictionary(g => g.StudentId);

        var deadlineUtc = FinalDeadlineHelper.GetFinalDeadlineUtc(cohort);
        var isDeadlinePassed = FinalDeadlineHelper.IsFinalDeadlinePassed(cohort, DateTime.UtcNow);

        var items = new List<CohortFinalSubmissionItemDto>();
        foreach (var enr in pagedEnrollments)
        {
            userDict.TryGetValue(enr.StudentId, out var user);
            subDict.TryGetValue(enr.StudentId, out var sub);
            gradeDict.TryGetValue(enr.StudentId, out var grade);

            bool hasFinalGrade = grade != null;
            bool onTime = sub != null && (deadlineUtc == null || sub.SubmittedAtUtc <= deadlineUtc.Value);

            string status;
            if (hasFinalGrade)
            {
                status = "Graded";
            }
            else if (sub != null && !string.IsNullOrWhiteSpace(sub.Url))
            {
                status = "Submitted";
            }
            else if (isDeadlinePassed)
            {
                status = "Missed";
            }
            else
            {
                status = "NotSubmitted";
            }

            items.Add(new CohortFinalSubmissionItemDto(
                StudentId: enr.StudentId,
                FullName: user?.FullName ?? "Tələbə",
                Email: user?.Email ?? string.Empty,
                Url: sub?.Url,
                SubmittedAtUtc: sub?.SubmittedAtUtc,
                OnTime: onTime,
                HasFinalGrade: hasFinalGrade,
                FinalGrade: grade?.Score,
                Status: status
            ));
        }

        return Result<PagedResult<CohortFinalSubmissionItemDto>>.Success(new PagedResult<CohortFinalSubmissionItemDto>(items, totalCount, page, pageSize));
    }

    private async Task<bool> IsUserInRoleAsync(Guid userId, string role, CancellationToken ct)
    {
        var ids = await _userLookup.GetUserIdsInRoleAsync(role, ct);
        return ids.Contains(userId);
    }
}
