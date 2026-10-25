using System.Collections.Concurrent;
using Serilog.Core;
using Serilog.Events;

namespace BuildBlock.Logging.Tests.Support;

public sealed class CollectingSink : ILogEventSink
{
    public static CollectingSink Instance { get; } = new();

    private readonly ConcurrentQueue<LogEvent> _events = new();

    public IReadOnlyList<LogEvent> Events => _events.ToArray();

    public void Emit(LogEvent logEvent) => _events.Enqueue(logEvent);

    public void Clear()
    {
        while (_events.TryDequeue(out _))
        {
        }
    }
}
