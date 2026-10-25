using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Core;
using BuildBlock.Logging.Extensions;

namespace BuildBlock.Logging.Tests.Support;

public static class LoggingTestHost
{
    public static WebApplication Create(
        IEnumerable<KeyValuePair<string, string?>>? configuration = null,
        IEnumerable<KeyValuePair<string, string?>>? environmentConfiguration = null,
        string environment = "Development",
        string applicationName = "TestService",
        bool clearSink = true)
    {
        if (clearSink)
        {
            CollectingSink.Instance.Clear();
        }

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = environment
        });
        builder.Environment.ApplicationName = applicationName;

        if (configuration is not null)
        {
            builder.Configuration.AddInMemoryCollection(configuration);
        }

        if (environmentConfiguration is not null)
        {
            builder.Configuration.AddInMemoryCollection(environmentConfiguration);
        }

        builder.WebHost.UseSetting(WebHostDefaults.PreventHostingStartupKey, "true");
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton<ILogEventSink>(CollectingSink.Instance);
        builder.AddAppLogging();
        return builder.Build();
    }
}
