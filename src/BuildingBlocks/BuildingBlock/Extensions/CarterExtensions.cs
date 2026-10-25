using System.Reflection;
using Carter;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlock.Extensions;

public static class CarterExtensions
{
    public static IServiceCollection AddAppCarter(this IServiceCollection services)
    {
        var entryAssembly = Assembly.GetEntryAssembly()
            ?? throw new InvalidOperationException("The entry assembly could not be determined.");

        services.AddCarter(
            assemblyCatalog: new DependencyContextAssemblyCatalog(entryAssembly));

        return services;
    }
}