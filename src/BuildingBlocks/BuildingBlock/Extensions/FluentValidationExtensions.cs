using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlock.Extensions;

public static class FluentValidationExtensions
{
    public static void AddFluentValidation(this IServiceCollection services)
    {
        var entryAssembly = Assembly.GetEntryAssembly();
        var callingAssembly = Assembly.GetCallingAssembly();
        if (entryAssembly is not null)
        {
            services.AddValidatorsFromAssembly(entryAssembly);
        }

        if (callingAssembly is not null && callingAssembly != entryAssembly)
        {
            services.AddValidatorsFromAssembly(callingAssembly);
        }
    }
}
