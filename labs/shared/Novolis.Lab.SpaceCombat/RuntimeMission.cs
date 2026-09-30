using System.IO.Compression;
using System.Text.Json;
using Novolis.Simulation.SpaceCombat;

namespace Novolis.Lab.SpaceCombat;

public sealed class RuntimeMission
{
    public string Id { get; set; } = "";
    public int UnlockIndex { get; set; }
    public string FreighterCraftId { get; set; } = "";
    public string FighterCraftId { get; set; } = "";
    public string HostileCraftId { get; set; } = "";
    public int HostileCount { get; set; }
    public float ProtectSeconds { get; set; }
    public int DestroyRequired { get; set; }
}
