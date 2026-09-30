using System.Text.Json;
using System.Text.Json.Serialization;
using CapitalistSimulator.Sim;

namespace CapitalistSimulator.Persistence;

internal sealed class CityDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public int Width { get; set; }
    public int Height { get; set; }
    public double SpendingLevel { get; set; }
    public double SalaryLevel { get; set; }
    public EconomicClimate Climate { get; set; }
    public decimal Population { get; set; }
    public List<TileDto> Tiles { get; set; } = [];
}
