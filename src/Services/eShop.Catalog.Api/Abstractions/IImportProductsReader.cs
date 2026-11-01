namespace eShop.Catalog.Api.Abstractions;

public interface IImportProductsReader
{
    IReadOnlyList<eShop.Catalog.Api.Products.ImportProducts.ImportedProduct> Read(Stream stream);
}