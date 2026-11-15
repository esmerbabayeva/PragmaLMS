using Microsoft.EntityFrameworkCore;
using Pragma.Domain.Entities;

namespace Pragma.Application.Contracts;

public interface IAppDbContext
{
    DbSet<Category> Categories { get; }
    DbSet<Course> Courses { get; }
    DbSet<Module> Modules { get; }
    DbSet<Lesson> Lessons { get; }
    DbSet<LessonResource> LessonResources { get; }
    DbSet<LessonNote> LessonNotes { get; }
    DbSet<Quiz> Quizzes { get; }
    DbSet<Question> Questions { get; }
    DbSet<AnswerOption> AnswerOptions { get; }
    DbSet<QuizAttempt> QuizAttempts { get; }
    DbSet<QuizAttemptAnswer> QuizAttemptAnswers { get; }
    DbSet<Enrollment> Enrollments { get; }
    DbSet<LessonProgress> LessonProgresses { get; }
    DbSet<Review> Reviews { get; }
    DbSet<Certificate> Certificates { get; }
    DbSet<UserStreak> UserStreaks { get; }
    DbSet<UserBadge> UserBadges { get; }
    DbSet<AuthChallenge> AuthChallenges { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<Cohort> Cohorts { get; }
    DbSet<CohortSchedule> CohortSchedules { get; }
    DbSet<CohortEnrollment> CohortEnrollments { get; }
    DbSet<AttendanceRecord> AttendanceRecords { get; }
    DbSet<CohortGradeEntry> CohortGradeEntries { get; }
    DbSet<CohortNote> CohortNotes { get; }
    DbSet<CourseTranslation> CourseTranslations { get; }
    DbSet<CategoryTranslation> CategoryTranslations { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<Announcement> Announcements { get; }
    DbSet<LessonQuestion> LessonQuestions { get; }
    DbSet<LessonAnswer> LessonAnswers { get; }
    DbSet<QuizTemplate> QuizTemplates { get; }
    DbSet<QuizAssignment> QuizAssignments { get; }
    DbSet<FinalSubmission> FinalSubmissions { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
