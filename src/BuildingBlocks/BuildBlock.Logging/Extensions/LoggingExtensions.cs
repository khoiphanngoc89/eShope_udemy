using BuildBlock.Logging.Enrichers;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace BuildBlock.Logging.Extensions;

public static class LoggingExtensions
{
    private const string ConsoleTemplate =
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} ({service.name}/{deployment.environment}) {TraceId}{NewLine}{Exception}";

    public static WebApplicationBuilder AddAppLogging(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var serviceName = builder.Environment.ApplicationName;
        var environmentName = builder.Environment.EnvironmentName;
        builder.Logging.SetMinimumLevel(LogLevel.Trace);

        builder.Host.UseSerilog((context, services, configuration) =>
        {
            var serilogSection = context.Configuration.GetSection("Serilog");
            if (serilogSection.Exists())
            {
                configuration.ReadFrom.Configuration(context.Configuration);

                if (!serilogSection.GetSection("WriteTo").GetChildren().Any())
                {
                    configuration.WriteTo.Console(outputTemplate: ConsoleTemplate);
                }
            }
            else
            {
                configuration
                    .MinimumLevel.Information()
                    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
                    .WriteTo.Console(outputTemplate: ConsoleTemplate);
            }

            configuration
                .Enrich.FromLogContext()
                .Enrich.With(new SensitiveDataEnricher())
                .Enrich.WithProperty("service.name", serviceName)
                .Enrich.WithProperty("deployment.environment", environmentName);

            foreach (var sink in services.GetServices<ILogEventSink>())
            {
                configuration.WriteTo.Sink(sink);
            }
        });

        return builder;
    }
}
