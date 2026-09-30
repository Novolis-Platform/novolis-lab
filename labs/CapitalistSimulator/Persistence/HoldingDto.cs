using System.Text.Json;
using System.Text.Json.Serialization;
using CapitalistSimulator.Sim;

namespace CapitalistSimulator.Persistence;

internal sealed class HoldingDto
{
    public Guid Owner { get; set; }
    public Guid Issuer { get; set; }
    public decimal Shares { get; set; }
}
