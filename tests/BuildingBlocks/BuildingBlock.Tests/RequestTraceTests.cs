using System.Diagnostics;
using BuildingBlock.Extensions;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace;

namespace BuildingBlock.Tests;

public class RequestTraceTests
{
    [Fact]
    public void AddAppOpenTelemetry_ListensToApplicationActivitySource()
    {
        var services = new ServiceCollection();
        services.AddAppOpenTelemetry("eShop.Catalog.Api");
        using var provider = services.BuildServiceProvider();
        _ = provider.GetRequiredService<TracerProvider>();

        using var source = new ActivitySource("eShop.Application");
        using var activity = source.StartActivity("MediatR GetProductsQuery");

        Assert.NotNull(activity);
    }
}
