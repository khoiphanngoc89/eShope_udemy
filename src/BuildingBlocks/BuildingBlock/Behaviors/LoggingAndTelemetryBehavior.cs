using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace BuildingBlock.Behaviors;

public sealed class LoggingAndTelemetryBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<LoggingAndTelemetryBehavior<TRequest, TResponse>> _logger;
    private static readonly ActivitySource ActivitySource = new("eShop.Application");

    public LoggingAndTelemetryBehavior(ILogger<LoggingAndTelemetryBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;

        using Activity? activity = ActivitySource.StartActivity($"MediatR {requestName}");
        activity?.SetTag("mediatr.request_type", requestName);

        _logger.LogInformation("Handling request: {RequestName}...", requestName);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var response = await next(cancellationToken);
            stopwatch.Stop();

            _logger.LogInformation("Handled {RequestName} successfully in {ElapsedMs}ms...DONE", requestName, stopwatch.ElapsedMilliseconds);
            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);

            _logger.LogError(
                ex,
                "Request {RequestName} failed after {ElapsedMs}ms\n{StackTrace}",
                requestName,
                stopwatch.ElapsedMilliseconds,
                ex.ToString());
            throw;
        }
    }
}
