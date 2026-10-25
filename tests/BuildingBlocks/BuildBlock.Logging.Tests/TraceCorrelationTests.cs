using System.Diagnostics;
using BuildBlock.Logging.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BuildBlock.Logging.Tests;

public class TraceCorrelationTests
{
    [Fact]
    public async Task AddAppLogging_WhenActivityIsCurrent_StampsMatchingTraceAndSpanIds()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded
        };
        ActivitySource.AddActivityListener(listener);

        await using var app = LoggingTestHost.Create();
        await app.StartAsync();

        using var activity = new Activity("catalog-request").Start();
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Trace");
        for (var i = 0; i < 20; i++)
        {
            logger.LogInformation("traced-{Index}", i);
        }

        var traced = CollectingSink.Instance.Events
            .Where(e => e.RenderMessage().Contains("traced-"))
            .ToArray();

        Assert.Equal(20, traced.Length);
        var matched = traced.Count(e =>
            e.TraceId == activity.TraceId && e.SpanId == activity.SpanId);
        Assert.True(matched >= 19, $"Expected at least 95% correlation, got {matched}/20");
        Assert.Equal(32, activity.TraceId.ToHexString().Length);
        Assert.Equal(16, activity.SpanId.ToHexString().Length);
    }

    [Fact]
    public async Task AddAppLogging_WhenActivityExistsWithoutTestListener_StampsMatchingIds()
    {
        await using var app = LoggingTestHost.Create();
        await app.StartAsync();

        using var activity = new Activity("catalog-request").Start();
        app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Trace")
            .LogInformation("host-traced");

        var evt = Assert.Single(
            CollectingSink.Instance.Events,
            e => e.RenderMessage().Contains("host-traced"));
        Assert.Equal(activity.TraceId, evt.TraceId);
        Assert.Equal(activity.SpanId, evt.SpanId);
    }

    [Fact]
    public async Task AddAppLogging_WhenNoActivity_OmitsTraceIdsAndStillWrites()
    {
        await using var app = LoggingTestHost.Create();
        await app.StartAsync();

        Activity.Current = null;
        app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("NoTrace")
            .LogInformation("untraced");

        var evt = Assert.Single(
            CollectingSink.Instance.Events,
            e => e.RenderMessage().Contains("untraced"));
        Assert.Null(evt.TraceId);
        Assert.Null(evt.SpanId);
        Assert.Contains("untraced", evt.RenderMessage());
    }

    [Fact]
    public async Task AddAppLogging_WhenOpenTelemetryWriteToOmitted_HostStartsAndWritesLocally()
    {
        await using var app = LoggingTestHost.Create(
            configuration:
            [
                new("Serilog:Using:0", "Serilog.Sinks.Console"),
                new("Serilog:MinimumLevel:Default", "Information"),
                new("Serilog:WriteTo:0:Name", "Console")
            ]);
        await app.StartAsync();

        app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Local")
            .LogInformation("no-otlp");

        Assert.Contains(CollectingSink.Instance.Events, e => e.RenderMessage().Contains("no-otlp"));
    }

    [Fact]
    public async Task AddAppLogging_WhenOpenTelemetryEndpointUnreachable_HostStartsAndWritesLocally()
    {
        await using var app = LoggingTestHost.Create(
            configuration:
            [
                new("Serilog:Using:0", "Serilog.Sinks.Console"),
                new("Serilog:Using:1", "Serilog.Sinks.OpenTelemetry"),
                new("Serilog:MinimumLevel:Default", "Information"),
                new("Serilog:WriteTo:0:Name", "Console"),
                new("Serilog:WriteTo:1:Name", "OpenTelemetry"),
                new("Serilog:WriteTo:1:Args:endpoint", "http://127.0.0.1:1"),
                new("Serilog:WriteTo:1:Args:protocol", "Grpc")
            ]);
        await app.StartAsync();

        app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Otlp")
            .LogInformation("otlp-failopen");

        Assert.Contains(CollectingSink.Instance.Events, e => e.RenderMessage().Contains("otlp-failopen"));
    }
}
