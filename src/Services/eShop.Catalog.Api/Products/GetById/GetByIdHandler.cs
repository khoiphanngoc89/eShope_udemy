using BuildingBlock.Cqrs;
using eShop.Catalog.Api.Data;
using eShop.Catalog.Api.Exceptions;
using FluentValidation;

namespace eShop.Catalog.Api.Products.GetById;

public sealed record GetByIdQuery(Guid Id) : BaseQuery<GetByIdResult>;
public sealed record GetByIdResult(Product Product);
public sealed class GetByIdValidator : AbstractValidator<GetByIdQuery>
{
    public GetByIdValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Product ID is required.");
    }
}
public sealed class GetByIdHandler(ICatalogUnitOfWork unitOfWork) : IQueryHandler<GetByIdQuery, GetByIdResult>
{
    public async Task<GetByIdResult> Handle(GetByIdQuery query, CancellationToken cancellationToken)
    {
        var product = await unitOfWork.Products.GetByIdAsync(query.Id, cancellationToken);
        if (product is null)
        {
            throw new ProductNotFoundException(query.Id);
        }

        return new GetByIdResult(product);
    }
}
