using System.Text.Json;
using System.Text.Json.Serialization;
using CapitalistSimulator.Sim;

namespace CapitalistSimulator.Persistence;

internal sealed class TileDto
{
    public int X { get; set; }
    public int Y { get; set; }
    public TileKind Kind { get; set; }
    public decimal LandCost { get; set; }
    public Guid? FirmId { get; set; }
}
