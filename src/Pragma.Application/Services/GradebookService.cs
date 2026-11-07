using Microsoft.EntityFrameworkCore;
using Pragma.Application.Common;
using Pragma.Application.Contracts;
using Pragma.Application.DTOs.Gradebook;
using Pragma.Domain.Enums;

namespace Pragma.Application.Services;

public class GradebookService : IGradebookService
{
    private readonly IAppDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserLookupService _userLookup;

    public GradebookService(
        IAppDbContext context,
        ICurrentUserService currentUserService,
        IUserLookupService userLookup)
    {
        _context = context;
        _currentUserService = currentUserService;
        _userLookup = userLookup;
    }

    public async Task<Result<CourseGradebookDto>> GetCourseGradebookAsync(Guid courseId, CancellationToken ct = default)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue)
            return Result<CourseGradebookDto>.Failure(Error.Unauthorized("İstifadəçi autentifikasiyadan keçməyib."));

        var course = await _context.Courses
            .AsNoTracking()
            .Include(c => c.Modules.Where(m => !m.IsDeleted))
                .ThenInclude(m => m.Lessons.Where(l => !l.IsDeleted))
            .FirstOrDefaultAsync(c => c.Id == courseId && !c.IsDeleted, ct);

        if (course == null)
            return Result<CourseGradebookDto>.Failure(Error.NotFound("Kurs tapılmadı."));

        var isAdmin = _currentUserService.IsInRole(UserRoles.Admin);
        var isInstructorOwner = _currentUserService.IsInRole(UserRoles.Instructor) && course.InstructorId == currentUserId.Value;

        if (!isAdmin && !isInstructorOwner)
            return Result<CourseGradebookDto>.Failure(Error.Forbidden("Bu kursun qiymət cədvəlinə baxmaq səlahiyyətiniz yoxdur."));

        var enrollments = await _context.Enrollments
            .AsNoTracking()
            .Where(e => e.CourseId == courseId && !e.IsDeleted)
            .OrderByDescending(e => e.EnrolledAtUtc)
            .ToListAsync(ct);

        var studentIds = enrollments.Select(e => e.StudentId).Distinct().ToList();
        var userDict = await _userLookup.GetUsersByIdsAsync(studentIds, ct);

        var courseLessonIds = course.Modules.SelectMany(m => m.Lessons).Select(l => l.Id).ToList();
        var quizzes = await _context.Quizzes
            .AsNoTracking()
            .Where(q => courseLessonIds.Contains(q.LessonId) && !q.IsDeleted)
            .ToListAsync(ct);

        var quizIds = quizzes.Select(q => q.Id).ToList();
        var attempts = await _context.QuizAttempts
            .AsNoTracking()
            .Where(a => quizIds.Contains(a.QuizId) && !a.IsDeleted)
            .ToListAsync(ct);

        var attemptsByStudent = attempts
            .GroupBy(a => a.StudentId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var studentSummaries = new List<StudentGradeSummaryDto>();
        foreach (var enr in enrollments)
        {
            var user = userDict.TryGetValue(enr.StudentId, out var u) ? u : null;
            var fullName = user?.FullName ?? "Məlumat yoxdur";
            var email = user?.Email ?? "Məlumat yoxdur";

            var studentAttempts = attemptsByStudent.TryGetValue(enr.StudentId, out var atts) ? atts : [];
            var quizzesTaken = studentAttempts.Select(a => a.QuizId).Distinct().Count();
            var avgScore = studentAttempts.Count > 0 ? studentAttempts.Average(a => a.ScorePercentage) : 0;

            studentSummaries.Add(new StudentGradeSummaryDto(
                StudentId: enr.StudentId,
                FullName: fullName,
                Email: email,
                EnrolledAtUtc: enr.EnrolledAtUtc,
                ProgressPercentage: enr.ProgressPercentage,
                IsCompleted: enr.IsCompleted,
                QuizzesTaken: quizzesTaken,
                AverageQuizScore: Math.Round(avgScore, 1)
            ));
        }

        var avgCompletion = enrollments.Count > 0 ? enrollments.Average(e => e.ProgressPercentage) : 0;

        var dto = new CourseGradebookDto(
            CourseId: course.Id,
            CourseTitle: course.Title,
            TotalStudents: enrollments.Count,
            AverageCompletionPercentage: Math.Round(avgCompletion, 1),
            Students: studentSummaries
        );

        return Result<CourseGradebookDto>.Success(dto);
    }
}
