using System.Text.Json.Serialization;
using BuildingBlock.Models;

namespace eShop.Catalog.Api.Models;

public class Product : BaseEntity
{
    public string Name { get; set; } = default!;
    public int Amount { get; set; }
    public decimal SewingCost { get; set; }
    public decimal FabricCostAccessoriesCost { get; set; }
    public decimal PackagingCost { get; set; }
    public decimal CostPrice { get; set; }
    [JsonIgnore]
    public decimal TotalPrice => CostPrice;
    public string Description { get; set; } = default!;
    public string ImageFile { get; set; } = default!;
    public List<string> Categories { get; set; } = new List<string>();
}
