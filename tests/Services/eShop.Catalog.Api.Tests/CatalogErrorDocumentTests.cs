using eShop.Catalog.Api.Logging;
using Serilog.Events;
using Serilog.Parsing;

namespace eShop.Catalog.Api.Tests;

public class CatalogErrorDocumentTests
{
    [Fact]
    public void TryMap_WhenLevelIsError_IncludesRequiredFields()
    {
        var timestamp = new DateTimeOffset(2026, 11, 1, 4, 52, 0, TimeSpan.Zero);
        var exception = new InvalidOperationException("search failed");
        var logEvent = CreateLogEvent(
            timestamp,
            LogEventLevel.Error,
            exception,
            "GetProducts failed after 12ms",
            ("service.name", "eShop.Catalog.Api"),
            ("TraceId", "abc123"),
            ("SpanId", "def456"));

        Assert.True(CatalogErrorDocument.TryMap(logEvent, out var document));
        Assert.NotNull(document);
        Assert.Equal(timestamp.UtcDateTime, document.Timestamp);
        Assert.Contains("GetProducts", document.Message, StringComparison.Ordinal);
        Assert.Contains("12ms", document.Message, StringComparison.Ordinal);
        Assert.Contains("InvalidOperationException", document.Message, StringComparison.Ordinal);
        Assert.Equal("Error", document.Level);
        Assert.Equal("eShop.Catalog.Api", document.ServiceName);
        Assert.Contains("InvalidOperationException", document.Exception, StringComparison.Ordinal);
        Assert.Equal("abc123", document.TraceId);
        Assert.Equal("def456", document.SpanId);
    }

    [Fact]
    public void TryMap_WhenLevelIsFatal_IncludesRequiredFields()
    {
        var logEvent = CreateLogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Fatal,
            exception: null,
            "Catalog crashed",
            ("service.name", "eShop.Catalog.Api"));

        Assert.True(CatalogErrorDocument.TryMap(logEvent, out var document));
        Assert.NotNull(document);
        Assert.Equal("Fatal", document.Level);
        Assert.Equal("eShop.Catalog.Api", document.ServiceName);
        Assert.Equal("Catalog crashed", document.Message);
        Assert.True(document.Timestamp.Kind == DateTimeKind.Utc || document.Timestamp.Kind == DateTimeKind.Unspecified);
    }

    [Theory]
    [InlineData(LogEventLevel.Information)]
    [InlineData(LogEventLevel.Debug)]
    [InlineData(LogEventLevel.Warning)]
    [InlineData(LogEventLevel.Verbose)]
    public void TryMap_WhenLevelIsBelowError_DoesNotMapToCatalogAppLogs(LogEventLevel level)
    {
        var logEvent = CreateLogEvent(
            DateTimeOffset.UtcNow,
            level,
            exception: null,
            "not an error",
            ("service.name", "eShop.Catalog.Api"));

        Assert.False(CatalogErrorDocument.TryMap(logEvent, out var document));
        Assert.Null(document);
    }

    private static LogEvent CreateLogEvent(
        DateTimeOffset timestamp,
        LogEventLevel level,
        Exception? exception,
        string message,
        params (string Name, string Value)[] properties)
    {
        var template = new MessageTemplateParser().Parse(message);
        var props = properties
            .Select(property => new LogEventProperty(property.Name, new ScalarValue(property.Value)))
            .ToList();

        return new LogEvent(timestamp, level, exception, template, props);
    }
}
