using System.Net;
using System.Text.Json;
using eShop.Catalog.Api.Models;
using eShop.Catalog.Api.Tests.Support;

namespace eShop.Catalog.Api.Tests;

public class GetProductsEndpointTests : IAsyncLifetime
{
    private CatalogApiFactory _factory = null!;
    private HttpClient _client = null!;

    public Task InitializeAsync()
    {
        _factory = new CatalogApiFactory();
        _client = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task GetProducts_WhenPageIsValid_ReturnsSliceAndTotalCount()
    {
        Seed(5);

        using var response = await _client.GetAsync("/products?pageNumber=1&pageSize=2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;
        Assert.Equal(1, root.GetProperty("pageNumber").GetInt32());
        Assert.Equal(2, root.GetProperty("pageSize").GetInt32());
        Assert.Equal(5, root.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, root.GetProperty("items").GetArrayLength());
        Assert.Equal(JsonValueKind.String, root.GetProperty("orderBy").ValueKind);
        Assert.Equal("CreatedAt", root.GetProperty("orderBy").GetString());
        Assert.Equal(JsonValueKind.String, root.GetProperty("orderDirection").ValueKind);
        Assert.Equal("Desc", root.GetProperty("orderDirection").GetString());
    }

    [Fact]
    public async Task GetProducts_WhenQueryIsOmitted_UsesPageOneAndSizeTen()
    {
        Seed(1);

        using var response = await _client.GetAsync("/products");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(1, json.RootElement.GetProperty("pageNumber").GetInt32());
        Assert.Equal(10, json.RootElement.GetProperty("pageSize").GetInt32());
        Assert.Equal(1, json.RootElement.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task GetProducts_WhenCatalogIsEmpty_ReturnsEmptyItemsAndZeroTotal()
    {
        using var response = await _client.GetAsync("/products?pageNumber=1&pageSize=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(0, json.RootElement.GetProperty("items").GetArrayLength());
        Assert.Equal(0, json.RootElement.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task GetProducts_WhenPageIsPastTheEnd_ReturnsEmptyItemsAndSameTotal()
    {
        Seed(2);

        using var response = await _client.GetAsync("/products?pageNumber=5&pageSize=2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(0, json.RootElement.GetProperty("items").GetArrayLength());
        Assert.Equal(2, json.RootElement.GetProperty("totalCount").GetInt32());
    }

    [Theory]
    [InlineData("/products?pageNumber=0")]
    [InlineData("/products?pageSize=0")]
    [InlineData("/products?pageSize=101")]
    [InlineData("/products?costPrice=-1")]
    public async Task GetProducts_WhenPageIsInvalid_ReturnsBadRequest(string url)
    {
        using var response = await _client.GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetProducts_WhenNameIsSupplied_ReturnsMatchingProducts()
    {
        var trail = SeedProduct("Trail Bike", 10m, ["bikes"]);
        SeedProduct("Helmet", 20m, ["gear"]);

        using var response = await _client.GetAsync("/products?name=bike");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(1, json.RootElement.GetProperty("totalCount").GetInt32());
        Assert.Equal(trail, json.RootElement.GetProperty("items")[0].GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task GetProducts_WhenCostPriceIsSupplied_ReturnsMatchingProducts()
    {
        var match = SeedProduct("Trail Bike", 10m, ["bikes"]);
        SeedProduct("Road Bike", 20m, ["bikes"]);

        using var response = await _client.GetAsync("/products?costPrice=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(1, json.RootElement.GetProperty("totalCount").GetInt32());
        Assert.Equal(match, json.RootElement.GetProperty("items")[0].GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task GetProducts_WhenProductIsOnlyInCatalogStore_OmitsItFromItems()
    {
        _factory.Documents.Store(new Product
        {
            Id = Guid.NewGuid(),
            Name = "Store Only",
            CostPrice = 1m,
            Description = "Desc",
            ImageFile = "file.png",
            Categories = ["bikes"]
        });

        using var response = await _client.GetAsync("/products");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(0, json.RootElement.GetProperty("totalCount").GetInt32());
        Assert.Equal(0, json.RootElement.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task GetProducts_WhenSearchIndexIsUnavailable_Returns503AndGetByIdAlsoFails()
    {
        var id = SeedProduct("Trail Bike", 10m, ["bikes"]);
        _factory.Search.Unavailable = true;

        using var searchResponse = await _client.GetAsync("/products");
        using var getByIdResponse = await _client.GetAsync($"/products/{id}");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, searchResponse.StatusCode);
        using var json = JsonDocument.Parse(await searchResponse.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.TryGetProperty("message", out _));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, getByIdResponse.StatusCode);
    }

    [Fact]
    public async Task GetProducts_WhenCategoryIsSupplied_ReturnsMatchingProducts()
    {
        var outdoor = SeedProduct("Tent", 50m, ["Outdoor"]);
        SeedProduct("Trail Bike", 10m, ["bikes"]);

        using var response = await _client.GetAsync("/products?category=outdoor");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(1, json.RootElement.GetProperty("totalCount").GetInt32());
        Assert.Equal(outdoor, json.RootElement.GetProperty("items")[0].GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task GetProducts_WhenOrderQueryUsesJsonStrings_EchoesOrderOnResult()
    {
        SeedProduct("Zebra", 1m, ["bikes"]);
        SeedProduct("Apple", 2m, ["bikes"]);

        using var response = await _client.GetAsync("/products?orderBy=Name&orderDirection=Asc");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;
        Assert.Equal(JsonValueKind.String, root.GetProperty("orderBy").ValueKind);
        Assert.Equal("Name", root.GetProperty("orderBy").GetString());
        Assert.Equal(JsonValueKind.String, root.GetProperty("orderDirection").ValueKind);
        Assert.Equal("Asc", root.GetProperty("orderDirection").GetString());
        Assert.Equal("Apple", root.GetProperty("items")[0].GetProperty("name").GetString());
    }

    private Guid SeedProduct(string name, decimal price, List<string> categories)
    {
        var id = Guid.NewGuid();
        var product = new Product
        {
            Id = id,
            Name = name,
            CostPrice = price,
            Description = "Desc",
            ImageFile = "file.png",
            Categories = categories
        };
        _factory.Documents.Store(product);
        _factory.Search.Store(product);
        return id;
    }

    private void Seed(int count)
    {
        for (var i = 0; i < count; i++)
        {
            var product = new Product
            {
                Id = Guid.NewGuid(),
                Name = $"Product {i}",
                CostPrice = i + 1,
                Description = "Desc",
                ImageFile = "file.png",
                Categories = ["bikes"]
            };
            _factory.Documents.Store(product);
            _factory.Search.Store(product);
        }
    }
}
