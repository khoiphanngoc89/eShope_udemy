using Elastic.Ingest.Elasticsearch;
using Elastic.Serilog.Sinks;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace eShop.Catalog.Api.Logging;

public static class CatalogErrorLogSinkExtensions
{
    public static WebApplicationBuilder AddCatalogErrorLogSink(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var nodes = builder.Configuration.GetSection("Serilog:Elasticsearch:Nodes").Get<string[]>()
            ?? [];
        var uris = nodes
            .Where(node => !string.IsNullOrWhiteSpace(node))
            .Select(node => new Uri(node))
            .ToArray();

        if (uris.Length == 0)
        {
            return builder;
        }

        builder.Services.AddSingleton<ILogEventSink>(_ => CreateSink(uris));
        return builder;
    }

    private static ILogEventSink CreateSink(Uri[] nodes)
    {
        var elasticsearch = new LoggerConfiguration()
            .MinimumLevel.Error()
            .WriteTo.Elasticsearch(nodes, options =>
            {
                options.DataStream = new Elastic.Ingest.Elasticsearch.DataStreams.DataStreamName("catalog", "app", "logs");
                options.BootstrapMethod = BootstrapMethod.Silent;
            })
            .CreateLogger();

        return new FailOpenErrorSink(elasticsearch);
    }

    private sealed class FailOpenErrorSink(Serilog.ILogger inner) : ILogEventSink
    {
        public void Emit(LogEvent logEvent)
        {
            if (!CatalogErrorDocument.TryMap(logEvent, out _))
            {
                return;
            }

            try
            {
                inner.Write(logEvent);
            }
            catch (Exception)
            {
                // Fail-open: console + OTLP/Loki still receive the event.
            }
        }
    }
}
