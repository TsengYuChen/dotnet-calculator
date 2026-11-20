using Microsoft.Extensions.DependencyInjection;
using VehicleGo.Application.Abstractions;
using VehicleGo.Application.Auth;

namespace VehicleGo.Application;

public static class DependencyInjection
{
    private static readonly Type[] HandlerInterfaces =
        [typeof(ICommandHandler<>), typeof(ICommandHandler<,>), typeof(IQueryHandler<,>)];

    /// <summary>
    /// Registers every handler of this assembly (scoped, by each handler interface it implements) and
    /// <see cref="LoginAdmission"/>. <see cref="TimeProvider"/> and the abstractions' implementations are
    /// registered by Web / Infrastructure.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var handlers = typeof(DependencyInjection).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false });
        foreach (var implementation in handlers)
        {
            foreach (var service in implementation.GetInterfaces()
                         .Where(i => i.IsGenericType && HandlerInterfaces.Contains(i.GetGenericTypeDefinition())))
            {
                services.AddScoped(service, implementation);
            }
        }

        services.AddScoped<LoginAdmission>();
        return services;
    }
}
