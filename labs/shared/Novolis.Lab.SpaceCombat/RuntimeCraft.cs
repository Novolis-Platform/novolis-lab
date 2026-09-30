using System.IO.Compression;
using System.Text.Json;
using Novolis.Simulation.SpaceCombat;

namespace Novolis.Lab.SpaceCombat;

public sealed class RuntimeCraft
{
    public string Id { get; set; } = "";
    public string Role { get; set; } = "";
    public string? MeshId { get; set; }
    public float MaxSpeed { get; set; }
    public float Acceleration { get; set; }
    public float TurnRate { get; set; }
    public float HitRadius { get; set; }
    public float Shield { get; set; }
    public float Hull { get; set; }
}
