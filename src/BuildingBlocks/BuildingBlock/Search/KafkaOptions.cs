namespace BuildingBlock.Search;

public sealed class KafkaOptions
{
    public const string SectionName = "Kafka";

    public string BootstrapServers { get; set; } = string.Empty;

    public string GroupId { get; set; } = string.Empty;

    public string Topic { get; set; } = string.Empty;
}
