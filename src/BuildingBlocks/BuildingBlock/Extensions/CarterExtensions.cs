using System.Linq;
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

        var assemblies = new DependencyContextAssemblyCatalog(entryAssembly)
            .GetAssemblies()
            .Append(entryAssembly)
            .Distinct()
            .ToArray();

        services.AddCarter(assemblyCatalog: new DependencyContextAssemblyCatalog(assemblies));

        return services;
    }
}