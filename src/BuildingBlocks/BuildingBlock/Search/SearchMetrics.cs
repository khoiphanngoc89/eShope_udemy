using System.Diagnostics.Metrics;

namespace BuildingBlock.Search;

public sealed class SearchMetrics
{
    public SearchMetrics(string meterName, string instrumentPrefix)
    {
        MeterName = meterName;
        var meter = new Meter(meterName);
        _duration = meter.CreateHistogram<double>($"{instrumentPrefix}.duration", "ms");
        _errors = meter.CreateCounter<long>($"{instrumentPrefix}.errors");
        meter.CreateObservableGauge($"{instrumentPrefix}.sync.lag_seconds", () => SyncLagSeconds);
    }

    public string MeterName { get; }

    public long SyncLagSeconds { get; set; }

    private readonly Histogram<double> _duration;
    private readonly Counter<long> _errors;

    public void RecordDuration(double milliseconds) => _duration.Record(milliseconds);

    public void RecordError() => _errors.Add(1);
}
