using BuildingBlock.Abstractions;
using BuildingBlock.Abstractions.Cqrs;
using BuildingBlock.Models;

namespace BuildingBlock.Data;


public static class Query
{
    public static bool Matches<T>(T? entity)
        where T : class, IActivable, IDeletable
        => entity is { IsActive: true, IsDeleted: false };

    public static IQueryable<T> Filter<T>(IQueryable<T> query)
        where T : IActivable, IDeletable
        => query.Where(entity => entity.IsActive && !entity.IsDeleted);
}

public static class QueryExtensions
{
    public static IQueryable<T> ApplyQuery<T>(this IQueryable<T> query)
        where T : IActivable, IDeletable
        => Query.Filter(query);
}
