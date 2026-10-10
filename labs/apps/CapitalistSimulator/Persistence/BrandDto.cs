using System.Text.Json;
using System.Text.Json.Serialization;
using CapitalistSimulator.Sim;

namespace CapitalistSimulator.Persistence;

internal sealed class BrandDto
{
    public double Awareness { get; set; }
    public double Loyalty { get; set; }
}
