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

    [Fact]
    public async Task GetOpenApiDocument_GetProductsEnumsAreJsonStrings()
    {
        await using var factory = new CatalogApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var parameters = document.RootElement
            .GetProperty("paths")
            .GetProperty("/products")
            .GetProperty("get")
            .GetProperty("parameters");

        var orderBy = Schema(document.RootElement, Parameter(parameters, "orderBy").GetProperty("schema"));
        var orderDirection = Schema(document.RootElement, Parameter(parameters, "orderDirection").GetProperty("schema"));

        AssertJsonStringEnum(orderBy, "CreatedAt", "Name");
        AssertJsonStringEnum(orderDirection, "Desc");
    }

    private static JsonElement Parameter(JsonElement parameters, string name)
    {
        foreach (var parameter in parameters.EnumerateArray())
        {
            if (string.Equals(parameter.GetProperty("name").GetString(), name, StringComparison.OrdinalIgnoreCase))
            {
                return parameter;
            }
        }

        throw new InvalidOperationException($"OpenAPI parameter '{name}' was not found.");
    }

    private static JsonElement Schema(JsonElement document, JsonElement schema)
    {
        if (!schema.TryGetProperty("$ref", out var reference))
        {
            return schema;
        }

        var path = reference.GetString() ?? string.Empty;
        const string prefix = "#/components/schemas/";
        Assert.StartsWith(prefix, path, StringComparison.Ordinal);
        return document.GetProperty("components").GetProperty("schemas").GetProperty(path[prefix.Length..]);
    }

    private static void AssertJsonStringEnum(JsonElement schema, params string[] expected)
    {
        if (schema.TryGetProperty("type", out var type))
        {
            Assert.Equal("string", type.GetString());
        }

        var values = schema.GetProperty("enum").EnumerateArray()
            .Select(item =>
            {
                Assert.Equal(JsonValueKind.String, item.ValueKind);
                return item.GetString()!;
            })
            .ToArray();
        foreach (var name in expected)
        {
            Assert.Contains(values, value => string.Equals(value, name, StringComparison.OrdinalIgnoreCase));
        }
    }
}
