using System.Text.Json;
using eShop.Catalog.Api.Tests.Support;

namespace eShop.Catalog.Api.Tests;

public class OpenApiDocumentTests
{
    [Fact]
    public async Task GetOpenApiDocument_IncludesUpdateProductPutOperation()
    {
        await using var factory = new CatalogApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var updateProductPath = document.RootElement
            .GetProperty("paths")
            .GetProperty("/products/{id}");

        Assert.True(updateProductPath.TryGetProperty("put", out _));

        var getProductsPath = document.RootElement
            .GetProperty("paths")
            .GetProperty("/products");
        Assert.True(getProductsPath.TryGetProperty("get", out _));
    }
}
