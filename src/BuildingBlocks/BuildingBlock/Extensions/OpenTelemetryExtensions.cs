using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace BuildingBlock.Extensions;

public static class OpenTelemetryExtensions
{
    public static IServiceCollection AddAppOpenTelemetry(
        this IServiceCollection services,
        string applicationName,
        Action<TracerProviderBuilder>? configureTracing = null,
        Action<MeterProviderBuilder>? configureMetrics = null)
    {
        services.AddOpenTelemetry()
            .WithTracing(tracing =>
            {
                tracing
                    .AddSource(applicationName)
                    .AddSource("eShop.Application")
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddOtlpExporter();

                configureTracing?.Invoke(tracing);
            })
            .WithLogging(logging => logging.AddOtlpExporter())
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation();
                configureMetrics?.Invoke(metrics);
                metrics.AddOtlpExporter();
            });
        return services;
    }
}
