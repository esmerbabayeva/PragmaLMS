using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Pragma.Application.Contracts;
using Pragma.Domain.Common;
using Pragma.Domain.Entities;
using Pragma.Infrastructure.Identity;

namespace Pragma.Infrastructure.Persistence;

public class AppDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>, IAppDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<Module> Modules => Set<Module>();
    public DbSet<Lesson> Lessons => Set<Lesson>();
    public DbSet<LessonResource> LessonResources => Set<LessonResource>();
    public DbSet<LessonNote> LessonNotes => Set<LessonNote>();
    public DbSet<Quiz> Quizzes => Set<Quiz>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<AnswerOption> AnswerOptions => Set<AnswerOption>();
    public DbSet<QuizAttempt> QuizAttempts => Set<QuizAttempt>();
    public DbSet<QuizAttemptAnswer> QuizAttemptAnswers => Set<QuizAttemptAnswer>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();
    public DbSet<LessonProgress> LessonProgresses => Set<LessonProgress>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<Certificate> Certificates => Set<Certificate>();
    public DbSet<UserStreak> UserStreaks => Set<UserStreak>();
    public DbSet<UserBadge> UserBadges => Set<UserBadge>();
    public DbSet<AuthChallenge> AuthChallenges => Set<AuthChallenge>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // 1. Kurs konfiqurasiyası
        builder.Entity<Course>(entity =>
        {
            entity.HasIndex(c => c.Slug).IsUnique();
            entity.Property(c => c.Title).HasMaxLength(200).IsRequired();
            entity.Property(c => c.Price).HasPrecision(18, 2);

            entity.HasOne(c => c.Category)
                  .WithMany(cat => cat.Courses)
                  .HasForeignKey(c => c.CategoryId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(c => c.Modules)
                  .WithOne(m => m.Course)
                  .HasForeignKey(m => m.CourseId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // 2. Modul və Dərs konfiqurasiyası
        builder.Entity<Module>(entity =>
        {
            entity.Property(m => m.Title).HasMaxLength(200).IsRequired();

            entity.HasMany(m => m.Lessons)
                  .WithOne(l => l.Module)
                  .HasForeignKey(l => l.ModuleId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Lesson>(entity =>
        {
            entity.Property(l => l.Title).HasMaxLength(200).IsRequired();

            entity.HasOne(l => l.Quiz)
                  .WithOne(q => q.Lesson)
                  .HasForeignKey<Quiz>(q => q.LessonId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(l => l.Resources)
                  .WithOne(r => r.Lesson)
                  .HasForeignKey(r => r.LessonId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(l => l.Notes)
                  .WithOne(n => n.Lesson)
                  .HasForeignKey(n => n.LessonId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // 3. Quiz konfiqurasiyası
        builder.Entity<Quiz>(entity =>
        {
            entity.HasMany(q => q.Questions)
                  .WithOne(qu => qu.Quiz)
                  .HasForeignKey(qu => qu.QuizId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Question>(entity =>
        {
            entity.HasMany(qu => qu.Options)
                  .WithOne(o => o.Question)
                  .HasForeignKey(o => o.QuestionId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // 4. Kaskad qorunması
        builder.Entity<Enrollment>(entity =>
        {
            entity.HasIndex(e => new { e.StudentId, e.CourseId }).IsUnique();

            entity.HasOne(e => e.Course)
                  .WithMany(c => c.Enrollments)
                  .HasForeignKey(e => e.CourseId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.Property(e => e.MiniProjectGrade).HasPrecision(5, 2);
            entity.Property(e => e.FinalGrade).HasPrecision(5, 2);
        });

        builder.Entity<QuizAttempt>(entity =>
        {
            entity.HasOne(qa => qa.Quiz)
                  .WithMany(q => q.Attempts)
                  .HasForeignKey(qa => qa.QuizId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(qa => qa.Answers)
                  .WithOne(a => a.QuizAttempt)
                  .HasForeignKey(a => a.QuizAttemptId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Review>(entity =>
        {
            entity.HasIndex(r => new { r.StudentId, r.CourseId }).IsUnique();

            entity.HasOne(r => r.Course)
                  .WithMany(c => c.Reviews)
                  .HasForeignKey(r => r.CourseId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<LessonProgress>(entity =>
        {
            entity.HasIndex(lp => new { lp.StudentId, lp.LessonId }).IsUnique();

            entity.HasOne(lp => lp.Lesson)
                  .WithMany(l => l.Progresses)
                  .HasForeignKey(lp => lp.LessonId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Certificate>(entity =>
        {
            entity.HasIndex(cert => cert.CertificateCode).IsUnique();

            entity.HasOne(cert => cert.Course)
                  .WithMany(c => c.Certificates)
                  .HasForeignKey(cert => cert.CourseId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Category>(entity =>
        {
            entity.HasIndex(c => c.Slug).IsUnique();
        });

        // 5. Global Query Filters (Soft delete)
        builder.Entity<Category>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<Course>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<Module>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<Lesson>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<LessonResource>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<LessonNote>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<Quiz>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<Question>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<AnswerOption>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<QuizAttempt>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<QuizAttemptAnswer>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<Enrollment>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<LessonProgress>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<Review>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<Certificate>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<UserStreak>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<UserBadge>().HasQueryFilter(e => !e.IsDeleted);

        builder.Entity<AuthChallenge>(entity =>
        {
            entity.HasIndex(a => a.EmailNormalized);
            entity.HasIndex(a => a.ExpiresUtc);
        });

        builder.Entity<RefreshToken>(entity =>
        {
            entity.HasIndex(r => r.TokenHash).IsUnique();
            entity.HasIndex(r => r.FamilyId);
            entity.HasIndex(r => r.UserId);
        });

        builder.Entity<AuditLog>(entity =>
        {
            entity.Property(a => a.Action).HasMaxLength(100).IsRequired();
            entity.Property(a => a.EntityName).HasMaxLength(100).IsRequired();
            entity.Property(a => a.EntityId).HasMaxLength(100).IsRequired();
            entity.Property(a => a.UserEmail).HasMaxLength(256);
            entity.Property(a => a.IpAddress).HasMaxLength(64);
            entity.HasIndex(a => a.CreatedAtUtc);
            entity.HasIndex(a => a.UserId);
        });

        // 6. BaseEntity RowVersion (Optimistic Concurrency)
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            if (typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
            {
                builder.Entity(entityType.ClrType).Property(nameof(BaseEntity.RowVersion)).IsRowVersion();
            }
        }
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var entries = ChangeTracker.Entries<BaseEntity>();

        foreach (var entry in entries)
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    break;
                case EntityState.Modified:
                    entry.Entity.MarkUpdated();
                    break;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
