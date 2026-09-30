using System.IO.Compression;
using System.Text.Json;
using Novolis.Simulation.SpaceCombat;

namespace Novolis.Lab.SpaceCombat;

public sealed class RuntimeManifest
{
    public Dictionary<string, string> Meshes { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, RuntimeCraft> Craft { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, string> Roles { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, RuntimeSfx> Sfx { get; set; } = new(StringComparer.Ordinal);
    public List<RuntimeMission> Missions { get; set; } = [];
}
