using BuildingBlock.Models;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BuildingBlock.Search;

public sealed class SearchIndexerHostedService<T>(
    IServiceScopeFactory scopeFactory,
    IOptions<KafkaOptions> options,
    ILogger<SearchIndexerHostedService<T>> logger,
    SearchMetrics metrics) : BackgroundService where T : BaseEntity
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var kafka = options.Value;
        if (string.IsNullOrWhiteSpace(kafka.BootstrapServers))
        {
            return;
        }

        var config = new ConsumerConfig
        {
            BootstrapServers = kafka.BootstrapServers,
            GroupId = kafka.GroupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = true
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(kafka.Topic);
        logger.LogInformation("Search indexer subscribed to {Topic}", kafka.Topic);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(stoppingToken);
                if (result?.Message?.Value is null)
                {
                    continue;
                }

                await using var scope = scopeFactory.CreateAsyncScope();
                var indexer = scope.ServiceProvider.GetRequiredService<SearchIndexer<T>>();
                await indexer.ApplyAsync(result.Message.Value, result.Topic, stoppingToken);
                var watermark = consumer.QueryWatermarkOffsets(result.TopicPartition, TimeSpan.FromSeconds(1));
                metrics.SyncLagSeconds = Math.Max(0, watermark.High.Value - result.Offset.Value - 1);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Search sync consumer failed\n{StackTrace}", ex.ToString());
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }
    }
}
