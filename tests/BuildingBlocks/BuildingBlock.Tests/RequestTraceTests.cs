using System.Diagnostics;
using BuildingBlock.Extensions;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace BuildingBlock.Tests;

public class RequestTraceTests
{
    [Fact]
    public void AddAppOpenTelemetry_ListensToApplicationActivitySource()
    {
        var services = new ServiceCollection();
        services.AddAppOpenTelemetry("eShop.Ordering.Api");
        using var provider = services.BuildServiceProvider();
        _ = provider.GetRequiredService<TracerProvider>();

        using var source = new ActivitySource("eShop.Application");
        using var activity = source.StartActivity("MediatR GetOrdersQuery");

        Assert.NotNull(activity);
    }

    [Fact]
    public void AddAppOpenTelemetry_BuildsWhenCallerSuppliesItsOwnMeter()
    {
        var services = new ServiceCollection();
        services.AddAppOpenTelemetry(
            "eShop.Ordering.Api",
            configureMetrics: metrics => metrics.AddMeter("eShop.Ordering"));
        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<MeterProvider>());
        Assert.NotNull(provider.GetRequiredService<TracerProvider>());
    }
}
