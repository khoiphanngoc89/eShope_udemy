using BuildingBlock.Search;

namespace eShop.Catalog.Api.Search;

internal static class CatalogSearchMetrics
{
    public const string MeterName = "eShop.Catalog.Search";

    internal static readonly SearchMetrics Instance = new(MeterName, "catalog.search");

    public static long SyncLagSeconds
    {
        get => Instance.SyncLagSeconds;
        set => Instance.SyncLagSeconds = value;
    }

    public static void RecordDuration(double milliseconds) => Instance.RecordDuration(milliseconds);

    public static void RecordError() => Instance.RecordError();
}
