using Serilog.Core;
using Serilog.Events;

namespace BuildBlock.Logging.Enrichers;

internal sealed class SensitiveDataEnricher : ILogEventEnricher
{
    private static readonly string[] Tokens =
        ["Password", "Secret", "Token", "ConnectionString", "Authorization"];

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        foreach (var name in logEvent.Properties.Keys.ToArray())
        {
            if (!Tokens.Any(token => name.Contains(token, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty(name, "***"));
        }
    }
}
