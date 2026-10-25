using BuildBlock.Logging.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog.Events;

namespace BuildBlock.Logging.Tests;

public class LoggingConfigurationTests
{
    [Fact]
    public async Task AddAppLogging_WhenSerilogSectionMissing_AppliesSafeDefaults()
    {
        await using var app = LoggingTestHost.Create();
        await app.StartAsync();

        var factory = app.Services.GetRequiredService<ILoggerFactory>();
        factory.CreateLogger("App").LogInformation("default-info");
        factory.CreateLogger("Microsoft.AspNetCore").LogInformation("framework-info");
        factory.CreateLogger("Microsoft.AspNetCore").LogWarning("framework-warning");

        var events = CollectingSink.Instance.Events;
        Assert.Contains(events, e => e.RenderMessage().Contains("default-info"));
        Assert.DoesNotContain(events, e => e.RenderMessage().Contains("framework-info"));
        Assert.Contains(events, e => e.RenderMessage().Contains("framework-warning"));
        Assert.DoesNotContain(events, e => e.Properties.ContainsKey("OTEL"));
    }

    [Fact]
    public async Task AddAppLogging_WhenEnvironmentOverridesMinimumLevel_EnvironmentWins()
    {
        await using var app = LoggingTestHost.Create(
            configuration:
            [
                new("Serilog:MinimumLevel:Default", "Error")
            ],
            environmentConfiguration:
            [
                new("Serilog:MinimumLevel:Default", "Information")
            ]);
        await app.StartAsync();

        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Override");
        logger.LogInformation("env-visible");
        logger.LogDebug("env-hidden");

        var events = CollectingSink.Instance.Events;
        Assert.Contains(events, e => e.RenderMessage().Contains("env-visible"));
        Assert.DoesNotContain(events, e => e.RenderMessage().Contains("env-hidden"));
    }

    [Fact]
    public async Task AddAppLogging_WhenTwoHostsHaveDifferentLevels_EachHonorsOwnSettings()
    {
        await using (var quiet = LoggingTestHost.Create(
            configuration: [new("Serilog:MinimumLevel:Default", "Warning")],
            applicationName: "QuietService"))
        {
            await quiet.StartAsync();
            quiet.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Q").LogInformation("quiet-info");
            Assert.DoesNotContain(CollectingSink.Instance.Events, e => e.RenderMessage().Contains("quiet-info"));
        }

        await using var noisy = LoggingTestHost.Create(
            configuration: [new("Serilog:MinimumLevel:Default", "Debug")],
            applicationName: "NoisyService");
        await noisy.StartAsync();
        noisy.Services.GetRequiredService<ILoggerFactory>().CreateLogger("N").LogDebug("noisy-debug");

        var events = CollectingSink.Instance.Events;
        Assert.Contains(events, e => e.RenderMessage().Contains("noisy-debug"));
        Assert.Contains(events, e => Scalar(e, "service.name") == "NoisyService");
    }

    [Fact]
    public async Task AddAppLogging_WhenSerilogSectionSetsError_DropsInformation()
    {
        await using var app = LoggingTestHost.Create(
            configuration:
            [
                new("Serilog:MinimumLevel:Default", "Error"),
                new("Serilog:WriteTo:0:Name", "Console")
            ]);
        await app.StartAsync();

        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Configured");
        logger.LogInformation("should-drop");
        logger.LogError("should-keep");

        var events = CollectingSink.Instance.Events;
        Assert.DoesNotContain(events, e => e.RenderMessage().Contains("should-drop"));
        var kept = Assert.Single(events, e => e.RenderMessage().Contains("should-keep"));
        Assert.Equal(LogEventLevel.Error, kept.Level);
    }

    [Fact]
    public async Task AddAppLogging_WhenWriteToEmpty_StillWritesLocalEvents()
    {
        await using var app = LoggingTestHost.Create(
            configuration:
            [
                new("Serilog:MinimumLevel:Default", "Information")
            ]);
        await app.StartAsync();

        app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("EmptyWriteTo")
            .LogInformation("still-local");

        Assert.Contains(
            CollectingSink.Instance.Events,
            e => e.RenderMessage().Contains("still-local"));
    }

    private static string Scalar(LogEvent evt, string name) =>
        evt.Properties[name].ToString().Trim('"');
}
