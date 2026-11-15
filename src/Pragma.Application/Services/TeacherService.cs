using Microsoft.EntityFrameworkCore;
using Pragma.Application.Common;
using Pragma.Application.Contracts;
using Pragma.Application.DTOs.Teacher;
using Pragma.Domain.Entities;
using Pragma.Domain.Enums;

namespace Pragma.Application.Services;

public class TeacherService : ITeacherService
{
    private readonly IAppDbContext _context;
    private readonly IUserLookupService _userLookup;

    public TeacherService(
        IAppDbContext context,
        IUserLookupService userLookup)
    {
        _context = context;
        _userLookup = userLookup;
    }

    public async Task<Result<IReadOnlyCollection<AssignedGroupSummaryDto>>> GetAssignedGroupsAsync(
        Guid teacherId,
        bool isAdmin,
        CancellationToken ct = default)
    {
        // 1. Kohortlar (Stage 2 Akademik Nüvə)
        var cohortQuery = _context.Cohorts
            .AsNoTracking()
            .Include(c => c.Course)
                .ThenInclude(crs => crs!.Category)
            .Include(c => c.Course)
                .ThenInclude(crs => crs!.Modules)
                    .ThenInclude(m => m.Lessons)
            .Include(c => c.Enrollments)
            .Where(c => !c.IsDeleted);

        if (!isAdmin)
        {
            cohortQuery = cohortQuery.Where(c => c.TeacherId == teacherId || (c.Course != null && c.Course.InstructorId == teacherId));
        }

        var cohorts = await cohortQuery.ToListAsync(ct);

        if (cohorts.Count > 0)
        {
            var cohortList = new List<AssignedGroupSummaryDto>();
            foreach (var c in cohorts)
            {
                var activeEnrollments = c.Enrollments.Where(e => !e.IsDeleted).ToList();
                var lessons = c.Course?.Modules.Where(m => !m.IsDeleted).SelectMany(m => m.Lessons.Where(l => !l.IsDeleted)).ToList() ?? new();
                var totalLessons = lessons.Count;
                var totalHours = c.TotalHours > 0 ? c.TotalHours : (totalLessons * 2.0);
                var completedHours = c.CompletedHours;
                var remainingHours = Math.Max(0.0, Math.Round(totalHours - completedHours, 1));
                var cohortCode = c.Name.Contains(':') ? c.Name.Split(':')[0].Trim() : null;
                var courseTitle = !string.IsNullOrWhiteSpace(c.Course?.Title)
                    ? (cohortCode != null ? $"{cohortCode}: {c.Course.Title}" : c.Course.Title)
                    : c.Name;

                var deadlineUtc = FinalDeadlineHelper.GetFinalDeadlineUtc(c);
                var deadlineLocal = FinalDeadlineHelper.ToBakuLocalString(deadlineUtc);
                var isEod = c.FinalDeadlineUtc.HasValue ? FinalDeadlineHelper.IsEndOfDay(c.FinalDeadlineUtc.Value) : true;

                cohortList.Add(new AssignedGroupSummaryDto(
                    c.Id,
                    courseTitle,
                    c.Course?.Category?.Name ?? "Ümumi",
                    activeEnrollments.Count,
                    totalLessons,
                    totalHours,
                    completedHours,
                    remainingHours,
                    0.0,
                    c.Id,
                    c.CourseId,
                    deadlineUtc,
                    deadlineLocal,
                    isEod,
                    c.Status.ToString()
                ));
            }
            return Result<IReadOnlyCollection<AssignedGroupSummaryDto>>.Success(cohortList);
        }

        // 2. Kurslar (Fallback)
        var query = _context.Courses
            .AsNoTracking()
            .Include(c => c.Category)
            .Include(c => c.Enrollments)
            .Include(c => c.Modules)
                .ThenInclude(m => m.Lessons)
            .Where(c => !c.IsDeleted);

        // Yalnız özünə təyin olunmuş kurslar/qruplar (Admin istisna)
        if (!isAdmin)
        {
            query = query.Where(c => c.InstructorId == teacherId);
        }

        var courses = await query.ToListAsync(ct);
        var resultList = new List<AssignedGroupSummaryDto>();

        foreach (var course in courses)
        {
            var activeEnrollments = course.Enrollments.Where(e => !e.IsDeleted).ToList();
            var totalStudents = activeEnrollments.Count;

            var lessons = course.Modules.Where(m => !m.IsDeleted).SelectMany(m => m.Lessons.Where(l => !l.IsDeleted)).ToList();
            var totalLessons = lessons.Count;

            // Hər dərs təxminən 2 akademik saat təşkil edir (minimum 1 dərs = 2 saat)
            var totalHours = totalLessons > 0 ? totalLessons * 2.0 : 0.0;
            var avgProgress = activeEnrollments.Any() ? activeEnrollments.Average(e => e.ProgressPercentage) : 0.0;
            var completedHours = Math.Round(totalHours * (avgProgress / 100.0), 1);
            var remainingHours = Math.Max(0.0, Math.Round(totalHours - completedHours, 1));

            // Ortalama bal
            double avgGrade = 0.0;
            if (activeEnrollments.Any())
            {
                var grades = activeEnrollments.Select(e => CalculateOfficialGrade(e, 0.0).Score ?? 0m).ToList();
                avgGrade = grades.Count > 0 ? Math.Round((double)grades.Average(), 1) : 0.0;
            }

            resultList.Add(new AssignedGroupSummaryDto(
                course.Id,
                course.Title,
                course.Category?.Name ?? "Ümumi",
                totalStudents,
                totalLessons,
                totalHours,
                completedHours,
                remainingHours,
                avgGrade,
                null,
                course.Id
            ));
        }

        return Result<IReadOnlyCollection<AssignedGroupSummaryDto>>.Success(resultList);
    }

    public async Task<Result<IReadOnlyCollection<GroupStudentDto>>> GetGroupStudentsAsync(
        Guid groupId,
        Guid teacherId,
        bool isAdmin,
        CancellationToken ct = default)
    {
        // 1. Kohort yoxlanışı
        var cohort = await _context.Cohorts
            .AsNoTracking()
            .Include(c => c.Course)
                .ThenInclude(crs => crs!.Modules)
                    .ThenInclude(m => m.Lessons)
                        .ThenInclude(l => l.Quiz)
            .Include(c => c.Enrollments)
            .Include(c => c.AttendanceRecords)
            .Include(c => c.GradeEntries)
            .FirstOrDefaultAsync(c => c.Id == groupId && !c.IsDeleted, ct);

        if (cohort != null)
        {
            if (!isAdmin && cohort.TeacherId != teacherId && cohort.Course?.InstructorId != teacherId)
            {
                return Result<IReadOnlyCollection<GroupStudentDto>>.Failure(Error.Forbidden("Cohort.Forbidden", "Bu kohortun tələbələrinə baxmaq səlahiyyətiniz yoxdur."));
            }

            var activeEnrollments = cohort.Enrollments.Where(e => !e.IsDeleted).OrderByDescending(e => e.EnrolledAtUtc).ToList();
            var studentIds = activeEnrollments.Select(e => e.StudentId).Distinct().ToList();
            var userDict = await _userLookup.GetUsersByIdsAsync(studentIds, ct);

            var lessonIds = cohort.Course?.Modules.Where(m => !m.IsDeleted).SelectMany(m => m.Lessons.Where(l => !l.IsDeleted)).Select(l => l.Id).ToList() ?? new();
            var quizzes = await _context.Quizzes
                .AsNoTracking()
                .Where(q => lessonIds.Contains(q.LessonId) && !q.IsDeleted)
                .Select(q => q.Id)
                .ToListAsync(ct);

            var attempts = await _context.QuizAttempts
                .AsNoTracking()
                .Where(qa => quizzes.Contains(qa.QuizId) && studentIds.Contains(qa.StudentId) && !qa.IsDeleted)
                .ToListAsync(ct);

            var submissions = await _context.FinalSubmissions
                .AsNoTracking()
                .Where(s => s.CohortId == groupId && studentIds.Contains(s.StudentId) && !s.IsDeleted)
                .ToListAsync(ct);
            var subDict = submissions.ToDictionary(s => s.StudentId);

            var cohortStudentList = new List<GroupStudentDto>();

            foreach (var enr in activeEnrollments)
            {
                userDict.TryGetValue(enr.StudentId, out var user);
                var fullName = user?.FullName ?? "Tələbə";
                var email = user?.Email ?? string.Empty;

                // Quiz ortalaması
                var studentAttempts = attempts.Where(a => a.StudentId == enr.StudentId).ToList();
                double quizAvg = 0.0;
                if (studentAttempts.Any() && quizzes.Any())
                {
                    var bestScores = quizzes
                        .Select(qId => studentAttempts.Where(a => a.QuizId == qId).Select(a => a.ScorePercentage).DefaultIfEmpty(-1).Max())
                        .Where(score => score >= 0)
                        .ToList();

                    if (bestScores.Any())
                    {
                        quizAvg = Math.Round(bestScores.Average(), 1);
                    }
                }

                // Mini və Final balları (CohortGradeEntries)
                var studentGrades = cohort.GradeEntries.Where(g => g.StudentId == enr.StudentId && !g.IsDeleted).ToList();
                var miniEntry = studentGrades.FirstOrDefault(g => g.ComponentType == GradeComponentType.MiniProject);
                var finalEntry = studentGrades.FirstOrDefault(g => g.ComponentType == GradeComponentType.Final);
                decimal? miniScore = miniEntry?.Score;
                decimal? finalScoreVal = finalEntry?.Score;

                // Davamiyyət (AttendanceRecords)
                var studentAttendance = cohort.AttendanceRecords.Where(a => a.StudentId == enr.StudentId && !a.IsDeleted).ToList();
                var absentCount = studentAttendance.Count(a => a.Status == AttendanceStatus.Absent);
                var lateCount = studentAttendance.Count(a => a.Status == AttendanceStatus.Late);
                var totalAbsences = absentCount + (lateCount / (cohort.LateToAbsenceRatio > 0 ? cohort.LateToAbsenceRatio : 3));

                // Akademik Yekun Bal
                var (officialScore, isProvisional) = CalculateCohortStudentGrade(
                    quizAvg, miniScore, finalScoreVal,
                    cohort.QuizWeight, cohort.MiniProjectWeight, cohort.FinalWeight,
                    cohort.Status == CohortStatus.Finished);

                var isAllEmpty = quizAvg <= 0 && !miniScore.HasValue && !finalScoreVal.HasValue;

                string status;
                if (totalAbsences > cohort.MaxAbsenceLimit)
                {
                    status = "Dropped";
                }
                else if (cohort.Status == CohortStatus.Finished)
                {
                    status = "Tamamlayıb";
                }
                else if (isAllEmpty)
                {
                    status = "Qiymətləndirilməyib";
                }
                else
                {
                    status = "Aktiv";
                }

                subDict.TryGetValue(enr.StudentId, out var sub);

                cohortStudentList.Add(new GroupStudentDto(
                    enr.StudentId,
                    fullName,
                    email,
                    enr.EnrolledAtUtc,
                    0.0,
                    quizAvg,
                    miniScore,
                    finalScoreVal,
                    absentCount,
                    lateCount,
                    totalAbsences,
                    officialScore,
                    isProvisional,
                    miniEntry?.Notes ?? finalEntry?.Notes,
                    status,
                    sub?.Url,
                    sub?.SubmittedAtUtc
                ));
            }

            return Result<IReadOnlyCollection<GroupStudentDto>>.Success(cohortStudentList);
        }

        // 2. Kurs yoxlanışı (Fallback)
        var course = await _context.Courses
            .AsNoTracking()
            .Include(c => c.Modules)
                .ThenInclude(m => m.Lessons)
            .FirstOrDefaultAsync(c => c.Id == groupId && !c.IsDeleted, ct);

        if (course == null)
        {
            return Result<IReadOnlyCollection<GroupStudentDto>>.Failure(Error.NotFound("Group.NotFound", "Qrup tapılmadı."));
        }

        // Resource-based yoxlama: Müəllimin öz qrupudurmu?
        if (!isAdmin && course.InstructorId != teacherId)
        {
            return Result<IReadOnlyCollection<GroupStudentDto>>.Failure(Error.Forbidden("Group.Forbidden", "Bu qrupun tələbələrinə baxmaq səlahiyyətiniz yoxdur."));
        }

        var enrollments = await _context.Enrollments
            .AsNoTracking()
            .Where(e => e.CourseId == groupId && !e.IsDeleted)
            .OrderByDescending(e => e.EnrolledAtUtc)
            .ToListAsync(ct);

        var courseStudentIds = enrollments.Select(e => e.StudentId).Distinct().ToList();
        var courseUserDict = await _userLookup.GetUsersByIdsAsync(courseStudentIds, ct);

        // Kviz nəticələrini topla (ən yaxşı cəhd qaydası ilə)
        var courseLessonIds = course.Modules.SelectMany(m => m.Lessons).Select(l => l.Id).ToList();
        var courseQuizzes = await _context.Quizzes
            .AsNoTracking()
            .Where(q => courseLessonIds.Contains(q.LessonId) && !q.IsDeleted)
            .Select(q => q.Id)
            .ToListAsync(ct);

        var courseAttempts = await _context.QuizAttempts
            .AsNoTracking()
            .Where(qa => courseQuizzes.Contains(qa.QuizId) && courseStudentIds.Contains(qa.StudentId) && !qa.IsDeleted)
            .ToListAsync(ct);

        var resultList = new List<GroupStudentDto>();

        foreach (var enrollment in enrollments)
        {
            courseUserDict.TryGetValue(enrollment.StudentId, out var user);
            var fullName = user?.FullName ?? "Tələbə";
            var email = user?.Email ?? string.Empty;

            // Kviz ortalaması (hər kviz üzrə ən yüksək balın ortalaması)
            var studentAttempts = courseAttempts.Where(a => a.StudentId == enrollment.StudentId).ToList();
            double quizAvg = 0.0;
            if (studentAttempts.Any() && courseQuizzes.Any())
            {
                var bestScores = courseQuizzes
                    .Select(qId => studentAttempts.Where(a => a.QuizId == qId).Select(a => a.ScorePercentage).DefaultIfEmpty(-1).Max())
                    .Where(score => score >= 0)
                    .ToList();

                if (bestScores.Any())
                {
                    quizAvg = Math.Round(bestScores.Average(), 1);
                }
            }

            var (finalScore, isProvisional) = CalculateOfficialGrade(enrollment, quizAvg);
            var totalAbsences = enrollment.AbsentCount + (enrollment.LateCount / 3);
            var isAllEmpty = quizAvg <= 0 && !enrollment.MiniProjectGrade.HasValue && !enrollment.FinalGrade.HasValue;

            string status;
            if (totalAbsences > 6)
            {
                status = "Dropped";
            }
            else if (enrollment.IsCompleted)
            {
                status = "Tamamlayıb";
            }
            else if (isAllEmpty)
            {
                status = "Qiymətləndirilməyib";
            }
            else
            {
                status = "Aktiv";
            }

            resultList.Add(new GroupStudentDto(
                enrollment.StudentId,
                fullName,
                email,
                enrollment.EnrolledAtUtc,
                enrollment.ProgressPercentage,
                quizAvg,
                enrollment.MiniProjectGrade,
                enrollment.FinalGrade,
                enrollment.AbsentCount,
                enrollment.LateCount,
                totalAbsences,
                finalScore,
                isProvisional,
                enrollment.TeacherFeedback,
                status
            ));
        }

        return Result<IReadOnlyCollection<GroupStudentDto>>.Success(resultList);
    }

    public async Task<Result<bool>> AssignGradeAsync(
        AssignGradeDto dto,
        Guid teacherId,
        bool isAdmin,
        CancellationToken ct = default)
    {
        // 1. Əvvəlcə qrupun Kohort olub-olmaması yoxlanılır (Stage 2/3 Akademik Model)
        var cohort = await _context.Cohorts
            .Include(c => c.Course)
            .Include(c => c.Enrollments)
            .Include(c => c.GradeEntries)
            .Include(c => c.AttendanceRecords)
            .FirstOrDefaultAsync(c => c.Id == dto.GroupId && !c.IsDeleted, ct);

        if (cohort != null)
        {
            if (!isAdmin && cohort.TeacherId != teacherId && cohort.Course?.InstructorId != teacherId)
            {
                return Result<bool>.Failure(Error.Forbidden("Group.Forbidden", "Yalnız sizə təyin olunmuş qrupun tələbələrinə bal verə bilərsiniz."));
            }

            var cohortEnrollment = cohort.Enrollments.FirstOrDefault(e => e.StudentId == dto.StudentId && !e.IsDeleted);
            if (cohortEnrollment == null)
            {
                return Result<bool>.Failure(Error.NotFound("Enrollment.NotFound", "Tələbə bu qrupda qeydiyyatda deyil."));
            }

            var now = DateTime.UtcNow;

            // 1.1 Mini Layihə balı
            if (dto.MiniProjectGrade.HasValue)
            {
                if (dto.MiniProjectGrade.Value < 0 || dto.MiniProjectGrade.Value > 100)
                    return Result<bool>.Failure(Error.Validation("Grade.Invalid", "Mini layihə balı 0 ilə 100 arasında olmalıdır."));

                var existingMini = cohort.GradeEntries.FirstOrDefault(g => g.StudentId == dto.StudentId && g.ComponentType == GradeComponentType.MiniProject && !g.IsDeleted);
                if (existingMini != null)
                {
                    existingMini.Score = Math.Round(dto.MiniProjectGrade.Value, 1, MidpointRounding.AwayFromZero);
                    existingMini.GradedByTeacherId = teacherId;
                    existingMini.GradedAtUtc = now;
                    if (!string.IsNullOrWhiteSpace(dto.TeacherFeedback)) existingMini.Notes = dto.TeacherFeedback.Trim();
                    existingMini.MarkUpdated();
                }
                else
                {
                    _context.CohortGradeEntries.Add(new CohortGradeEntry
                    {
                        CohortId = cohort.Id,
                        StudentId = dto.StudentId,
                        ComponentType = GradeComponentType.MiniProject,
                        Score = Math.Round(dto.MiniProjectGrade.Value, 1, MidpointRounding.AwayFromZero),
                        GradedByTeacherId = teacherId,
                        GradedAtUtc = now,
                        Notes = dto.TeacherFeedback?.Trim()
                    });
                }
            }

            // 1.2 Final Layihə balı
            if (dto.FinalGrade.HasValue)
            {
                if (dto.FinalGrade.Value < 0 || dto.FinalGrade.Value > 100)
                    return Result<bool>.Failure(Error.Validation("Grade.Invalid", "Final balı 0 ilə 100 arasında olmalıdır."));

                var existingFinal = cohort.GradeEntries.FirstOrDefault(g => g.StudentId == dto.StudentId && g.ComponentType == GradeComponentType.Final && !g.IsDeleted);
                if (existingFinal != null)
                {
                    existingFinal.Score = Math.Round(dto.FinalGrade.Value, 1, MidpointRounding.AwayFromZero);
                    existingFinal.GradedByTeacherId = teacherId;
                    existingFinal.GradedAtUtc = now;
                    if (!string.IsNullOrWhiteSpace(dto.TeacherFeedback)) existingFinal.Notes = dto.TeacherFeedback.Trim();
                    existingFinal.MarkUpdated();
                }
                else
                {
                    _context.CohortGradeEntries.Add(new CohortGradeEntry
                    {
                        CohortId = cohort.Id,
                        StudentId = dto.StudentId,
                        ComponentType = GradeComponentType.Final,
                        Score = Math.Round(dto.FinalGrade.Value, 1, MidpointRounding.AwayFromZero),
                        GradedByTeacherId = teacherId,
                        GradedAtUtc = now,
                        Notes = dto.TeacherFeedback?.Trim()
                    });
                }
            }

            // 1.3 Davamiyyət (Qayıb və Gecikmə)
            if (dto.AbsentCount.HasValue || dto.LateCount.HasValue)
            {
                var targetAbsents = Math.Max(0, dto.AbsentCount ?? 0);
                var targetLates = Math.Max(0, dto.LateCount ?? 0);

                var existingRecords = cohort.AttendanceRecords.Where(a => a.StudentId == dto.StudentId && !a.IsDeleted).ToList();
                var currentAbsents = existingRecords.Where(a => a.Status == AttendanceStatus.Absent).ToList();
                var currentLates = existingRecords.Where(a => a.Status == AttendanceStatus.Late).ToList();

                // Qayıbları sinxronlaşdır
                while (currentAbsents.Count < targetAbsents)
                {
                    var newDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-currentAbsents.Count));
                    var rec = new AttendanceRecord
                    {
                        CohortId = cohort.Id,
                        StudentId = dto.StudentId,
                        Date = newDate,
                        Status = AttendanceStatus.Absent,
                        RecordedByUserId = teacherId,
                        RecordedAtUtc = now
                    };
                    _context.AttendanceRecords.Add(rec);
                    currentAbsents.Add(rec);
                }
                while (currentAbsents.Count > targetAbsents && currentAbsents.Count > 0)
                {
                    var last = currentAbsents[^1];
                    _context.AttendanceRecords.Remove(last);
                    currentAbsents.RemoveAt(currentAbsents.Count - 1);
                }

                // Gecikmələri sinxronlaşdır
                while (currentLates.Count < targetLates)
                {
                    var newDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10 - currentLates.Count));
                    var rec = new AttendanceRecord
                    {
                        CohortId = cohort.Id,
                        StudentId = dto.StudentId,
                        Date = newDate,
                        Status = AttendanceStatus.Late,
                        RecordedByUserId = teacherId,
                        RecordedAtUtc = now
                    };
                    _context.AttendanceRecords.Add(rec);
                    currentLates.Add(rec);
                }
                while (currentLates.Count > targetLates && currentLates.Count > 0)
                {
                    var last = currentLates[^1];
                    _context.AttendanceRecords.Remove(last);
                    currentLates.RemoveAt(currentLates.Count - 1);
                }

                var ratio = cohort.LateToAbsenceRatio > 0 ? cohort.LateToAbsenceRatio : 3;
                var effective = targetAbsents + (targetLates / ratio);
                if (effective > cohort.MaxAbsenceLimit)
                {
                    cohortEnrollment.Status = CohortEnrollmentStatus.Dropped;
                }
                else if (cohortEnrollment.Status == CohortEnrollmentStatus.Dropped)
                {
                    cohortEnrollment.Status = CohortEnrollmentStatus.Enrolled;
                }
            }

            // 1.4 Müəllim şərhi
            if (!string.IsNullOrWhiteSpace(dto.TeacherFeedback))
            {
                var note = new CohortNote
                {
                    CohortId = cohort.Id,
                    StudentId = dto.StudentId,
                    AuthorTeacherId = teacherId,
                    Type = CohortNoteType.Feedback,
                    Text = dto.TeacherFeedback.Trim()
                };
                _context.CohortNotes.Add(note);
            }

            await _context.SaveChangesAsync(ct);
            return Result<bool>.Success(true);
        }

        // 2. Əgər kohort deyilsə, klassik Kurs yoxlanılır (Köhnə arxitektura fallback)
        var course = await _context.Courses
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == dto.GroupId && !c.IsDeleted, ct);

        if (course == null)
        {
            return Result<bool>.Failure(Error.NotFound("Group.NotFound", "Qrup tapılmadı."));
        }

        if (!isAdmin && course.InstructorId != teacherId)
        {
            return Result<bool>.Failure(Error.Forbidden("Group.Forbidden", "Yalnız sizə təyin olunmuş qrupun tələbələrinə bal verə bilərsiniz."));
        }

        var enrollment = await _context.Enrollments
            .FirstOrDefaultAsync(e => e.CourseId == dto.GroupId && e.StudentId == dto.StudentId && !e.IsDeleted, ct);

        if (enrollment == null)
        {
            return Result<bool>.Failure(Error.NotFound("Enrollment.NotFound", "Tələbə bu qrupda qeydiyyatda deyil."));
        }

        if (dto.MiniProjectGrade.HasValue)
        {
            if (dto.MiniProjectGrade.Value < 0 || dto.MiniProjectGrade.Value > 100)
                return Result<bool>.Failure(Error.Validation("Grade.Invalid", "Mini layihə balı 0 ilə 100 arasında olmalıdır."));
            enrollment.MiniProjectGrade = dto.MiniProjectGrade.Value;
        }

        if (dto.FinalGrade.HasValue)
        {
            if (dto.FinalGrade.Value < 0 || dto.FinalGrade.Value > 100)
                return Result<bool>.Failure(Error.Validation("Grade.Invalid", "Final balı 0 ilə 100 arasında olmalıdır."));
            enrollment.FinalGrade = dto.FinalGrade.Value;
        }

        if (dto.AbsentCount.HasValue)
        {
            if (dto.AbsentCount.Value < 0)
                return Result<bool>.Failure(Error.Validation("Attendance.Invalid", "Qayıb sayı mənfi ola bilməz."));
            enrollment.AbsentCount = dto.AbsentCount.Value;
        }

        if (dto.LateCount.HasValue)
        {
            if (dto.LateCount.Value < 0)
                return Result<bool>.Failure(Error.Validation("Attendance.Invalid", "Gecikmə sayı mənfi ola bilməz."));
            enrollment.LateCount = dto.LateCount.Value;
        }

        if (!string.IsNullOrWhiteSpace(dto.TeacherFeedback))
        {
            enrollment.TeacherFeedback = dto.TeacherFeedback.Trim();
        }

        enrollment.GradedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);

        return Result<bool>.Success(true);
    }

    public async Task<Result<IReadOnlyCollection<TeacherQuizSummaryDto>>> GetGroupQuizzesAsync(
        Guid groupId,
        Guid teacherId,
        bool isAdmin,
        CancellationToken ct = default)
    {
        var course = await _context.Courses
            .AsNoTracking()
            .Include(c => c.Modules)
                .ThenInclude(m => m.Lessons)
                    .ThenInclude(l => l.Quiz)
                        .ThenInclude(q => q!.Questions)
            .FirstOrDefaultAsync(c => c.Id == groupId && !c.IsDeleted, ct);

        if (course == null)
        {
            // Qrup ID-si kohort da ola bilər (Stage 2/3)
            var cohort = await _context.Cohorts
                .AsNoTracking()
                .Include(c => c.Course)
                    .ThenInclude(crs => crs!.Modules)
                        .ThenInclude(m => m.Lessons)
                            .ThenInclude(l => l.Quiz)
                                .ThenInclude(q => q!.Questions)
                .FirstOrDefaultAsync(c => c.Id == groupId && !c.IsDeleted, ct);

            if (cohort != null && cohort.Course != null)
            {
                if (!isAdmin && cohort.TeacherId != teacherId && cohort.Course.InstructorId != teacherId)
                {
                    return Result<IReadOnlyCollection<TeacherQuizSummaryDto>>.Failure(Error.Forbidden("Group.Forbidden", "Bu qrupun kvizlərinə baxmaq səlahiyyətiniz yoxdur."));
                }
                course = cohort.Course;
            }
        }

        if (course == null)
        {
            return Result<IReadOnlyCollection<TeacherQuizSummaryDto>>.Failure(Error.NotFound("Group.NotFound", "Kurs və ya qrup tapılmadı."));
        }

        // Resource-based yoxlama (Əgər birbaşa kurs ID-si verilibsə)
        if (!isAdmin && course.InstructorId != teacherId)
        {
            var isCohortTeacher = await _context.Cohorts
                .AsNoTracking()
                .AnyAsync(c => c.CourseId == course.Id && c.TeacherId == teacherId && !c.IsDeleted, ct);
            if (!isCohortTeacher)
            {
                return Result<IReadOnlyCollection<TeacherQuizSummaryDto>>.Failure(Error.Forbidden("Group.Forbidden", "Bu kursun kvizlərinə baxmaq səlahiyyətiniz yoxdur."));
            }
        }

        var list = new List<TeacherQuizSummaryDto>();

        foreach (var module in course.Modules.Where(m => !m.IsDeleted))
        {
            foreach (var lesson in module.Lessons.Where(l => !l.IsDeleted))
            {
                if (lesson.Quiz != null && !lesson.Quiz.IsDeleted)
                {
                    list.Add(new TeacherQuizSummaryDto(
                        lesson.Quiz.Id,
                        lesson.Id,
                        lesson.Title,
                        lesson.Quiz.Title,
                        lesson.Quiz.Description,
                        lesson.Quiz.PassingScorePercentage,
                        lesson.Quiz.TimeLimitMinutes,
                        lesson.Quiz.MaxAttempts,
                        lesson.Quiz.Questions.Count(q => !q.IsDeleted)
                    ));
                }
            }
        }

        return Result<IReadOnlyCollection<TeacherQuizSummaryDto>>.Success(list);
    }

    /// <summary>
    /// Rəsmi akademik qiymətləndirmə formulu:
    /// Yekun bal = Quiz ortalaması * 30% + Mini layihə * 30% + Final * 40%
    /// Hələ qiymətlənməyən komponent olduqda IsProvisional = true qaytarılır.
    /// Bütün komponentlər boşdursa bal null qaytarılır və IsProvisional = false.
    /// </summary>
    private static (decimal? Score, bool IsProvisional) CalculateOfficialGrade(Enrollment enrollment, double quizAvg)
    {
        var hasQuiz = quizAvg > 0;
        var hasMini = enrollment.MiniProjectGrade.HasValue;
        var hasFinal = enrollment.FinalGrade.HasValue;

        if (hasQuiz && hasMini && hasFinal)
        {
            var total = ((decimal)quizAvg * 0.30m) + (enrollment.MiniProjectGrade!.Value * 0.30m) + (enrollment.FinalGrade!.Value * 0.40m);
            return (Math.Round(total, 1, MidpointRounding.AwayFromZero), false);
        }

        // Bütün komponentlər boşdursa
        if (!hasQuiz && !hasMini && !hasFinal)
        {
            return (null, false);
        }

        // Müvəqqəti (Provisional) bal
        decimal currentWeightedSum = 0m;
        decimal currentTotalWeight = 0m;

        if (hasQuiz)
        {
            currentWeightedSum += (decimal)quizAvg * 0.30m;
            currentTotalWeight += 0.30m;
        }

        if (hasMini)
        {
            currentWeightedSum += enrollment.MiniProjectGrade!.Value * 0.30m;
            currentTotalWeight += 0.30m;
        }

        if (hasFinal)
        {
            currentWeightedSum += enrollment.FinalGrade!.Value * 0.40m;
            currentTotalWeight += 0.40m;
        }

        if (currentTotalWeight == 0m)
        {
            return (null, false);
        }

        var provisional = currentWeightedSum / currentTotalWeight;
        return (Math.Round(provisional, 1, MidpointRounding.AwayFromZero), true);
    }

    private static (decimal? Score, bool IsProvisional) CalculateCohortStudentGrade(
        double quizAvg,
        decimal? miniScore,
        decimal? finalScore,
        decimal quizWeight,
        decimal miniWeight,
        decimal finalWeight,
        bool isCohortFinished)
    {
        var hasQuiz = quizAvg > 0;
        var hasMini = miniScore.HasValue;
        var hasFinal = finalScore.HasValue;

        if (!hasQuiz && !hasMini && !hasFinal)
        {
            return (null, false);
        }

        if (hasQuiz && hasMini && hasFinal)
        {
            var total = ((decimal)quizAvg * (quizWeight / 100m)) +
                        (miniScore!.Value * (miniWeight / 100m)) +
                        (finalScore!.Value * (finalWeight / 100m));
            return (Math.Round(total, 1, MidpointRounding.AwayFromZero), false);
        }

        decimal currentWeightedSum = 0m;
        decimal currentTotalWeight = 0m;

        if (hasQuiz)
        {
            currentWeightedSum += (decimal)quizAvg * (quizWeight / 100m);
            currentTotalWeight += (quizWeight / 100m);
        }

        if (hasMini)
        {
            currentWeightedSum += miniScore!.Value * (miniWeight / 100m);
            currentTotalWeight += (miniWeight / 100m);
        }

        if (hasFinal)
        {
            currentWeightedSum += finalScore!.Value * (finalWeight / 100m);
            currentTotalWeight += (finalWeight / 100m);
        }

        if (currentTotalWeight == 0m)
        {
            return (null, false);
        }

        var provisional = currentWeightedSum / currentTotalWeight;
        var isProvisional = !isCohortFinished;
        return (Math.Round(provisional, 1, MidpointRounding.AwayFromZero), isProvisional);
    }
}
