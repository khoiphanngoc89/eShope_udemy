using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using eShop.Catalog.Api.Tests.Support;

namespace eShop.Catalog.Api.Tests;

[Trait("Category", "Integration")]
public class CatalogProductSearchIntegrationTests : IAsyncLifetime
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
    public async Task Create_WhenSearchHasNotSynced_OmitsProductFromBrowseAndGetById()
    {
        var id = await CreateProduct("Unsynced Helmet");

        using var browse = await _client.GetAsync("/products");
        using var getById = await _client.GetAsync($"/products/{id}");

        Assert.Equal(HttpStatusCode.OK, browse.StatusCode);
        using var page = JsonDocument.Parse(await browse.Content.ReadAsStringAsync());
        Assert.Equal(0, page.RootElement.GetProperty("totalCount").GetInt32());
        Assert.Equal(HttpStatusCode.NotFound, getById.StatusCode);
    }

    [Fact]
    public async Task Create_WhenSearchHasSynced_BrowseAndGetByIdReturnTheProduct()
    {
        var id = await CreateProduct("Synced Trail Bike");
        await SyncSearch(id);

        using var browse = await _client.GetAsync("/products");
        using var getById = await _client.GetAsync($"/products/{id}");

        Assert.Equal(HttpStatusCode.OK, browse.StatusCode);
        using var page = JsonDocument.Parse(await browse.Content.ReadAsStringAsync());
        Assert.Equal(1, page.RootElement.GetProperty("totalCount").GetInt32());
        Assert.Equal(id, page.RootElement.GetProperty("items")[0].GetProperty("id").GetGuid());
        Assert.Equal(JsonValueKind.String, page.RootElement.GetProperty("orderBy").ValueKind);
        Assert.Equal("CreatedAt", page.RootElement.GetProperty("orderBy").GetString());
        Assert.Equal(JsonValueKind.String, page.RootElement.GetProperty("orderDirection").ValueKind);
        Assert.Equal("Desc", page.RootElement.GetProperty("orderDirection").GetString());

        Assert.Equal(HttpStatusCode.OK, getById.StatusCode);
        using var product = JsonDocument.Parse(await getById.Content.ReadAsStringAsync());
        Assert.Equal(id, product.RootElement.GetProperty("product").GetProperty("id").GetGuid());
        Assert.Equal("Synced Trail Bike", product.RootElement.GetProperty("product").GetProperty("name").GetString());
    }

    [Fact]
    public async Task GetProducts_WhenOrderByJsonStrings_SortsAndEchoesOrderOnPage()
    {
        await SyncSearch(await CreateProduct("Zebra"));
        await SyncSearch(await CreateProduct("Apple"));

        using var response = await _client.GetAsync("/products?orderBy=Name&orderDirection=Asc");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var page = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = page.RootElement;
        Assert.Equal("Name", root.GetProperty("orderBy").GetString());
        Assert.Equal("Asc", root.GetProperty("orderDirection").GetString());
        Assert.Equal("Apple", root.GetProperty("items")[0].GetProperty("name").GetString());
        Assert.Equal("Zebra", root.GetProperty("items")[1].GetProperty("name").GetString());
    }

    [Fact]
    public async Task BrowseAndGetById_WhenSearchIndexIsUnavailable_Return503()
    {
        var id = await CreateProduct("Outage Bike");
        await SyncSearch(id);
        _factory.Search.Unavailable = true;

        using var browse = await _client.GetAsync("/products");
        using var getById = await _client.GetAsync($"/products/{id}");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, browse.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, getById.StatusCode);
        using var browseJson = JsonDocument.Parse(await browse.Content.ReadAsStringAsync());
        using var getByIdJson = JsonDocument.Parse(await getById.Content.ReadAsStringAsync());
        Assert.True(browseJson.RootElement.TryGetProperty("message", out _));
        Assert.True(getByIdJson.RootElement.TryGetProperty("message", out _));
    }

    private async Task<Guid> CreateProduct(string name)
    {
        var body = new
        {
            name,
            amount = 1,
            costPrice = 10m,
            sewingCost = 0m,
            fabricCostAccessoriesCost = 0m,
            packagingCost = 0m,
            description = "Integration",
            imageFile = "item.png",
            categories = new[] { "bikes" }
        };

        using var created = await _client.PostAsJsonAsync("/products", body);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var json = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("id").GetGuid();
    }

    private async Task SyncSearch(Guid id)
    {
        var stored = await _factory.Documents.LoadAsync(id);
        Assert.NotNull(stored);
        _factory.Search.Store(stored);
    }
}
