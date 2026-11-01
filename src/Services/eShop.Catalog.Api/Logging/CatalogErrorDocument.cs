using System.Text.Json.Serialization;
using Serilog.Events;

namespace eShop.Catalog.Api.Logging;

public sealed record CatalogErrorDocument
{
    public const string IndexName = "catalog-app-logs";

    [JsonPropertyName("@timestamp")]
    public required DateTime Timestamp { get; init; }

    public required string Message { get; init; }

    public required string Level { get; init; }

    public string? Exception { get; init; }

    [JsonPropertyName("service.name")]
    public required string ServiceName { get; init; }

    [JsonPropertyName("trace_id")]
    public string? TraceId { get; init; }

    [JsonPropertyName("span_id")]
    public string? SpanId { get; init; }

    public static bool TryMap(LogEvent logEvent, out CatalogErrorDocument? document)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        document = null;

        if (logEvent.Level is not LogEventLevel.Error and not LogEventLevel.Fatal)
        {
            return false;
        }

        var rendered = logEvent.RenderMessage();
        var exception = logEvent.Exception?.ToString();
        var message = exception is null ? rendered : $"{rendered}{Environment.NewLine}{exception}";

        document = new CatalogErrorDocument
        {
            Timestamp = logEvent.Timestamp.UtcDateTime,
            Message = message,
            Level = logEvent.Level.ToString(),
            Exception = exception,
            ServiceName = ReadScalar(logEvent, "service.name") ?? "eShop.Catalog.Api",
            TraceId = ReadScalar(logEvent, "TraceId") ?? ReadScalar(logEvent, "trace_id") ?? logEvent.TraceId?.ToHexString(),
            SpanId = ReadScalar(logEvent, "SpanId") ?? ReadScalar(logEvent, "span_id") ?? logEvent.SpanId?.ToHexString()
        };

        return true;
    }

    private static string? ReadScalar(LogEvent logEvent, string name)
    {
        return logEvent.Properties.TryGetValue(name, out var value) && value is ScalarValue { Value: { } raw }
            ? raw.ToString()
            : null;
    }
}
