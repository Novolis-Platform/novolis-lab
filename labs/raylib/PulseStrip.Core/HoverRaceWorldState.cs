namespace PulseStrip.Core;

/// <summary>Mutable world state for a PulseStrip race.</summary>
public sealed class HoverRaceWorldState
{
    public int Tick { get; set; }
    public required IReadOnlyList<HoverCraftState> Craft { get; init; }
    public List<HoverProjectile> Projectiles { get; } = [];
    public required IReadOnlyList<TrackPickup> Pickups { get; init; }
    public int TargetLaps { get; init; } = 3;
    public bool RaceFinished { get; set; }
}
