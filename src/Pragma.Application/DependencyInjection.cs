using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Pragma.Application.Contracts;
using Pragma.Application.Services;

namespace Pragma.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // FluentValidation qaydalarını avtomatik assambleyadan qeydiyyata alırıq
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        // Biznes servisləri
        services.AddScoped<ICourseAccessService, CourseAccessService>();
        services.AddScoped<ICourseService, CourseService>();
        services.AddScoped<IModuleService, ModuleService>();
        services.AddScoped<ILessonService, LessonService>();
        services.AddScoped<IEnrollmentService, EnrollmentService>();
        services.AddScoped<IQuizService, QuizService>();
        services.AddScoped<ILearningProgressService, LearningProgressService>();
        services.AddScoped<ILessonNoteService, LessonNoteService>();
        services.AddScoped<ICertificateService, CertificateService>();
        services.AddScoped<IGamificationService, GamificationService>();
        services.AddScoped<IAiMentorService, AiMentorService>();
        services.AddScoped<ILiveSessionService, LiveSessionService>();
        services.AddScoped<IGradebookService, GradebookService>();
        services.AddScoped<ITeacherService, TeacherService>();
        services.AddScoped<ICertificateEligibilityPolicy, CertificateEligibilityPolicy>();
        services.AddScoped<IAcademicGradeService, AcademicGradeService>();
        services.AddScoped<IAttendanceService, AttendanceService>();
        services.AddScoped<ICohortService, CohortService>();
        services.AddScoped<ICohortNoteService, CohortNoteService>();
        services.AddScoped<IStudentGradeService, StudentGradeService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IAnnouncementService, AnnouncementService>();
        services.AddScoped<ILessonQaService, LessonQaService>();
        services.AddScoped<IQuizTemplateService, QuizTemplateService>();
        services.AddScoped<IQuizAssignmentService, QuizAssignmentService>();

        return services;
    }
}
