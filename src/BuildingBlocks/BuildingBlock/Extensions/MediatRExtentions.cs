using System.Reflection;
using BuildingBlock.Behaviors;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlock.Extensions;

public static class MediatRExtensions
{
    public static IServiceCollection AddAppMediatR(this IServiceCollection services)
    {
        var entryAssembly = Assembly.GetEntryAssembly();
        var callingAssembly = Assembly.GetCallingAssembly();
        services.AddMediatR(cfg =>
        {
            if (entryAssembly is not null)
            {
                cfg.RegisterServicesFromAssembly(entryAssembly);
            }

            if (callingAssembly is not null && callingAssembly != entryAssembly)
            {
                cfg.RegisterServicesFromAssembly(callingAssembly);
            }

            cfg.AddOpenBehavior(typeof(LoggingAndTelemetryBehavior<,>));
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        return services;
    }
}