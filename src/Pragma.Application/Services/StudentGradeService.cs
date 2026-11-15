using Microsoft.EntityFrameworkCore;
using Pragma.Application.Common;
using Pragma.Application.Contracts;
using Pragma.Application.DTOs.Cohorts;
using Pragma.Application.DTOs.Student;
using Pragma.Domain.Enums;

namespace Pragma.Application.Services;

public class StudentGradeService : IStudentGradeService
{
    private readonly IAppDbContext _context;
    private readonly IUserLookupService _userLookup;
    private readonly ICertificateEligibilityPolicy _certificatePolicy;

    public StudentGradeService(
        IAppDbContext context,
        IUserLookupService userLookup,
        ICertificateEligibilityPolicy certificatePolicy)
    {
        _context = context;
        _userLookup = userLookup;
        _certificatePolicy = certificatePolicy;
    }

    public async Task<Result<IReadOnlyCollection<StudentEnrolledCohortDto>>> GetMyEnrolledCohortsAsync(
        Guid currentStudentId,
        CancellationToken ct = default)
    {
        var enrollments = await _context.CohortEnrollments
            .AsNoTracking()
            .Include(e => e.Cohort)
                .ThenInclude(c => c!.Course)
            .Where(e => e.StudentId == currentStudentId && !e.IsDeleted)
            .OrderByDescending(e => e.EnrolledAtUtc)
            .ToListAsync(ct);

        var result = enrollments.Select(e => new StudentEnrolledCohortDto(
            e.CohortId,
            e.Cohort?.Name ?? "Qrup",
            e.Cohort?.CourseId ?? Guid.Empty,
            e.Cohort?.Course?.Title ?? string.Empty,
            e.Cohort?.Status ?? CohortStatus.Upcoming,
            e.EnrolledAtUtc
        )).ToList();

        return Result<IReadOnlyCollection<StudentEnrolledCohortDto>>.Success(result);
    }

    public async Task<Result<StudentCohortGradeDetailsDto>> GetMyCohortGradeDetailsAsync(
        Guid cohortId,
        Guid currentStudentId,
        CancellationToken ct = default)
    {
        var cohort = await _context.Cohorts
            .AsNoTracking()
            .Include(c => c.Course)
                .ThenInclude(crs => crs!.Modules)
                    .ThenInclude(m => m.Lessons)
                        .ThenInclude(l => l.Quiz)
            .Include(c => c.Enrollments)
            .Include(c => c.AttendanceRecords)
            .Include(c => c.GradeEntries)
            .FirstOrDefaultAsync(c => c.Id == cohortId && !c.IsDeleted, ct);

        if (cohort == null)
        {
            return Result<StudentCohortGradeDetailsDto>.Failure(Error.NotFound("StudentGrade.CohortNotFound", "Kohort tapılmadı."));
        }

        var enrollment = cohort.Enrollments.FirstOrDefault(e => e.StudentId == currentStudentId && !e.IsDeleted);
        if (enrollment == null)
        {
            // Tələbə başqa tələbənin və ya qeydiyyatda olmadığı kohortun məlumatına baxa bilməz -> 404
            return Result<StudentCohortGradeDetailsDto>.Failure(Error.NotFound("StudentGrade.CohortNotFound", "Bu kohorta qeydiyyatınız tapılmadı."));
        }

        // 1. Kvizlər və Kviz Ortalaması
        var lessonIds = cohort.Course?.Modules.Where(m => !m.IsDeleted).SelectMany(m => m.Lessons.Where(l => !l.IsDeleted)).Select(l => l.Id).ToList() ?? new();
        var quizzes = await _context.Quizzes
            .AsNoTracking()
            .Where(q => lessonIds.Contains(q.LessonId) && !q.IsDeleted)
            .Select(q => q.Id)
            .ToListAsync(ct);

        var attempts = await _context.QuizAttempts
            .AsNoTracking()
            .Where(qa => quizzes.Contains(qa.QuizId) && qa.StudentId == currentStudentId && !qa.IsDeleted)
            .ToListAsync(ct);

        decimal? quizAvg = null;
        int quizzesCompleted = 0;
        if (attempts.Any() && quizzes.Any())
        {
            var bestScores = quizzes
                .Select(qId => attempts.Where(a => a.QuizId == qId).Select(a => a.ScorePercentage).DefaultIfEmpty(-1).Max())
                .Where(score => score >= 0)
                .ToList();

            quizzesCompleted = bestScores.Count;
            if (bestScores.Any())
            {
                quizAvg = (decimal)Math.Round(bestScores.Average(), 1);
            }
        }

        // 2. Mini Layihə və Final Balları
        var studentGrades = cohort.GradeEntries.Where(g => g.StudentId == currentStudentId && !g.IsDeleted).ToList();
        var miniEntry = studentGrades.FirstOrDefault(g => g.ComponentType == GradeComponentType.MiniProject);
        var finalEntry = studentGrades.FirstOrDefault(g => g.ComponentType == GradeComponentType.Final);
        decimal? miniScore = miniEntry?.Score;
        decimal? finalScore = finalEntry?.Score;

        // 3. Yekun Bal və Müvəqqəti (Provisional) Hesablama
        var (officialScore, isProvisional) = CalculateCohortStudentGrade(
            (double)(quizAvg ?? 0m), miniScore, finalScore,
            cohort.QuizWeight, cohort.MiniProjectWeight, cohort.FinalWeight,
            cohort.Status == CohortStatus.Finished);

        var gradesDto = new StudentGradeComponentDto(
            quizAvg,
            quizzesCompleted,
            quizzes.Count,
            miniScore,
            finalScore,
            officialScore,
            isProvisional,
            cohort.QuizWeight,
            cohort.MiniProjectWeight,
            cohort.FinalWeight
        );

        // 4. Davamiyyət Hesablaması
        var studentAttendance = cohort.AttendanceRecords
            .Where(a => a.StudentId == currentStudentId && !a.IsDeleted)
            .OrderByDescending(a => a.Date)
            .ToList();

        var presentCount = studentAttendance.Count(a => a.Status == AttendanceStatus.Present);
        var absentCount = studentAttendance.Count(a => a.Status == AttendanceStatus.Absent);
        var lateCount = studentAttendance.Count(a => a.Status == AttendanceStatus.Late);
        var excusedCount = studentAttendance.Count(a => a.Status == AttendanceStatus.Excused);
        var ratio = cohort.LateToAbsenceRatio > 0 ? cohort.LateToAbsenceRatio : 3;
        var effectiveAbsences = absentCount + (lateCount / ratio);
        var hasWarning = effectiveAbsences >= (cohort.MaxAbsenceLimit - 2);
        var isDropped = effectiveAbsences > cohort.MaxAbsenceLimit || enrollment.Status == CohortEnrollmentStatus.Dropped;

        var historyDto = studentAttendance.Select(a => new StudentAttendanceRecordDto(
            a.Date,
            a.Date.DayOfWeek,
            a.Status,
            a.Note
        )).ToList();

        var attendanceDto = new StudentAttendanceOverviewDto(
            absentCount,
            lateCount,
            excusedCount,
            presentCount,
            effectiveAbsences,
            cohort.MaxAbsenceLimit,
            ratio,
            hasWarning,
            isDropped,
            historyDto
        );

        // 5. Müəllimin Şərhləri (YALNIZ Feedback / Tələbəyə şərh tipli — Private qeydlər DTO səviyyəsində qətiyyən daxil edilmir!)
        var feedbackNotes = await _context.CohortNotes
            .AsNoTracking()
            .Where(n => n.CohortId == cohortId && n.StudentId == currentStudentId && n.Type == CohortNoteType.Feedback && !n.IsDeleted)
            .OrderByDescending(n => n.CreatedAtUtc)
            .ToListAsync(ct);

        var authorIds = feedbackNotes.Select(n => n.AuthorTeacherId).Distinct().ToList();
        var authorDict = (await _userLookup.GetUsersByIdsAsync(authorIds, ct)) ?? new Dictionary<Guid, UserBasicInfoDto>();

        var feedbackNotesDto = new List<StudentFeedbackNoteDto>();
        foreach (var n in feedbackNotes)
        {
            if (!authorDict.TryGetValue(n.AuthorTeacherId, out var author) || author == null)
            {
                author = await _userLookup.GetUserByIdAsync(n.AuthorTeacherId, ct);
            }

            feedbackNotesDto.Add(new StudentFeedbackNoteDto(
                n.Id,
                author?.FullName ?? "Müəllim",
                n.Text,
                n.GradeComponent,
                n.CreatedAtUtc
            ));
        }

        // 6. Sertifikat Hüququ (ICertificateEligibilityPolicy)
        var eligibilityEvaluation = _certificatePolicy.Evaluate(
            enrollment,
            officialScore ?? 0m,
            effectiveAbsences,
            cohort.MaxAbsenceLimit,
            cohort.Status == CohortStatus.Finished
        );

        var existingCert = await _context.Certificates
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.StudentId == currentStudentId && c.CourseId == cohort.CourseId && !c.IsDeleted && c.IsRevoked == false, ct);

        var certificateDto = new StudentCertificateStatusDto(
            eligibilityEvaluation.Result,
            eligibilityEvaluation.Reason,
            existingCert?.Id,
            existingCert?.CertificateCode,
            existingCert != null ? $"/api/courses/{cohort.CourseId}/certificate/download" : null
        );

        var deadlineUtc = FinalDeadlineHelper.GetFinalDeadlineUtc(cohort);
        var deadlineLocal = FinalDeadlineHelper.ToBakuLocalString(deadlineUtc);
        var isEod = cohort.FinalDeadlineUtc.HasValue ? FinalDeadlineHelper.IsEndOfDay(cohort.FinalDeadlineUtc.Value) : true;
        var isDeadlinePassed = FinalDeadlineHelper.IsFinalDeadlinePassed(cohort, DateTime.UtcNow);

        var sub = await _context.FinalSubmissions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.CohortId == cohort.Id && s.StudentId == currentStudentId && !s.IsDeleted, ct);

        bool isGraded = finalScore.HasValue;
        string subStatus = isGraded ? "Graded" : (sub != null ? "Submitted" : (isDeadlinePassed ? "Overdue" : "NotSubmitted"));

        var submissionDto = new StudentFinalSubmissionDto(
            Url: sub?.Url,
            SubmittedAtUtc: sub?.SubmittedAtUtc,
            UpdatedAtUtc: sub?.UpdatedAtUtc,
            FinalDeadlineLocal: deadlineLocal,
            IsDeadlinePassed: isDeadlinePassed,
            IsGraded: isGraded,
            FinalGrade: finalScore,
            Status: subStatus
        );

        var resultDetails = new StudentCohortGradeDetailsDto(
            cohort.Id,
            cohort.Name,
            cohort.CourseId,
            cohort.Course?.Title ?? string.Empty,
            cohort.Status,
            gradesDto,
            attendanceDto,
            feedbackNotesDto,
            certificateDto,
            deadlineUtc,
            deadlineLocal,
            isEod,
            isDeadlinePassed,
            submissionDto
        );

        return Result<StudentCohortGradeDetailsDto>.Success(resultDetails);
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

        // Müvəqqəti (Provisional) bal: hələ qiymətləndirilməmiş komponentin çəkisi qalan komponentlərə mütənasib bölünür
        decimal currentTotalWeight = 0m;
        decimal currentWeightedSum = 0m;

        if (hasQuiz)
        {
            currentTotalWeight += quizWeight;
            currentWeightedSum += (decimal)quizAvg * quizWeight;
        }

        if (hasMini)
        {
            currentTotalWeight += miniWeight;
            currentWeightedSum += miniScore!.Value * miniWeight;
        }

        if (hasFinal)
        {
            currentTotalWeight += finalWeight;
            currentWeightedSum += finalScore!.Value * finalWeight;
        }

        if (currentTotalWeight == 0m)
        {
            return (null, false);
        }

        var provisional = currentWeightedSum / currentTotalWeight;
        return (Math.Round(provisional, 1, MidpointRounding.AwayFromZero), true);
    }
}
