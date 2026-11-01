namespace BuildingBlock.Search;

public sealed class ElasticsearchOptions
{
    public const string SectionName = "Elasticsearch";

    public string Uri { get; set; } = string.Empty;
}
