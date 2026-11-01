using System.Text.Json;
using BuildingBlock.Search;
using eShop.Catalog.Api.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace eShop.Catalog.Api.Tests;

public class ProductSearchIndexerTests
{
    [Theory]
    [InlineData("c")]
    [InlineData("r")]
    [InlineData("u")]
    public async Task Apply_WhenActiveNotDeleted_UpsertsDocument(string op)
    {
        var store = new InMemorySearchStore<Product>();
        var indexer = new SearchIndexer<Product>(store, NullLogger<SearchIndexer<Product>>.Instance);
        var product = CreateProduct();

        await indexer.ApplyAsync(Event(op, product));

        Assert.True(store.Contains(product.Id));
    }

    [Theory]
    [InlineData("d", false, true)]
    [InlineData("u", true, true)]
    [InlineData("u", false, false)]
    public async Task Apply_WhenDeletedOrInactive_RemovesDocument(string op, bool deleted, bool active)
    {
        var store = new InMemorySearchStore<Product>();
        var product = CreateProduct();
        store.Store(product);
        var indexer = new SearchIndexer<Product>(store, NullLogger<SearchIndexer<Product>>.Instance);
        product.IsDeleted = deleted;
        product.IsActive = active;

        await indexer.ApplyAsync(Event(op, product));

        Assert.False(store.Contains(product.Id));
    }

    [Fact]
    public async Task Apply_WhenVersionIsStale_DoesNotOverwriteNewerDocument()
    {
        var store = new InMemorySearchStore<Product>();
        var newer = CreateProduct();
        newer.Name = "New";
        newer.LastModified = DateTimeOffset.UtcNow;
        store.Store(newer);
        var indexer = new SearchIndexer<Product>(store, NullLogger<SearchIndexer<Product>>.Instance);
        var stale = CreateProduct();
        stale.Id = newer.Id;
        stale.Name = "Old";
        stale.LastModified = newer.LastModified.AddMinutes(-5);

        await indexer.ApplyAsync(Event("u", stale));

        var page = await store.SearchAsync(new SearchQuery
        {
            ContainsField = nameof(Product.Name),
            ContainsValue = "New"
        });
        Assert.Equal(1, page.TotalCount);
    }

    [Fact]
    public async Task Apply_WhenPayloadIsInvalid_LogsErrorAndThrows()
    {
        var store = new InMemorySearchStore<Product>();
        var logger = new RecordingLogger();
        var indexer = new SearchIndexer<Product>(store, logger);

        await Assert.ThrowsAnyAsync<Exception>(() => indexer.ApplyAsync("{"));
        Assert.Contains(logger.Messages, message => message.Contains("Search sync failed", StringComparison.Ordinal));
        Assert.DoesNotContain(logger.Messages, message => message.Contains("success", StringComparison.OrdinalIgnoreCase));
    }

    private static Product CreateProduct() => new()
    {
        Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
        Name = "Trail Bike",
        CostPrice = 10m,
        Description = "Desc",
        ImageFile = "file.png",
        Categories = ["bikes"],
        CreatedAt = DateTimeOffset.UtcNow,
        LastModified = DateTimeOffset.UtcNow,
        IsActive = true,
        IsDeleted = false
    };

    private static string Event(string op, Product product) => JsonSerializer.Serialize(new
    {
        op,
        after = product
    });

    private sealed class RecordingLogger : Microsoft.Extensions.Logging.ILogger<SearchIndexer<Product>>
    {
        public List<string> Messages { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;

        public void Log<TState>(
            Microsoft.Extensions.Logging.LogLevel logLevel,
            Microsoft.Extensions.Logging.EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose()
            {
            }
        }
    }
}
