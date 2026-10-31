using BuildingBlock.Models;

namespace BuildingBlock.Cqrs;

public abstract record BaseQuery<TResponse> : IQuery<TResponse>
    where TResponse : notnull;

public static class BaseQuery
{
    public static bool Matches<T>(T? entity)
        where T : class, IActivable, IDeletable
        => entity is { IsActive: true, IsDeleted: false };

    public static IQueryable<T> Filter<T>(IQueryable<T> query)
        where T : IActivable, IDeletable
        => query.Where(entity => entity.IsActive && !entity.IsDeleted);
}

public static class BaseQueryExtensions
{
    public static IQueryable<T> ApplyBaseQuery<T>(this IQueryable<T> query)
        where T : IActivable, IDeletable
        => BaseQuery.Filter(query);
}
