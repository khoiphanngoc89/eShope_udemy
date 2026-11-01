using BuildingBlock.Search;
using eShop.Catalog.Api.Exceptions;

namespace eShop.Catalog.Api.Products.GetById;

public sealed record GetByIdQuery(Guid Id) : IQuery<GetByIdResult>;
public sealed record GetByIdResult(Product Product);
public sealed class GetByIdValidator : AbstractValidator<GetByIdQuery>
{
    public GetByIdValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Product ID is required.");
    }
}
public sealed class GetByIdHandler(ISearchStore<Product> searchStore) : IQueryHandler<GetByIdQuery, GetByIdResult>
{
    public async Task<GetByIdResult> Handle(GetByIdQuery query, CancellationToken cancellationToken)
    {
        var product = await searchStore.GetByIdAsync(query.Id, cancellationToken);
        ProductNotFoundException.ThrowIfNull(product, query.Id);

        return new GetByIdResult(product!);
    }
}
