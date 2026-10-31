using BuildingBlock.Exceptions;

namespace eShop.Catalog.Api.Exceptions;

public class ProductNotFoundException : NotFoundException
{
    public ProductNotFoundException(Guid id) : base($"Product with ID {id} not found.")
    {
    }

    public static void ThrowIfNull(Product? product, Guid id)
    {
        if (product is null)
        {
            throw new ProductNotFoundException(id);
        }
    }
}
