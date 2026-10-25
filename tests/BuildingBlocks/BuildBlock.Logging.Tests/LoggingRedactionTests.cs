using BuildBlock.Logging.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BuildBlock.Logging.Tests;

public class LoggingRedactionTests
{
    [Theory]
    [InlineData("Password")]
    [InlineData("password")]
    [InlineData("Secret")]
    [InlineData("ApiSecret")]
    [InlineData("Token")]
    [InlineData("AccessToken")]
    [InlineData("ConnectionString")]
    [InlineData("Authorization")]
    public async Task AddAppLogging_WhenPropertyNameIsSensitive_RedactsValue(string propertyName)
    {
        await using var app = LoggingTestHost.Create();
        await app.StartAsync();

        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Redaction");
        logger.LogInformation($"leaked {{{propertyName}}}", "super-secret");

        var evt = Assert.Single(
            CollectingSink.Instance.Events,
            e => e.RenderMessage().Contains("leaked"));

        Assert.True(evt.Properties.ContainsKey(propertyName), $"Missing property '{propertyName}'");
        Assert.Equal("***", evt.Properties[propertyName].ToString().Trim('"'));
    }
}
