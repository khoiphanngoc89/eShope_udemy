using System.Diagnostics;
using BuildingBlock.Behaviors;
using BuildingBlock.Abstractions.Cqrs;
using Microsoft.Extensions.Logging;

namespace BuildingBlock.Tests;

public class LoggingAndTelemetryBehaviorTests
{
    [Fact]
    public async Task Handle_WhenHandlerThrows_LogsStackTrace()
    {
        var logger = new CapturingLogger<LoggingAndTelemetryBehavior<BoomCommand, string>>();
        var behavior = new LoggingAndTelemetryBehavior<BoomCommand, string>(logger);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            behavior.Handle(new BoomCommand(), _ => BoomHandler.Throw(), CancellationToken.None));

        var entry = Assert.Single(logger.Entries, item => item.Exception is not null);
        Assert.Same(exception, entry.Exception);
        Assert.Contains("BoomCommand", entry.Message, StringComparison.Ordinal);
        Assert.Contains("boom from handler", entry.Message, StringComparison.Ordinal);
        Assert.Contains("at ", entry.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(BoomHandler), entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Handle_WhenListenerIsAttached_RecordsMediatRActivity()
    {
        var recorded = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "eShop.Application",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = recorded.Add
        };
        ActivitySource.AddActivityListener(listener);

        var logger = new CapturingLogger<LoggingAndTelemetryBehavior<BoomCommand, string>>();
        var behavior = new LoggingAndTelemetryBehavior<BoomCommand, string>(logger);

        await behavior.Handle(new BoomCommand(), _ => Task.FromResult("ok"), CancellationToken.None);

        var activity = Assert.Single(recorded);
        Assert.Equal("MediatR BoomCommand", activity.DisplayName);
        Assert.Equal("BoomCommand", activity.GetTagItem("mediatr.request_type"));
    }

    private sealed record BoomCommand : ICommand<string>;

    private static class BoomHandler
    {
        public static Task<string> Throw() =>
            throw new InvalidOperationException("boom from handler");
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<(string Message, Exception? Exception)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add((formatter(state, exception), exception));
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }
}
