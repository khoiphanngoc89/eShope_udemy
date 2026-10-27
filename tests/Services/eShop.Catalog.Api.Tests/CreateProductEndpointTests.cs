using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using eShop.Catalog.Api.Tests.Support;

namespace eShop.Catalog.Api.Tests;

public class CreateProductEndpointTests : IAsyncLifetime
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
    public async Task PostProducts_WhenBodyIsValid_ReturnsCreatedAndStoresProduct()
    {
        var body = new
        {
            name = "Trail Bike",
            price = 1299.50m,
            description = "A sturdy trail bike",
            imageFile = "trail-bike.png",
            categories = new[] { "bikes", "outdoor" }
        };

        var response = await _client.PostAsJsonAsync("/products", body);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var id = json.RootElement.GetProperty("id").GetGuid();
        Assert.NotEqual(Guid.Empty, id);
        Assert.Contains($"/products/{id}", response.Headers.Location?.ToString());

        var stored = await _factory.Documents.LoadAsync(id);
        Assert.NotNull(stored);
        Assert.Equal(id, stored.Id);
        Assert.Equal(body.name, stored.Name);
        Assert.Equal(body.price, stored.Price);
        Assert.Equal(body.description, stored.Description);
        Assert.Equal(body.imageFile, stored.ImageFile);
        Assert.Equal(body.categories, stored.Categories);
    }

    [Fact]
    public async Task PostProducts_WhenCalledTwice_ReturnsDistinctIds()
    {
        var body = new
        {
            name = "Helmet",
            price = 40m,
            description = "Safe",
            imageFile = "helmet.png",
            categories = new[] { "safety" }
        };

        var first = await _client.PostAsJsonAsync("/products", body);
        var second = await _client.PostAsJsonAsync("/products", body);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        var firstId = JsonDocument.Parse(await first.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();
        var secondId = JsonDocument.Parse(await second.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();
        Assert.NotEqual(firstId, secondId);
    }
}
