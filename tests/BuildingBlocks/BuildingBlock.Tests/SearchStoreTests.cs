using BuildingBlock.Exceptions;
using BuildingBlock.Models;
using BuildingBlock.Search;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace BuildingBlock.Tests;

public class SearchStoreTests
{
    [Fact]
    public async Task Search_WhenStoreIsUnavailable_ThrowsSearchIndexUnavailableException()
    {
        var store = new InMemorySearchStore<SampleItem> { Unavailable = true };

        await Assert.ThrowsAsync<SearchIndexUnavailableException>(() =>
            store.SearchAsync(new SearchQuery(), CancellationToken.None));
    }

    [Fact]
    public async Task Search_WhenMoreItemsThanPageSize_ReturnsOnlyThatPageAndFilteredTotal()
    {
        var store = new InMemorySearchStore<SampleItem>();
        Seed(store, 5);

        var result = await store.SearchAsync(new SearchQuery { PageNumber = 1, PageSize = 2 }, CancellationToken.None);

        Assert.Equal(1, result.PageNumber);
        Assert.Equal(2, result.PageSize);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(5, result.TotalCount);
    }

    [Fact]
    public async Task Search_WhenSomeItemsAreInactiveOrDeleted_OmitsThem()
    {
        var store = new InMemorySearchStore<SampleItem>();
        var active = CreateItem("active");
        var inactive = CreateItem("inactive", isActive: false);
        var deleted = CreateItem("deleted", isDeleted: true);
        store.Store(active);
        store.Store(inactive);
        store.Store(deleted);

        var result = await store.SearchAsync(new SearchQuery(), CancellationToken.None);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal([active.Id], result.Items.Select(item => item.Id));
    }

    [Fact]
    public async Task Search_WhenContainsValueIsSupplied_ReturnsCaseInsensitiveMatches()
    {
        var store = new InMemorySearchStore<SampleItem>();
        var trail = CreateItem("Trail Bike", price: 10m, tags: ["bikes"]);
        store.Store(trail);
        store.Store(CreateItem("Helmet", price: 20m, tags: ["gear"]));

        var result = await store.SearchAsync(new SearchQuery
        {
            ContainsField = nameof(SampleItem.Name),
            ContainsValue = "bike"
        }, CancellationToken.None);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal([trail.Id], result.Items.Select(item => item.Id));
    }

    [Fact]
    public async Task Search_WhenExactAndCollectionFiltersAreSupplied_ReturnsIntersection()
    {
        var store = new InMemorySearchStore<SampleItem>();
        var match = CreateItem("Trail Bike", price: 10m, tags: ["bikes"]);
        store.Store(match);
        store.Store(CreateItem("City Bike", price: 10m, tags: ["bikes"]));
        store.Store(CreateItem("Trail Bike", price: 20m, tags: ["bikes"]));
        store.Store(CreateItem("Trail Bike", price: 10m, tags: ["gear"]));

        var result = await store.SearchAsync(new SearchQuery
        {
            ContainsField = nameof(SampleItem.Name),
            ContainsValue = "trail",
            ExactMatches = new Dictionary<string, object?> { [nameof(SampleItem.Price)] = 10m },
            CollectionContainsField = nameof(SampleItem.Tags),
            CollectionContainsValue = "bikes"
        }, CancellationToken.None);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal([match.Id], result.Items.Select(item => item.Id));
    }

    [Fact]
    public async Task Upsert_WhenVersionIsStale_DoesNotOverwriteNewerDocument()
    {
        var store = new InMemorySearchStore<SampleItem>();
        var newer = CreateItem("New");
        newer.LastModified = DateTimeOffset.UtcNow;
        store.Store(newer);
        var stale = CreateItem("Old");
        stale.Id = newer.Id;
        stale.LastModified = newer.LastModified.AddMinutes(-5);

        await store.UpsertAsync(stale, CancellationToken.None);

        var page = await store.SearchAsync(new SearchQuery
        {
            ContainsField = nameof(SampleItem.Name),
            ContainsValue = "New"
        }, CancellationToken.None);
        Assert.Equal(1, page.TotalCount);
    }

    [Fact]
    public void UnavailableSearchStore_ThrowsSearchIndexUnavailableException()
    {
        ISearchStore<SampleItem> store = new UnavailableSearchStore<SampleItem>();

        Assert.Throws<SearchIndexUnavailableException>(() =>
            store.SearchAsync(new SearchQuery(), CancellationToken.None).GetAwaiter().GetResult());
    }

    [Fact]
    public void IsMissingIndex_WhenElasticsearchHasNoSuchIndex_IsNotAnOutage()
    {
        Assert.True(ElasticsearchSearchStore<SampleItem>.IsMissingIndex(
            "index_not_found_exception",
            "no such index [catalog-products]"));
    }

    [Fact]
    public void IsMissingIndex_WhenTheIndexCannotBeReached_IsAnOutage()
    {
        Assert.False(ElasticsearchSearchStore<SampleItem>.IsMissingIndex(null, "connection refused"));
    }

    [Fact]
    public void ContainsQuery_WhenNameHasVietnameseText_UsesCaseInsensitiveKeywordContains()
    {
        var query = ElasticsearchSearchStore<SampleItem>.ContainsQuery(
            nameof(SampleItem.Name),
            "Đầm Alice - size 1 (Xanh dương)");

        Assert.Equal("name.keyword", query.Field.ToString());
        Assert.Equal("*Đầm Alice - size 1 (Xanh dương)*", query.Value);
        Assert.True(query.CaseInsensitive);
    }

    private static SampleItem CreateItem(
        string name,
        decimal price = 1m,
        List<string>? tags = null,
        bool isDeleted = false,
        bool isActive = true) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Price = price,
        Tags = tags ?? ["bikes"],
        CreatedAt = DateTimeOffset.UtcNow,
        LastModified = DateTimeOffset.UtcNow,
        IsDeleted = isDeleted,
        IsActive = isActive
    };

    private static void Seed(InMemorySearchStore<SampleItem> store, int count)
    {
        for (var i = 0; i < count; i++)
        {
            store.Store(new SampleItem
            {
                Id = Guid.NewGuid(),
                Name = $"Item {i}",
                Price = i + 1,
                Tags = ["bikes"]
            });
        }
    }

    private sealed class SampleItem : BaseEntity
    {
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public List<string> Tags { get; set; } = [];
    }
}

public class SearchIndexerTests
{
    [Theory]
    [InlineData("c")]
    [InlineData("r")]
    [InlineData("u")]
    public async Task Apply_WhenActiveNotDeleted_UpsertsDocument(string op)
    {
        var store = new InMemorySearchStore<SampleItem>();
        var indexer = new SearchIndexer<SampleItem>(store, NullLogger<SearchIndexer<SampleItem>>.Instance);
        var item = CreateItem();

        await indexer.ApplyAsync(Event(op, item));

        Assert.True(store.Contains(item.Id));
    }

    [Theory]
    [InlineData("d", false, true)]
    [InlineData("u", true, true)]
    [InlineData("u", false, false)]
    public async Task Apply_WhenDeletedOrInactive_RemovesDocument(string op, bool deleted, bool active)
    {
        var store = new InMemorySearchStore<SampleItem>();
        var item = CreateItem();
        store.Store(item);
        var indexer = new SearchIndexer<SampleItem>(store, NullLogger<SearchIndexer<SampleItem>>.Instance);
        item.IsDeleted = deleted;
        item.IsActive = active;

        await indexer.ApplyAsync(Event(op, item));

        Assert.False(store.Contains(item.Id));
    }

    [Fact]
    public async Task Apply_WhenPayloadIsInvalid_LogsErrorAndThrows()
    {
        var store = new InMemorySearchStore<SampleItem>();
        var logger = new RecordingLogger();
        var indexer = new SearchIndexer<SampleItem>(store, logger);

        await Assert.ThrowsAnyAsync<Exception>(() => indexer.ApplyAsync("{"));
        Assert.Contains(logger.Messages, message => message.Contains("Search sync failed", StringComparison.Ordinal));
        Assert.DoesNotContain(logger.Messages, message => message.Contains("success", StringComparison.OrdinalIgnoreCase));
    }

    private static SampleItem CreateItem() => new()
    {
        Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
        Name = "Trail Bike",
        Price = 10m,
        Tags = ["bikes"],
        CreatedAt = DateTimeOffset.UtcNow,
        LastModified = DateTimeOffset.UtcNow,
        IsActive = true,
        IsDeleted = false
    };

    private static string Event(string op, SampleItem item) => System.Text.Json.JsonSerializer.Serialize(new
    {
        op,
        after = item
    });

    private sealed class SampleItem : BaseEntity
    {
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public List<string> Tags { get; set; } = [];
    }

    private sealed class RecordingLogger : ILogger<SearchIndexer<SampleItem>>
    {
        public List<string> Messages { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
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
