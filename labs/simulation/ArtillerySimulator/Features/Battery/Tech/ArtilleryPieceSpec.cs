using Novolis.Physics.Ballistics;

namespace ArtillerySimulator;

/// <summary>Immutable bag implementation of <see cref="IArtilleryPieceSpec"/> for catalog entries.</summary>
internal sealed class ArtilleryPieceSpec : IArtilleryPieceSpec
{
    public ArtilleryPieceSpec(
        string id,
        string displayName,
        string shortName,
        float caliberMeters,
        float barrelLengthMeters,
        float muzzleRadiusMeters,
        float gunHeightOffsetMeters,
        float minElevationDegrees,
        float maxElevationDegrees,
        float maxTraverseDegrees,
        bool continuousTraverse,
        float minEffectiveRangeMeters,
        float launchClearanceMeters,
        ProjectileProfile projectile,
        BallisticErrorBudget errors,
        WarheadEffectSpec warhead,
        IReadOnlyList<ChargeZoneSpec> charges)
    {
        Id = id;
        DisplayName = displayName;
        ShortName = shortName;
        CaliberMeters = caliberMeters;
        BarrelLengthMeters = barrelLengthMeters;
        MuzzleRadiusMeters = muzzleRadiusMeters;
        GunHeightOffsetMeters = gunHeightOffsetMeters;
        MinElevationDegrees = minElevationDegrees;
        MaxElevationDegrees = maxElevationDegrees;
        MaxTraverseDegrees = maxTraverseDegrees;
        ContinuousTraverse = continuousTraverse;
        MinEffectiveRangeMeters = minEffectiveRangeMeters;
        LaunchClearanceMeters = launchClearanceMeters;
        Projectile = projectile;
        Errors = errors;
        Warhead = warhead;
        Charges = charges;
    }

    public string Id { get; }
    public string DisplayName { get; }
    public string ShortName { get; }
    public float CaliberMeters { get; }
    public float BarrelLengthMeters { get; }
    public float MuzzleRadiusMeters { get; }
    public float GunHeightOffsetMeters { get; }
    public float MinElevationDegrees { get; }
    public float MaxElevationDegrees { get; }
    public float MaxTraverseDegrees { get; }
    public bool ContinuousTraverse { get; }
    public float MinEffectiveRangeMeters { get; }
    public float LaunchClearanceMeters { get; }
    public ProjectileProfile Projectile { get; }
    public BallisticErrorBudget Errors { get; }
    public WarheadEffectSpec Warhead { get; }
    public IReadOnlyList<ChargeZoneSpec> Charges { get; }
}
