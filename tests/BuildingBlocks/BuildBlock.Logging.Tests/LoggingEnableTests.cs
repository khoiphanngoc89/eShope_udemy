using BuildBlock.Logging.Extensions;
using BuildBlock.Logging.Tests.Support;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog.Events;

namespace BuildBlock.Logging.Tests;

public class LoggingEnableTests
{
    [Fact]
    public void AddAppLogging_WhenBuilderIsNull_ThrowsArgumentNullException()
    {
        WebApplicationBuilder? builder = null;

        Assert.Throws<ArgumentNullException>(() => builder!.AddAppLogging());
    }

    [Fact]
    public async Task AddAppLogging_WhenHostStarts_WritesStructuredIdentityFields()
    {
        await using var app = LoggingTestHost.Create();
        await app.StartAsync();

        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("EnableTests");
        logger.LogInformation("catalog started");

        var evt = Assert.Single(
            CollectingSink.Instance.Events,
            e => e.RenderMessage().Contains("catalog started"));

        Assert.NotEqual(default, evt.Timestamp);
        Assert.Equal(LogEventLevel.Information, evt.Level);
        Assert.Contains("catalog started", evt.RenderMessage());
        Assert.Equal(app.Environment.ApplicationName, Scalar(evt, "service.name"));
        Assert.Equal(app.Environment.EnvironmentName, Scalar(evt, "deployment.environment"));
    }

    private static string Scalar(LogEvent evt, string name)
    {
        Assert.True(evt.Properties.ContainsKey(name), $"Missing property '{name}'");
        return evt.Properties[name].ToString().Trim('"');
    }
}
