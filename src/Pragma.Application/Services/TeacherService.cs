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
                var grades = activeEnrollments.Select(e => CalculateOfficialGrade(e, 0.0).Score).ToList();
                avgGrade = Math.Round((double)grades.Average(), 1);
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
                avgGrade
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

        var studentIds = enrollments.Select(e => e.StudentId).Distinct().ToList();
        var userDict = await _userLookup.GetUsersByIdsAsync(studentIds, ct);

        // Kviz nəticələrini topla (ən yaxşı cəhd qaydası ilə)
        var lessonIds = course.Modules.SelectMany(m => m.Lessons).Select(l => l.Id).ToList();
        var quizzes = await _context.Quizzes
            .AsNoTracking()
            .Where(q => lessonIds.Contains(q.LessonId) && !q.IsDeleted)
            .Select(q => q.Id)
            .ToListAsync(ct);

        var attempts = await _context.QuizAttempts
            .AsNoTracking()
            .Where(qa => quizzes.Contains(qa.QuizId) && studentIds.Contains(qa.StudentId) && !qa.IsDeleted)
            .ToListAsync(ct);

        var resultList = new List<GroupStudentDto>();

        foreach (var enrollment in enrollments)
        {
            userDict.TryGetValue(enrollment.StudentId, out var user);
            var fullName = user?.FullName ?? "Tələbə";
            var email = user?.Email ?? string.Empty;

            // Kviz ortalaması (hər kviz üzrə ən yüksək balın ortalaması)
            var studentAttempts = attempts.Where(a => a.StudentId == enrollment.StudentId).ToList();
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

            var (finalScore, isProvisional) = CalculateOfficialGrade(enrollment, quizAvg);
            var totalAbsences = enrollment.AbsentCount + (enrollment.LateCount / 3);
            var status = totalAbsences > 6 ? "Dropped" : (enrollment.IsCompleted ? "Tamamlayıb" : "Aktiv");

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
        var course = await _context.Courses
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == dto.GroupId && !c.IsDeleted, ct);

        if (course == null)
        {
            return Result<bool>.Failure(Error.NotFound("Group.NotFound", "Qrup tapılmadı."));
        }

        // Resource-based yoxlama: Müəllimin öz qrupudurmu?
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

        // Balların və davamiyyətin daxil edilməsi
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
            return Result<IReadOnlyCollection<TeacherQuizSummaryDto>>.Failure(Error.NotFound("Group.NotFound", "Kurs və ya qrup tapılmadı."));
        }

        // Resource-based yoxlama
        if (!isAdmin && course.InstructorId != teacherId)
        {
            return Result<IReadOnlyCollection<TeacherQuizSummaryDto>>.Failure(Error.Forbidden("Group.Forbidden", "Bu kursun kvizlərinə baxmaq səlahiyyətiniz yoxdur."));
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
    /// </summary>
    private static (decimal Score, bool IsProvisional) CalculateOfficialGrade(Enrollment enrollment, double quizAvg)
    {
        var hasQuiz = quizAvg > 0;
        var hasMini = enrollment.MiniProjectGrade.HasValue;
        var hasFinal = enrollment.FinalGrade.HasValue;

        if (hasQuiz && hasMini && hasFinal)
        {
            var total = ((decimal)quizAvg * 0.30m) + (enrollment.MiniProjectGrade!.Value * 0.30m) + (enrollment.FinalGrade!.Value * 0.40m);
            return (Math.Round(total, 1, MidpointRounding.AwayFromZero), false);
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
            return (0.0m, true);
        }

        var provisional = currentWeightedSum / currentTotalWeight;
        return (Math.Round(provisional, 1, MidpointRounding.AwayFromZero), true);
    }
}
