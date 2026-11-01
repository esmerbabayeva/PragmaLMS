using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Pragma.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // FluentValidation qaydalarını avtomatik assambleyadan qeydiyyata alırıq
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        return services;
    }
}
