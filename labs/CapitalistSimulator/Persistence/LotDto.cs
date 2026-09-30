using System.Text.Json;
using System.Text.Json.Serialization;
using CapitalistSimulator.Sim;

namespace CapitalistSimulator.Persistence;

internal sealed class LotDto
{
    public string ProductId { get; set; } = "";
    public decimal Quantity { get; set; }
    public double Quality { get; set; }
    public decimal UnitCost { get; set; }
}
