using System.Text.Json;
using System.Text.Json.Serialization;
using CapitalistSimulator.Sim;

namespace CapitalistSimulator.Persistence;

internal sealed class FirmDto
{
    public Guid Id { get; set; }
    public Guid Owner { get; set; }
    public Guid CityId { get; set; }
    public string FirmTypeId { get; set; } = "";
    public FirmKind Kind { get; set; }
    public string Name { get; set; } = "";
    public int TileX { get; set; }
    public int TileY { get; set; }
    public int LayoutW { get; set; }
    public int LayoutH { get; set; }
    public RetailFamily? RetailFamily { get; set; }
    public ExtractKind ExtractKind { get; set; }
    public int FactorySize { get; set; }
    public decimal MonthlyExpense { get; set; }
    public bool AutoApplyRd { get; set; }
    public List<UnitDto> Units { get; set; } = [];
    public List<LinkDto> Links { get; set; } = [];
    public List<LotDto> Inventory { get; set; } = [];
}
