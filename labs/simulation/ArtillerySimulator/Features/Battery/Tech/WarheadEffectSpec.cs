namespace ArtillerySimulator;

/// <summary>
/// Soft-kill / suppress radii used by the defense mission. Swap per ammunition family when expanding.
/// Explicitly a game/effect model — not a fuze or fragment table.
/// </summary>
internal sealed class WarheadEffectSpec
{
    public WarheadEffectSpec(float caughtMeters, float pinnedMeters)
    {
        CaughtMeters = caughtMeters;
        PinnedMeters = pinnedMeters;
    }

    public float CaughtMeters { get; }
    public float PinnedMeters { get; }
}
