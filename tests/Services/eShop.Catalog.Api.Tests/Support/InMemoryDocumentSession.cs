using BuildingBlock.Data;
using eShop.Catalog.Api.Abstractions;
using eShop.Catalog.Api.Data;
using eShop.Catalog.Api.Models;
using Marten;
using Marten.Linq;
using NSubstitute;

namespace eShop.Catalog.Api.Tests.Support;

public sealed class InMemoryDocumentSession
{
    private readonly Dictionary<Guid, Product> _products = new();

    public void Store(Product product)
    {
        _products[product.Id] = product;
    }

    public Task<Product?> LoadAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _products.TryGetValue(id, out var product);
        return Task.FromResult(product);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public IDocumentSession CreateSession()
    {
        var session = Substitute.For<IDocumentSession>();
        session.When(s => s.Store(Arg.Any<Product[]>())).Do(call =>
        {
            foreach (var product in call.ArgAt<Product[]>(0))
            {
                Store(product);
            }
        });
        session.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(call => SaveChangesAsync(call.Arg<CancellationToken>()));
        session.LoadAsync<Product>(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(call => LoadAsync(call.Arg<Guid>(), call.Arg<CancellationToken>()));
        session.Query<Product>().Returns(_ => CreateQueryable());
        return session;
    }

    public ICatalogUnitOfWork CreateUnitOfWork()
    {
        var session = CreateSession();
        return new CatalogUnitOfWork(session, new GenericRepository<Product>(session));
    }

    private IMartenQueryable<Product> CreateQueryable()
    {
        var data = _products.Values.AsQueryable();
        var queryable = Substitute.For<IMartenQueryable<Product>>();
        queryable.Provider.Returns(data.Provider);
        queryable.Expression.Returns(data.Expression);
        queryable.ElementType.Returns(data.ElementType);
        queryable.GetEnumerator().Returns(_ => data.GetEnumerator());
        ((IEnumerable<Product>)queryable).GetEnumerator().Returns(_ => data.GetEnumerator());
        return queryable;
    }
}
