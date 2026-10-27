namespace eShop.Catalog.Api.Options;

public sealed class DatabaseOptions
{
    public const string SectionName = "ConnectionStrings";

    public string CatalogDb { get; set; } = string.Empty;
}
