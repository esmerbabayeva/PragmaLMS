using Microsoft.EntityFrameworkCore;
using Pragma.Application.Common;
using Pragma.Application.Contracts;
using Pragma.Application.DTOs.Notifications;
using Pragma.Domain.Entities;
using Pragma.Domain.Enums;

namespace Pragma.Application.Services;

public class AcademicGradeService : IAcademicGradeService
{
    private readonly IAppDbContext _context;
    private readonly IUserLookupService _userLookup;
    private readonly INotificationService? _notificationService;

    public AcademicGradeService(
        IAppDbContext context,
        IUserLookupService userLookup,
        INotificationService? notificationService = null)
    {
        _context = context;
        _userLookup = userLookup;
        _notificationService = notificationService;
    }

    public async Task<Result<StudentAcademicGradeSummary>> CalculateStudentGradeAsync(
        Guid cohortId,
        Guid studentId,
        CancellationToken ct = default)
    {
        var cohort = await _context.Cohorts
            .AsNoTracking()
            .Include(c => c.Course)
            .Include(c => c.Enrollments)
            .FirstOrDefaultAsync(c => c.Id == cohortId && !c.IsDeleted, ct);

        if (cohort == null)
        {
            return Result<StudentAcademicGradeSummary>.Failure(Error.NotFound("Cohort.NotFound", "Kohort tapılmadı."));
        }

        var enrollment = cohort.Enrollments.FirstOrDefault(e => e.StudentId == studentId && !e.IsDeleted);
        if (enrollment == null)
        {
            return Result<StudentAcademicGradeSummary>.Failure(Error.NotFound("Enrollment.NotFound", "Tələbə bu kohortda qeydiyyatda deyil."));
        }

        var user = await _userLookup.GetUserByIdAsync(studentId, ct);
        var fullName = user?.FullName ?? "Tələbə";
        var email = user?.Email ?? string.Empty;

        // 1. Kurs üzrə bütün Quiz-lərin tapılması (Yüngül və optimallaşdırılmış sorğu)
        var quizIds = await GetCourseQuizIdsAsync(cohort.CourseId, ct);
        var totalQuizzes = quizIds.Count;

        // 2. Tələbənin quiz cəhdləri
        var attempts = totalQuizzes > 0
            ? await _context.QuizAttempts
                .AsNoTracking()
                .Where(qa => qa.StudentId == studentId && quizIds.Contains(qa.QuizId) && !qa.IsDeleted)
                .ToListAsync(ct)
            : new List<QuizAttempt>();

        // 3. Mini layihə və Final balı (CohortGradeEntry)
        var grades = await _context.CohortGradeEntries
            .AsNoTracking()
            .Where(g => g.CohortId == cohortId && g.StudentId == studentId && !g.IsDeleted)
            .ToListAsync(ct);

        var summary = ComputeStudentSummary(
            studentId, fullName, email, enrollment.Status, totalQuizzes, attempts, grades, cohort);

        return Result<StudentAcademicGradeSummary>.Success(summary);
    }

    private static StudentAcademicGradeSummary ComputeStudentSummary(
        Guid studentId,
        string fullName,
        string email,
        CohortEnrollmentStatus status,
        int totalQuizzes,
        IEnumerable<QuizAttempt> studentAttempts,
        IEnumerable<CohortGradeEntry> studentGrades,
        Cohort cohort)
    {
        decimal? quizAverage = null;
        int quizzesCompleted = 0;

        if (totalQuizzes > 0)
        {
            var bestAttemptsByQuiz = studentAttempts
                .GroupBy(a => a.QuizId)
                .Select(g => g.Max(a => (decimal)a.ScorePercentage))
                .ToList();

            quizzesCompleted = bestAttemptsByQuiz.Count;
            if (quizzesCompleted > 0)
            {
                quizAverage = Math.Round(bestAttemptsByQuiz.Average(), 1, MidpointRounding.AwayFromZero);
            }
        }

        var gradesList = studentGrades as IList<CohortGradeEntry> ?? studentGrades.ToList();
        decimal? miniScore = gradesList.FirstOrDefault(g => g.ComponentType == GradeComponentType.MiniProject)?.Score;
        string? miniNotes = gradesList.FirstOrDefault(g => g.ComponentType == GradeComponentType.MiniProject)?.Notes;
        decimal? finalScore = gradesList.FirstOrDefault(g => g.ComponentType == GradeComponentType.Final)?.Score;
        string? finalNotes = gradesList.FirstOrDefault(g => g.ComponentType == GradeComponentType.Final)?.Notes;

        var (officialScore, isProvisional) = CalculateFinalScore(
            quizAvg: quizAverage,
            miniScore: miniScore,
            finalScore: finalScore,
            quizWeight: cohort.QuizWeight,
            miniWeight: cohort.MiniProjectWeight,
            finalWeight: cohort.FinalWeight,
            isCohortFinished: cohort.Status == CohortStatus.Finished
        );

        return new StudentAcademicGradeSummary(
            StudentId: studentId,
            StudentFullName: fullName,
            StudentEmail: email,
            QuizAverage: quizAverage,
            QuizzesCompleted: quizzesCompleted,
            TotalQuizzes: totalQuizzes,
            MiniProjectScore: miniScore,
            FinalScore: finalScore,
            OfficialScore: officialScore,
            IsProvisional: isProvisional,
            Status: status,
            MiniProjectNotes: miniNotes,
            FinalNotes: finalNotes
        );
    }

    public async Task<Result<IReadOnlyCollection<StudentAcademicGradeSummary>>> GetCohortGradesAsync(
        Guid cohortId,
        Guid requestingUserId,
        bool isAdmin,
        CancellationToken ct = default)
    {
        var cohort = await _context.Cohorts
            .AsNoTracking()
            .Include(c => c.Course)
            .Include(c => c.Enrollments)
            .FirstOrDefaultAsync(c => c.Id == cohortId && !c.IsDeleted, ct);

        if (cohort == null)
        {
            return Result<IReadOnlyCollection<StudentAcademicGradeSummary>>.Failure(Error.NotFound("Cohort.NotFound", "Kohort tapılmadı."));
        }

        // Səlahiyyət yoxlanışı
        if (!isAdmin)
        {
            var isCourseInstructor = cohort.Course != null && cohort.Course.InstructorId == requestingUserId;
            var isAssignedTeacher = cohort.TeacherId == requestingUserId;
            var isEnrolledStudent = cohort.Enrollments.Any(e => e.StudentId == requestingUserId && !e.IsDeleted);

            if (!isCourseInstructor && !isAssignedTeacher && !isEnrolledStudent)
            {
                return Result<IReadOnlyCollection<StudentAcademicGradeSummary>>.Failure(Error.Forbidden("Cohort.Forbidden", "Qiymət cədvəlinə baxmaq səlahiyyətiniz yoxdur."));
            }

            // Tələbə yalnız öz qiymətinə baxa bilər
            if (isEnrolledStudent && !isCourseInstructor && !isAssignedTeacher)
            {
                var singleResult = await CalculateStudentGradeAsync(cohortId, requestingUserId, ct);
                if (singleResult.IsFailure) return Result<IReadOnlyCollection<StudentAcademicGradeSummary>>.Failure(singleResult.Error);
                return Result<IReadOnlyCollection<StudentAcademicGradeSummary>>.Success([singleResult.Value]);
            }
        }

        var enrollments = cohort.Enrollments.Where(e => !e.IsDeleted).ToList();
        if (enrollments.Count == 0)
        {
            return Result<IReadOnlyCollection<StudentAcademicGradeSummary>>.Success(Array.Empty<StudentAcademicGradeSummary>());
        }

        var studentIds = enrollments.Select(e => e.StudentId).Distinct().ToList();

        // 1. Tələbələrin məlumatları toplu çəkilir (Batch User Lookup)
        var usersDict = (await _userLookup.GetUsersByIdsAsync(studentIds, ct)) ?? new Dictionary<Guid, UserBasicInfoDto>();

        // 2. Kurs üzrə bütün Quiz-lərin tapılması (Yüngül və optimallaşdırılmış sorğu)
        var quizIds = await GetCourseQuizIdsAsync(cohort.CourseId, ct);
        var totalQuizzes = quizIds.Count;

        // 3. Bütün tələbələrin quiz cəhdləri tək sorğu ilə çəkilir və yaddaşda qruplaşdırılır
        var allAttempts = totalQuizzes > 0
            ? await _context.QuizAttempts
                .AsNoTracking()
                .Where(qa => studentIds.Contains(qa.StudentId) && quizIds.Contains(qa.QuizId) && !qa.IsDeleted)
                .ToListAsync(ct)
            : new List<QuizAttempt>();

        var attemptsByStudent = allAttempts.ToLookup(a => a.StudentId);

        // 4. Bütün tələbələrin qiymət qeydləri tək sorğu ilə çəkilir və yaddaşda qruplaşdırılır
        var allGradeEntries = await _context.CohortGradeEntries
            .AsNoTracking()
            .Where(g => g.CohortId == cohortId && studentIds.Contains(g.StudentId) && !g.IsDeleted)
            .ToListAsync(ct);

        var gradesByStudent = allGradeEntries.ToLookup(g => g.StudentId);

        // 5. Yaddaşda hər bir tələbə üçün yekun bal hesablanır (0 əlavə SQL sorğusu)
        var list = new List<StudentAcademicGradeSummary>(enrollments.Count);
        foreach (var enr in enrollments)
        {
            usersDict.TryGetValue(enr.StudentId, out var user);
            var fullName = user?.FullName ?? "Tələbə";
            var email = user?.Email ?? string.Empty;

            var studentAttempts = attemptsByStudent[enr.StudentId];
            var studentGrades = gradesByStudent[enr.StudentId];

            var summary = ComputeStudentSummary(
                enr.StudentId, fullName, email, enr.Status, totalQuizzes, studentAttempts, studentGrades, cohort);

            list.Add(summary);
        }

        return Result<IReadOnlyCollection<StudentAcademicGradeSummary>>.Success(list);
    }

    public async Task<Result<bool>> SubmitComponentGradeAsync(
        Guid cohortId,
        Guid studentId,
        GradeComponentType componentType,
        decimal score,
        Guid teacherId,
        bool isAdmin,
        string? notes = null,
        CancellationToken ct = default)
    {
        if (score < 0m || score > 100m)
        {
            return Result<bool>.Failure(Error.Validation("Grade.InvalidScore", "Qiymət balı 0 ilə 100 arasında olmalıdır."));
        }

        if (componentType == GradeComponentType.Quiz)
        {
            return Result<bool>.Failure(Error.Validation("Grade.QuizAutoComputed", "Kviz balı əl ilə daxil edilmir, tələbənin kviz cəhdlərindən avtomatik hesablanır."));
        }

        var cohort = await _context.Cohorts
            .Include(c => c.Course)
            .Include(c => c.Enrollments)
            .FirstOrDefaultAsync(c => c.Id == cohortId && !c.IsDeleted, ct);

        if (cohort == null)
        {
            return Result<bool>.Failure(Error.NotFound("Cohort.NotFound", "Kohort tapılmadı."));
        }

        // Səlahiyyət yoxlanışı: Yalnız Admin, Course Instructor və ya Cohort-un rəsmi təyin olunmuş Teacher-i daxil edə bilər
        if (!isAdmin && cohort.TeacherId != teacherId && cohort.Course?.InstructorId != teacherId)
        {
            return Result<bool>.Failure(Error.Forbidden("Cohort.Forbidden", "Bu kohorta qiymət daxil etmək səlahiyyətiniz yoxdur."));
        }

        var enrollment = cohort.Enrollments.FirstOrDefault(e => e.StudentId == studentId && !e.IsDeleted);
        if (enrollment == null)
        {
            return Result<bool>.Failure(Error.NotFound("Enrollment.NotFound", "Tələbə bu kohortda qeydiyyatda deyil."));
        }

        var existingEntry = await _context.CohortGradeEntries
            .FirstOrDefaultAsync(g => g.CohortId == cohortId && g.StudentId == studentId && g.ComponentType == componentType && !g.IsDeleted, ct);

        if (existingEntry != null)
        {
            existingEntry.Score = Math.Round(score, 1, MidpointRounding.AwayFromZero);
            existingEntry.GradedByTeacherId = teacherId;
            existingEntry.GradedAtUtc = DateTime.UtcNow;
            existingEntry.Notes = notes?.Trim();
        }
        else
        {
            var newEntry = new CohortGradeEntry
            {
                CohortId = cohortId,
                StudentId = studentId,
                ComponentType = componentType,
                Score = Math.Round(score, 1, MidpointRounding.AwayFromZero),
                GradedByTeacherId = teacherId,
                GradedAtUtc = DateTime.UtcNow,
                Notes = notes?.Trim()
            };
            _context.CohortGradeEntries.Add(newEntry);
        }

        await _context.SaveChangesAsync(ct);

        // Tələbənin ümumi rəsmi balını yeniləyirik
        var calcResult = await CalculateStudentGradeAsync(cohortId, studentId, ct);
        if (calcResult.IsSuccess)
        {
            enrollment.OfficialFinalScore = calcResult.Value.OfficialScore;
            enrollment.IsProvisional = calcResult.Value.IsProvisional;
            await _context.SaveChangesAsync(ct);
        }

        if (_notificationService != null)
        {
            await _notificationService.CreateNotificationAsync(new CreateNotificationDto(
                UserId: studentId,
                Title: "Yeni Qiymət Daxil Edildi",
                Message: $"{cohort.Name} qrupunda {componentType} komponenti üzrə {score} bal qeyd edildi.",
                Type: NotificationType.GradePosted,
                TargetUrl: "/portal/grades.html"
            ), ct);
        }

        return Result<bool>.Success(true);
    }

    public async Task<Result<bool>> SubmitBulkGradesAsync(
        Guid cohortId,
        BulkSubmitGradesDto dto,
        Guid teacherId,
        bool isAdmin,
        CancellationToken ct = default)
    {
        var cohort = await _context.Cohorts
            .Include(c => c.Course)
            .Include(c => c.Enrollments)
            .FirstOrDefaultAsync(c => c.Id == cohortId && !c.IsDeleted, ct);

        if (cohort == null)
        {
            return Result<bool>.Failure(Error.NotFound("Cohort.NotFound", "Kohort tapılmadı."));
        }

        if (!isAdmin && cohort.TeacherId != teacherId && cohort.Course?.InstructorId != teacherId)
        {
            return Result<bool>.Failure(Error.Forbidden("Cohort.Forbidden", "Bu kohorta qiymət daxil etmək səlahiyyətiniz yoxdur."));
        }

        var activeEnrollmentStudentIds = cohort.Enrollments
            .Where(e => !e.IsDeleted)
            .Select(e => e.StudentId)
            .ToHashSet();

        var existingGradeEntries = await _context.CohortGradeEntries
            .Where(g => g.CohortId == cohortId && !g.IsDeleted)
            .ToListAsync(ct);

        var utcNow = DateTime.UtcNow;

        foreach (var item in dto.Grades)
        {
            if (!activeEnrollmentStudentIds.Contains(item.StudentId))
            {
                continue;
            }

            // 1. Mini Project
            var existingMini = existingGradeEntries.FirstOrDefault(g => g.StudentId == item.StudentId && g.ComponentType == GradeComponentType.MiniProject);
            if (item.MiniProjectScore.HasValue)
            {
                var score = item.MiniProjectScore.Value;
                if (score < 0m || score > 100m)
                {
                    return Result<bool>.Failure(Error.Validation("Grade.InvalidScore", $"Mini layihə balı 0 ilə 100 arasında olmalıdır ({score})."));
                }
                var rounded = Math.Round(score, 1, MidpointRounding.AwayFromZero);
                if (existingMini != null)
                {
                    existingMini.Score = rounded;
                    existingMini.GradedByTeacherId = teacherId;
                    existingMini.GradedAtUtc = utcNow;
                    if (item.MiniProjectNotes != null) existingMini.Notes = item.MiniProjectNotes.Trim();
                }
                else
                {
                    var newEntry = new CohortGradeEntry
                    {
                        CohortId = cohortId,
                        StudentId = item.StudentId,
                        ComponentType = GradeComponentType.MiniProject,
                        Score = rounded,
                        GradedByTeacherId = teacherId,
                        GradedAtUtc = utcNow,
                        Notes = item.MiniProjectNotes?.Trim()
                    };
                    _context.CohortGradeEntries.Add(newEntry);
                    existingGradeEntries.Add(newEntry);
                }
            }
            else if (existingMini != null)
            {
                if (item.MiniProjectNotes != null)
                {
                    existingMini.Notes = item.MiniProjectNotes.Trim();
                    existingMini.GradedAtUtc = utcNow;
                }
                else
                {
                    existingMini.MarkDeleted();
                }
            }

            // 2. Final
            var existingFinal = existingGradeEntries.FirstOrDefault(g => g.StudentId == item.StudentId && g.ComponentType == GradeComponentType.Final);
            if (item.FinalScore.HasValue)
            {
                var score = item.FinalScore.Value;
                if (score < 0m || score > 100m)
                {
                    return Result<bool>.Failure(Error.Validation("Grade.InvalidScore", $"Final balı 0 ilə 100 arasında olmalıdır ({score})."));
                }
                var rounded = Math.Round(score, 1, MidpointRounding.AwayFromZero);
                if (existingFinal != null)
                {
                    existingFinal.Score = rounded;
                    existingFinal.GradedByTeacherId = teacherId;
                    existingFinal.GradedAtUtc = utcNow;
                    if (item.FinalNotes != null) existingFinal.Notes = item.FinalNotes.Trim();
                }
                else
                {
                    var newEntry = new CohortGradeEntry
                    {
                        CohortId = cohortId,
                        StudentId = item.StudentId,
                        ComponentType = GradeComponentType.Final,
                        Score = rounded,
                        GradedByTeacherId = teacherId,
                        GradedAtUtc = utcNow,
                        Notes = item.FinalNotes?.Trim()
                    };
                    _context.CohortGradeEntries.Add(newEntry);
                    existingGradeEntries.Add(newEntry);
                }
            }
            else if (existingFinal != null)
            {
                if (item.FinalNotes != null)
                {
                    existingFinal.Notes = item.FinalNotes.Trim();
                    existingFinal.GradedAtUtc = utcNow;
                }
                else
                {
                    existingFinal.MarkDeleted();
                }
            }
        }

        // BATCH HESABLAMA: N+1 sorğularını aradan qaldıraraq tələbələrin rəsmi ballarını tək addımda yeniləmək
        var studentIdsToUpdate = dto.Grades
            .Select(g => g.StudentId)
            .Where(activeEnrollmentStudentIds.Contains)
            .Distinct()
            .ToList();

        if (studentIdsToUpdate.Count > 0)
        {
            var quizIds = await GetCourseQuizIdsAsync(cohort.CourseId, ct);
            var totalQuizzes = quizIds.Count;

            var attempts = totalQuizzes > 0
                ? await _context.QuizAttempts
                    .AsNoTracking()
                    .Where(qa => studentIdsToUpdate.Contains(qa.StudentId) && quizIds.Contains(qa.QuizId) && !qa.IsDeleted)
                    .ToListAsync(ct)
                : new List<QuizAttempt>();

            var attemptsByStudent = attempts.ToLookup(a => a.StudentId);
            var activeGradesByStudent = existingGradeEntries
                .Where(g => !g.IsDeleted && studentIdsToUpdate.Contains(g.StudentId))
                .ToLookup(g => g.StudentId);

            foreach (var studentId in studentIdsToUpdate)
            {
                var enrollment = cohort.Enrollments.FirstOrDefault(e => e.StudentId == studentId && !e.IsDeleted);
                if (enrollment != null)
                {
                    decimal? quizAverage = null;
                    if (totalQuizzes > 0)
                    {
                        var studentAttempts = attemptsByStudent[studentId];
                        var bestAttemptsByQuiz = studentAttempts
                            .GroupBy(a => a.QuizId)
                            .Select(g => g.Max(a => (decimal)a.ScorePercentage))
                            .ToList();

                        if (bestAttemptsByQuiz.Count > 0)
                        {
                            quizAverage = Math.Round(bestAttemptsByQuiz.Average(), 1, MidpointRounding.AwayFromZero);
                        }
                    }

                    var studentGrades = activeGradesByStudent[studentId];
                    decimal? miniScore = studentGrades.FirstOrDefault(g => g.ComponentType == GradeComponentType.MiniProject)?.Score;
                    decimal? finalScore = studentGrades.FirstOrDefault(g => g.ComponentType == GradeComponentType.Final)?.Score;

                    var (officialScore, isProvisional) = CalculateFinalScore(
                        quizAvg: quizAverage,
                        miniScore: miniScore,
                        finalScore: finalScore,
                        quizWeight: cohort.QuizWeight,
                        miniWeight: cohort.MiniProjectWeight,
                        finalWeight: cohort.FinalWeight,
                        isCohortFinished: cohort.Status == CohortStatus.Finished
                    );

                    enrollment.OfficialFinalScore = officialScore;
                    enrollment.IsProvisional = isProvisional;
                }
            }
        }

        await _context.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }

    public async Task<Result<bool>> FinalizeCohortGradesAsync(
        Guid cohortId,
        Guid requestingUserId,
        bool isAdmin,
        CancellationToken ct = default)
    {
        var cohort = await _context.Cohorts
            .Include(c => c.Course)
            .Include(c => c.Enrollments)
            .FirstOrDefaultAsync(c => c.Id == cohortId && !c.IsDeleted, ct);

        if (cohort == null)
        {
            return Result<bool>.Failure(Error.NotFound("Cohort.NotFound", "Kohort tapılmadı."));
        }

        if (!isAdmin && cohort.Course?.InstructorId != requestingUserId)
        {
            return Result<bool>.Failure(Error.Forbidden("Cohort.Forbidden", "Kohort ballarını rəsmiləşdirmək səlahiyyətiniz yoxdur."));
        }

        var deadlineUtc = FinalDeadlineHelper.GetFinalDeadlineUtc(cohort);
        if (!FinalDeadlineHelper.IsFinalDeadlinePassed(cohort, DateTime.UtcNow))
        {
            if (!deadlineUtc.HasValue)
            {
                return Result<bool>.Failure(Error.Conflict("Cohort.DeadlineNotConfigured", "Təhvil müddəti təyin olunmayıb."));
            }

            return Result<bool>.Failure(Error.Conflict("Cohort.DeadlineNotPassed", "Təhvil müddəti bitməyib."));
        }

        var enrollments = cohort.Enrollments.Where(e => !e.IsDeleted).ToList();
        var studentIds = enrollments.Select(e => e.StudentId).Distinct().ToList();

        if (studentIds.Count > 0)
        {
            var quizIds = await GetCourseQuizIdsAsync(cohort.CourseId, ct);
            var totalQuizzes = quizIds.Count;

            var attempts = totalQuizzes > 0
                ? await _context.QuizAttempts
                    .AsNoTracking()
                    .Where(qa => studentIds.Contains(qa.StudentId) && quizIds.Contains(qa.QuizId) && !qa.IsDeleted)
                    .ToListAsync(ct)
                : new List<QuizAttempt>();

            var attemptsByStudent = attempts.ToLookup(a => a.StudentId);
            var gradeEntries = await _context.CohortGradeEntries
                .AsNoTracking()
                .Where(g => g.CohortId == cohortId && studentIds.Contains(g.StudentId) && !g.IsDeleted)
                .ToListAsync(ct);
            var gradesByStudent = gradeEntries.ToLookup(g => g.StudentId);

            var submittedStudentIds = await _context.FinalSubmissions
                .AsNoTracking()
                .Where(s => s.CohortId == cohortId && !s.IsDeleted && !string.IsNullOrWhiteSpace(s.Url))
                .Select(s => s.StudentId)
                .ToListAsync(ct);

            var ungradedSubmittedStudents = enrollments
                .Where(e => submittedStudentIds.Contains(e.StudentId))
                .Where(e => !gradesByStudent[e.StudentId].Any(g => g.ComponentType == GradeComponentType.Final && !g.IsDeleted))
                .Select(e => e.StudentId)
                .ToList();

            if (ungradedSubmittedStudents.Count > 0)
            {
                var idsStr = string.Join(", ", ungradedSubmittedStudents);
                return Result<bool>.Failure(Error.Conflict(
                    "Cohort.UngradedFinalSubmissions",
                    $"Təhvil verilmiş, qiymətləndirilməmiş {ungradedSubmittedStudents.Count} final layihə var: [{idsStr}]"));
            }

            foreach (var enr in enrollments)
            {
                decimal? quizAverage = null;
                if (totalQuizzes > 0)
                {
                    var studentAttempts = attemptsByStudent[enr.StudentId];
                    var bestAttemptsByQuiz = studentAttempts
                        .GroupBy(a => a.QuizId)
                        .Select(g => g.Max(a => (decimal)a.ScorePercentage))
                        .ToList();

                    if (bestAttemptsByQuiz.Count > 0)
                    {
                        quizAverage = Math.Round(bestAttemptsByQuiz.Average(), 1, MidpointRounding.AwayFromZero);
                    }
                }

                var studentGrades = gradesByStudent[enr.StudentId];
                decimal? miniScore = studentGrades.FirstOrDefault(g => g.ComponentType == GradeComponentType.MiniProject)?.Score;
                var finalGradeEntry = studentGrades.FirstOrDefault(g => g.ComponentType == GradeComponentType.Final);
                decimal? finalScore = finalGradeEntry?.Score;

                if (finalGradeEntry == null)
                {
                    // Təhvil linki də, qiymət də olmayan tələbə
                    enr.FinalNotSubmitted = true;
                    finalScore = 0m;
                }
                else
                {
                    // Final qiymətləndirilib (müəllim açıq 0 yazsa belə təhvil verilmiş sayılır)
                    enr.FinalNotSubmitted = false;
                }

                var (officialScore, isProvisional) = CalculateFinalScore(
                    quizAvg: quizAverage,
                    miniScore: miniScore,
                    finalScore: finalScore,
                    quizWeight: cohort.QuizWeight,
                    miniWeight: cohort.MiniProjectWeight,
                    finalWeight: cohort.FinalWeight,
                    isCohortFinished: true
                );

                enr.OfficialFinalScore = officialScore;
                enr.IsProvisional = isProvisional;
            }

            await _context.SaveChangesAsync(ct);
        }

        return Result<bool>.Success(true);
    }

    /// <summary>
    /// Qiymətləndirmə düsturu:
    /// Yekun bal = Quiz ortalaması * QuizWeight% + Mini layihə * MiniWeight% + Final * FinalWeight%.
    /// Qiymətləndirilməmiş komponent varsa çəkilər mütənasib bölünür və IsProvisional=true olur.
    /// Yuvarlaqlaşdırma: 1 onluq, MidpointRounding.AwayFromZero.
    /// </summary>
    public static (decimal Score, bool IsProvisional) CalculateFinalScore(
        decimal? quizAvg,
        decimal? miniScore,
        decimal? finalScore,
        decimal quizWeight,
        decimal miniWeight,
        decimal finalWeight,
        bool isCohortFinished)
    {
        var hasQuiz = quizAvg.HasValue;
        var hasMini = miniScore.HasValue;
        var hasFinal = finalScore.HasValue;

        // Bütün 3 komponent də mövcuddursa
        if (hasQuiz && hasMini && hasFinal)
        {
            var total = (quizAvg!.Value * (quizWeight / 100m)) +
                        (miniScore!.Value * (miniWeight / 100m)) +
                        (finalScore!.Value * (finalWeight / 100m));

            var rounded = Math.Round(total, 1, MidpointRounding.AwayFromZero);
            // Dəmir Qayda (AGENTS.md 3.1): Cohort Finished olduqda və ya Final qiymətləndikdə bal rəsmi olur (IsProvisional=false)
            return (rounded, !isCohortFinished && !hasFinal);
        }

        // Çatışmayan komponent varsa: mövcud komponentlər arasında mütənasib bölünmə
        decimal weightedSum = 0m;
        decimal totalPresentWeight = 0m;

        if (hasQuiz)
        {
            weightedSum += quizAvg!.Value * (quizWeight / 100m);
            totalPresentWeight += quizWeight;
        }

        if (hasMini)
        {
            weightedSum += miniScore!.Value * (miniWeight / 100m);
            totalPresentWeight += miniWeight;
        }

        if (hasFinal)
        {
            weightedSum += finalScore!.Value * (finalWeight / 100m);
            totalPresentWeight += finalWeight;
        }

        if (totalPresentWeight == 0m)
        {
            return (0.0m, !isCohortFinished);
        }

        var proportionalScore = (weightedSum / (totalPresentWeight / 100m));
        var resultScore = Math.Round(proportionalScore, 1, MidpointRounding.AwayFromZero);

        return (resultScore, !isCohortFinished);
    }

    private async Task<List<Guid>> GetCourseQuizIdsAsync(Guid courseId, CancellationToken ct)
    {
        return await _context.Modules
            .AsNoTracking()
            .Where(m => m.CourseId == courseId && !m.IsDeleted)
            .SelectMany(m => m.Lessons.Where(l => !l.IsDeleted && l.Quiz != null && !l.Quiz.IsDeleted))
            .Select(l => l.Quiz!.Id)
            .ToListAsync(ct);
    }
}
