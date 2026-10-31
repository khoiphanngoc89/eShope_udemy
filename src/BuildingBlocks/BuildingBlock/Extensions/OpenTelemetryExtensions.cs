using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Logs;
using OpenTelemetry.Trace;

namespace BuildingBlock.Extensions;

public static class OpenTelemetryExtensions
{
    public static IServiceCollection AddAppOpenTelemetry(this IServiceCollection services, string applicationName, Action<TracerProviderBuilder>? configureDatabaseTracing = null)
    {
        services.AddOpenTelemetry()
        .WithTracing(tracing =>
         {
             tracing
             .AddSource(applicationName) // Crucial: Must perfectly match the ActivitySource name above
             .AddAspNetCoreInstrumentation()       // Automatically traces host HTTP calls
             .AddHttpClientInstrumentation()       // Tracks outgoing HTTP dependencies automatically
             .AddOtlpExporter();

             configureDatabaseTracing?.Invoke(tracing);


         })                   // Emits standard format spans directly to an OTel Collector/agent
            .WithLogging(logging => logging
            .AddOtlpExporter());
        return services;
    }
}