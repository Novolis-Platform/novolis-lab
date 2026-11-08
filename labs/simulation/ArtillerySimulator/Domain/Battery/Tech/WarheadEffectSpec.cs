namespace ArtillerySimulator;

/// <summary>
/// Soft-kill / suppress radii used by the defense mission. Swap per ammunition family when expanding.
/// Explicitly a game/effect model — not a fuze or fragment table.
/// </summary>
internal sealed class WarheadEffectSpec
{
    public WarheadEffectSpec(
        float caughtMeters,
        float pinnedMeters,
        float airburstHeightMeters = 0f,
        float scareSeconds = 5f)
    {
        CaughtMeters = caughtMeters;
        PinnedMeters = pinnedMeters;
        AirburstHeightMeters = MathF.Max(0f, airburstHeightMeters);
        ScareSeconds = MathF.Max(0.5f, scareSeconds);
    }

    /// <summary>Horizontal radius that puts a vehicle out of action.</summary>
    public float CaughtMeters { get; }

    /// <summary>Horizontal radius that suppresses / slows a vehicle.</summary>
    public float PinnedMeters { get; }

    /// <summary>
    /// Detonation height AGL for educational airburst HE (0 = point-detonating ground burst).
    /// Improves soft-skin coverage without claiming a real fuze table.
    /// </summary>
    public float AirburstHeightMeters { get; }

    /// <summary>How long a near-miss keeps a column suppressed.</summary>
    public float ScareSeconds { get; }

    public bool IsAirburst => AirburstHeightMeters > 0.5f;
}
