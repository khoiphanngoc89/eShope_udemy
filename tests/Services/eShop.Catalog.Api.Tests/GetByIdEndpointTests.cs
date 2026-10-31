using System.Net;
using System.Text.Json;
using eShop.Catalog.Api.Models;
using eShop.Catalog.Api.Tests.Support;

namespace eShop.Catalog.Api.Tests;

public class GetByIdEndpointTests : IAsyncLifetime
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
    public async Task GetProductById_WhenProductExists_ReturnsProductAndTraits()
    {
        var product = StoreProduct(isDeleted: false, isActive: true);

        using var response = await _client.GetAsync($"/products/{product.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var body = json.RootElement.GetProperty("product");
        Assert.Equal(product.Id, body.GetProperty("id").GetGuid());
        Assert.Equal(product.Name, body.GetProperty("name").GetString());
        Assert.Equal(product.Price, body.GetProperty("price").GetDecimal());
        Assert.Equal(product.Description, body.GetProperty("description").GetString());
        Assert.Equal(product.ImageFile, body.GetProperty("imageFile").GetString());
        Assert.Equal(product.Categories, body.GetProperty("categories").EnumerateArray().Select(item => item.GetString()).ToArray());
        Assert.False(body.GetProperty("isDeleted").GetBoolean());
        Assert.True(body.GetProperty("isActive").GetBoolean());
        Assert.True(body.TryGetProperty("createdAt", out _));
        Assert.True(body.TryGetProperty("lastModified", out _));
        Assert.True(body.TryGetProperty("createdBy", out _));
        Assert.True(body.TryGetProperty("lastModifiedBy", out _));
    }

    [Fact]
    public async Task GetProductById_WhenProductIsInactive_ReturnsNotFound()
    {
        var product = StoreProduct(isDeleted: false, isActive: false);

        using var response = await _client.GetAsync($"/products/{product.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty("message").GetString()));
    }

    [Fact]
    public async Task GetProductById_WhenProductIsMissing_ReturnsNotFound()
    {
        using var response = await _client.GetAsync($"/products/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty("message").GetString()));
    }

    [Fact]
    public async Task GetProductById_WhenProductIsDeleted_ReturnsNotFound()
    {
        var product = StoreProduct(isDeleted: true, isActive: true);

        using var response = await _client.GetAsync($"/products/{product.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty("message").GetString()));
    }

    private Product StoreProduct(bool isDeleted, bool isActive)
    {
        var now = DateTime.UtcNow;
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Trail Bike",
            Price = 1299.50m,
            Description = "A sturdy trail bike",
            ImageFile = "trail-bike.png",
            Categories = ["bikes", "outdoor"],
            CreatedAt = now,
            LastModified = now,
            IsDeleted = isDeleted,
            IsActive = isActive
        };
        _factory.Documents.Store(product);
        return product;
    }
}
