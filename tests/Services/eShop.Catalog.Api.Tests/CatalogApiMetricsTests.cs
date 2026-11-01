using System.Diagnostics.Metrics;
using eShop.Catalog.Api.Tests.Support;

namespace eShop.Catalog.Api.Tests;

public class CatalogApiMetricsTests
{
    [Fact]
    public void Catalog_RegistersItsOwnMetersOnSharedOpenTelemetry()
    {
        var extensionsPath = Path.Combine(
            RepoRoot.Path,
            "src",
            "BuildingBlocks",
            "BuildingBlock",
            "Extensions",
            "OpenTelemetryExtensions.cs");
        var catalogPath = Path.Combine(
            RepoRoot.Path,
            "src",
            "Services",
            "eShop.Catalog.Api",
            "DependencyInjection.cs");
        Assert.True(File.Exists(extensionsPath), extensionsPath);
        Assert.True(File.Exists(catalogPath), catalogPath);

        var extensions = File.ReadAllText(extensionsPath);
        var catalog = File.ReadAllText(catalogPath);

        Assert.DoesNotContain("eShop.Catalog.Search", extensions, StringComparison.Ordinal);
        Assert.Contains("AddAspNetCoreInstrumentation", extensions, StringComparison.Ordinal);
        Assert.Contains("configureMetrics", extensions, StringComparison.Ordinal);

        Assert.Contains("CatalogSearchMetrics.MeterName", catalog, StringComparison.Ordinal);
        Assert.Contains("SearchMetrics.SharedMeterName", catalog, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HttpRequest_PublishesAspNetCoreServerDurationMetric()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            seen.Add(instrument.Name);
            l.EnableMeasurementEvents(instrument);
        };
        listener.Start();

        await using var factory = new CatalogApiFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/products?pageNumber=1&pageSize=1");

        Assert.True(
            seen.Contains("http.server.request.duration"),
            "Catalog.Api should export ASP.NET request duration (AddAspNetCoreInstrumentation on metrics).");
        Assert.True((int)response.StatusCode is >= 200 and < 600);
    }
}
