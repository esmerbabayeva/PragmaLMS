using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Pragma.Domain.Entities;
using Pragma.Domain.Enums;
using Pragma.Infrastructure.Persistence;
using Xunit;

namespace Pragma.IntegrationTests;

public class SqliteRelationalTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public SqliteRelationalTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:;Foreign Keys=True");
        _connection.Open();

        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = "PRAGMA foreign_keys = ON;";
            cmd.ExecuteNonQuery();
        }

        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    private AppDbContext CreateContext() => new(_options);

    private (Guid CategoryId, Guid CourseId, Guid CohortId) SeedCohort(AppDbContext context, string prefix = "test")
    {
        var category = new Category
        {
            Name = $"{prefix} Category",
            Slug = $"{prefix}-cat-{Guid.NewGuid():N}",
            Description = "Desc",
            IconName = "folder"
        };
        context.Categories.Add(category);
        context.SaveChanges();

        var course = new Course
        {
            Title = $"{prefix} Course",
            Slug = $"{prefix}-crs-{Guid.NewGuid():N}",
            CategoryId = category.Id,
            Price = 0,
            IsFree = true,
            Level = CourseLevel.Beginner
        };
        context.Courses.Add(course);
        context.SaveChanges();

        var cohort = new Cohort
        {
            Name = $"{prefix} Cohort",
            CourseId = course.Id,
            TeacherId = Guid.NewGuid(),
            StartDate = DateTime.UtcNow
        };
        context.Cohorts.Add(cohort);
        context.SaveChanges();

        return (category.Id, course.Id, cohort.Id);
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    // 2.2 (a) Category.Slug: yarat, soft delete, eyni slug ilə yenidən yarat (uğurlu); aktiv ikinci eyni slug (xəta)
    [Fact]
    public void CategorySlug_UniqueIndex_AllowsRecreationAfterSoftDelete_AndRejectsDuplicateActive()
    {
        using (var context = CreateContext())
        {
            var cat1 = new Category
            {
                Name = "Test Category 1",
                Slug = "test-category",
                Description = "Desc 1",
                IconName = "tag"
            };
            context.Categories.Add(cat1);
            context.SaveChanges();

            // Soft delete
            cat1.MarkDeleted();
            context.SaveChanges();
        }

        // Recreate with same slug (Should succeed because cat1 is soft-deleted)
        using (var context = CreateContext())
        {
            var cat2 = new Category
            {
                Name = "Test Category 2",
                Slug = "test-category",
                Description = "Desc 2",
                IconName = "tag"
            };
            context.Categories.Add(cat2);
            context.SaveChanges();
        }

        // Attempting to add a second active category with the same slug must throw DbUpdateException
        using (var context = CreateContext())
        {
            var cat3 = new Category
            {
                Name = "Test Category 3",
                Slug = "test-category",
                Description = "Desc 3",
                IconName = "tag"
            };
            context.Categories.Add(cat3);

            var ex = Assert.Throws<DbUpdateException>(() => context.SaveChanges());
            Assert.NotNull(ex);
        }
    }

    // 2.2 (b) Course.Slug: yarat, soft delete, eyni slug ilə yenidən yarat (uğurlu); aktiv ikinci eyni slug (xəta)
    [Fact]
    public void CourseSlug_UniqueIndex_AllowsRecreationAfterSoftDelete_AndRejectsDuplicateActive()
    {
        Guid categoryId;
        using (var context = CreateContext())
        {
            var category = new Category { Name = "Backend", Slug = "backend-slug-test", Description = "Desc", IconName = "code" };
            context.Categories.Add(category);
            context.SaveChanges();
            categoryId = category.Id;

            var course1 = new Course
            {
                Title = "Course 1",
                Slug = "course-slug",
                CategoryId = categoryId,
                Price = 0,
                IsFree = true,
                Level = CourseLevel.Beginner
            };
            context.Courses.Add(course1);
            context.SaveChanges();

            // Soft delete
            course1.MarkDeleted();
            context.SaveChanges();
        }

        // Recreate with same slug (Should succeed)
        using (var context = CreateContext())
        {
            var course2 = new Course
            {
                Title = "Course 2",
                Slug = "course-slug",
                CategoryId = categoryId,
                Price = 0,
                IsFree = true,
                Level = CourseLevel.Beginner
            };
            context.Courses.Add(course2);
            context.SaveChanges();
        }

        // Second active course with same slug must throw DbUpdateException
        using (var context = CreateContext())
        {
            var course3 = new Course
            {
                Title = "Course 3",
                Slug = "course-slug",
                CategoryId = categoryId,
                Price = 0,
                IsFree = true,
                Level = CourseLevel.Intermediate
            };
            context.Courses.Add(course3);

            var ex = Assert.Throws<DbUpdateException>(() => context.SaveChanges());
            Assert.NotNull(ex);
        }
    }

    // 2.2 (c) CohortGradeEntry: köhnə sətri MarkDeleted et, eyni (CohortId, StudentId, ComponentType) ilə yenisini əlavə et (uğurlu); aktiv ikinci eyni açar (xəta)
    [Fact]
    public void CohortGradeEntry_CompositeUniqueIndex_AllowsRecreationAfterSoftDelete_AndRejectsDuplicateActive()
    {
        Guid cohortId;
        var studentId = Guid.NewGuid();

        using (var context = CreateContext())
        {
            var seed = SeedCohort(context, "grade");
            cohortId = seed.CohortId;

            var entry1 = new CohortGradeEntry
            {
                CohortId = cohortId,
                StudentId = studentId,
                ComponentType = GradeComponentType.MiniProject,
                Score = 80m,
                GradedAtUtc = DateTime.UtcNow
            };
            context.CohortGradeEntries.Add(entry1);
            context.SaveChanges();

            // Soft delete
            entry1.MarkDeleted();
            context.SaveChanges();
        }

        // Re-add entry with same composite key (Should succeed because entry1 is soft-deleted)
        using (var context = CreateContext())
        {
            var entry2 = new CohortGradeEntry
            {
                CohortId = cohortId,
                StudentId = studentId,
                ComponentType = GradeComponentType.MiniProject,
                Score = 90m,
                GradedAtUtc = DateTime.UtcNow
            };
            context.CohortGradeEntries.Add(entry2);
            context.SaveChanges();
        }

        // Duplicate active entry must fail
        using (var context = CreateContext())
        {
            var entry3 = new CohortGradeEntry
            {
                CohortId = cohortId,
                StudentId = studentId,
                ComponentType = GradeComponentType.MiniProject,
                Score = 100m,
                GradedAtUtc = DateTime.UtcNow
            };
            context.CohortGradeEntries.Add(entry3);

            var ex = Assert.Throws<DbUpdateException>(() => context.SaveChanges());
            Assert.NotNull(ex);
        }
    }

    // 2.2 (d) CohortEnrollment, AttendanceRecord unikal indeksləri: təkrarlanan açar xəta verir
    [Fact]
    public void CohortEnrollment_UniqueIndex_RejectsDuplicateActiveKeys()
    {
        Guid cohortId;
        var studentId = Guid.NewGuid();

        using (var context = CreateContext())
        {
            var seed = SeedCohort(context, "enroll");
            cohortId = seed.CohortId;

            var enrollment1 = new CohortEnrollment
            {
                CohortId = cohortId,
                StudentId = studentId,
                Status = CohortEnrollmentStatus.Enrolled
            };
            context.CohortEnrollments.Add(enrollment1);
            context.SaveChanges();

            var enrollment2 = new CohortEnrollment
            {
                CohortId = cohortId,
                StudentId = studentId,
                Status = CohortEnrollmentStatus.Enrolled
            };
            context.CohortEnrollments.Add(enrollment2);

            var ex = Assert.Throws<DbUpdateException>(() => context.SaveChanges());
            Assert.NotNull(ex);
        }
    }

    [Fact]
    public void AttendanceRecord_UniqueIndex_RejectsDuplicateDatePerStudent()
    {
        Guid cohortId;
        var studentId = Guid.NewGuid();
        var date = new DateOnly(2026, 11, 14);

        using (var context = CreateContext())
        {
            var seed = SeedCohort(context, "att");
            cohortId = seed.CohortId;

            var att1 = new AttendanceRecord
            {
                CohortId = cohortId,
                StudentId = studentId,
                Date = date,
                Status = AttendanceStatus.Present
            };
            context.AttendanceRecords.Add(att1);
            context.SaveChanges();

            var att2 = new AttendanceRecord
            {
                CohortId = cohortId,
                StudentId = studentId,
                Date = date,
                Status = AttendanceStatus.Late
            };
            context.AttendanceRecords.Add(att2);

            var ex = Assert.Throws<DbUpdateException>(() => context.SaveChanges());
            Assert.NotNull(ex);
        }
    }

    // 2.2 (e) Cohort silinəndə Restrict/Cascade FK davranışlarından ən azı ikisi
    [Fact]
    public void ForeignKeyBehavior_CourseDeleteRestrict_WhenCohortExists()
    {
        Guid courseId;

        using (var context = CreateContext())
        {
            var seed = SeedCohort(context, "fk-crs");
            courseId = seed.CourseId;
        }

        // Deleting course when cohort references it should violate Restrict FK
        using (var context = CreateContext())
        {
            var course = context.Courses.First(c => c.Id == courseId);
            context.Courses.Remove(course);

            var ex = Assert.Throws<DbUpdateException>(() => context.SaveChanges());
            Assert.NotNull(ex);
        }
    }

    [Fact]
    public void ForeignKeyBehavior_CohortDeleteRestrict_WhenEnrollmentExists()
    {
        Guid cohortId;

        using (var context = CreateContext())
        {
            var seed = SeedCohort(context, "fk-enr");
            cohortId = seed.CohortId;

            var enrollment = new CohortEnrollment
            {
                CohortId = cohortId,
                StudentId = Guid.NewGuid(),
                Status = CohortEnrollmentStatus.Enrolled
            };
            context.CohortEnrollments.Add(enrollment);
            context.SaveChanges();
        }

        // Deleting cohort when enrollment exists should violate Restrict FK on CohortEnrollment.CohortId
        using (var context = CreateContext())
        {
            var cohort = context.Cohorts.First(c => c.Id == cohortId);
            context.Cohorts.Remove(cohort);

            var ex = Assert.Throws<DbUpdateException>(() => context.SaveChanges());
            Assert.NotNull(ex);
        }
    }

    [Fact]
    public void ForeignKeyBehavior_CohortDeleteCascade_DeletesSchedules()
    {
        Guid cohortId;
        Guid scheduleId;

        using (var context = CreateContext())
        {
            var seed = SeedCohort(context, "fk-sched");
            cohortId = seed.CohortId;

            var schedule = new CohortSchedule
            {
                CohortId = cohortId,
                DayOfWeek = DayOfWeek.Monday,
                StartTime = new TimeSpan(10, 0, 0),
                EndTime = new TimeSpan(12, 0, 0)
            };
            context.CohortSchedules.Add(schedule);
            context.SaveChanges();
            scheduleId = schedule.Id;
        }

        // Deleting cohort should cascade delete schedules (DeleteBehavior.Cascade)
        using (var context = CreateContext())
        {
            var cohort = context.Cohorts.First(c => c.Id == cohortId);
            context.Cohorts.Remove(cohort);
            context.SaveChanges();
        }

        using (var context = CreateContext())
        {
            Assert.Null(context.Cohorts.Find(cohortId));
            Assert.Null(context.CohortSchedules.Find(scheduleId));
        }
    }
}
