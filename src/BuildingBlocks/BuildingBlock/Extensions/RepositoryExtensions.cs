using BuildingBlock.Data;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlock.Extensions;

public static class RepositoryExtensions
{
    public static IServiceCollection AddGenericRepository(this IServiceCollection services)
    {
        services.AddScoped(typeof(IRepository<>), typeof(GenericRepository<>));
        return services;
    }
}
