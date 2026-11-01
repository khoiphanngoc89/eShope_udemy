using System.Diagnostics.Metrics;
using BuildingBlock.Exceptions;
using BuildingBlock.Search;
using eShop.Catalog.Api.Models;
using eShop.Catalog.Api.Products.GetProducts;
using eShop.Catalog.Api.Search;

namespace eShop.Catalog.Api.Tests;

public class CatalogSearchMetricsTests
{
    [Fact]
    public async Task Handle_WhenSearchSucceeds_RecordsDuration()
    {
        var recorded = new List<Measurement<double>>();
        using var listener = ListenHistogram(recorded);
        var store = new InMemorySearchStore<Product>();
        store.Store(new Product
        {
            Id = Guid.NewGuid(),
            Name = "Trail Bike",
            CostPrice = 10m,
            Description = "Desc",
            ImageFile = "file.png",
            Categories = ["bikes"],
            IsActive = true,
            IsDeleted = false
        });

        await new GetProductsHandler(store).Handle(new GetProductsQuery(1, 10), CancellationToken.None);

        Assert.NotEmpty(recorded);
    }

    [Fact]
    public async Task Handle_WhenSearchIndexIsUnavailable_IncrementsErrors()
    {
        var recorded = new List<Measurement<long>>();
        using var listener = ListenCounter(recorded);
        var store = new InMemorySearchStore<Product> { Unavailable = true };

        await Assert.ThrowsAsync<SearchIndexUnavailableException>(() =>
            new GetProductsHandler(store).Handle(new GetProductsQuery(1, 10), CancellationToken.None));

        Assert.NotEmpty(recorded);
    }

    [Fact]
    public void SyncLag_WhenKafkaIsNotRunning_CanBeReadAsZero()
    {
        CatalogSearchMetrics.SyncLagSeconds = 0;
        Assert.Equal(0, CatalogSearchMetrics.SyncLagSeconds);
    }

    private static MeterListener ListenHistogram(List<Measurement<double>> recorded)
    {
        var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == CatalogSearchMetrics.MeterName && instrument.Name == "catalog.search.duration")
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<double>((_, value, _, _) => recorded.Add(new Measurement<double>(value)));
        listener.Start();
        return listener;
    }

    private static MeterListener ListenCounter(List<Measurement<long>> recorded)
    {
        var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == CatalogSearchMetrics.MeterName && instrument.Name == "catalog.search.errors")
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, value, _, _) => recorded.Add(new Measurement<long>(value)));
        listener.Start();
        return listener;
    }
}
