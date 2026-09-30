using System.IO.Compression;
using System.Text.Json;
using Novolis.Simulation.SpaceCombat;

namespace Novolis.Lab.SpaceCombat;

public sealed class RuntimeSfx
{
    public string Path { get; set; } = "";
    public string Role { get; set; } = "";
}
