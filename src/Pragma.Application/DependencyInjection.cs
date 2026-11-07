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

        return services;
    }
}
