using BuildingBlock.Models;
using BuildingBlock.Search;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace BuildingBlock.Extensions;

public static class SearchServiceCollectionExtensions
{
    public static IServiceCollection AddSearchIndex<T>(
        this IServiceCollection services,
        IConfiguration configuration,
        string indexName)
        where T : BaseEntity
    {
        services.AddOptions<ElasticsearchOptions>()
            .Bind(configuration.GetSection(ElasticsearchOptions.SectionName));
        services.AddOptions<KafkaOptions>()
            .Bind(configuration.GetSection(KafkaOptions.SectionName));
        services.TryAddSingleton(new SearchMetrics(SearchMetrics.SharedMeterName, "search"));
        services.AddSingleton<ISearchStore<T>>(provider =>
        {
            var elasticsearch = provider.GetRequiredService<IOptions<ElasticsearchOptions>>().Value;
            return string.IsNullOrWhiteSpace(elasticsearch.Uri)
                ? new UnavailableSearchStore<T>()
                : new ElasticsearchSearchStore<T>(elasticsearch, indexName);
        });
        services.AddSingleton<SearchIndexer<T>>();
        var kafka = configuration.GetSection(KafkaOptions.SectionName).Get<KafkaOptions>();
        if (!string.IsNullOrWhiteSpace(kafka?.BootstrapServers))
        {
            services.AddHostedService<SearchIndexerHostedService<T>>();
        }

        return services;
    }
}
