using Novolis.Physics.Ballistics;

namespace ArtillerySimulator;

/// <summary>
/// Expandable mil-tech description of one artillery piece + ammunition family.
/// Add a new sealed implementation and register it in <see cref="ArtilleryPieceCatalog"/> —
/// do not fork host constants.
/// </summary>
internal interface IArtilleryPieceSpec
{
    string Id { get; }
    string DisplayName { get; }
    string ShortName { get; }

    float CaliberMeters { get; }
    float BarrelLengthMeters { get; }
    float MuzzleRadiusMeters { get; }
    float GunHeightOffsetMeters { get; }

    float MinElevationDegrees { get; }
    float MaxElevationDegrees { get; }
    float MaxTraverseDegrees { get; }
    bool ContinuousTraverse { get; }

    float MinEffectiveRangeMeters { get; }
    float LaunchClearanceMeters { get; }

    /// <summary>Autoloader / breech cycle (battle clock).</summary>
    float LoaderRamSeconds { get; }
    float LoaderChargeSeconds { get; }
    float LoaderChargeSwapSeconds { get; }
    float LoaderCloseSeconds { get; }

    /// <summary>Powered traverse / elevation rates (NATO mils per battle-clock second).</summary>
    float ElevateMilsPerSecond { get; }
    float TraverseMilsPerSecond { get; }

    /// <summary>HIMARS-style ripple: rockets per FIRE (1 = single tube).</summary>
    int SalvoRoundCount { get; }

    /// <summary>Horizontal sheaf radius for salvo impacts (meters).</summary>
    float SalvoSpreadMeters { get; }

    ProjectileProfile Projectile { get; }
    BallisticErrorBudget Errors { get; }
    WarheadEffectSpec Warhead { get; }
    IReadOnlyList<ChargeZoneSpec> Charges { get; }

    ChargeZoneSpec ChargeAt(int index)
    {
        if (Charges.Count == 0)
            throw new InvalidOperationException($"{Id} has no charge zones.");
        return Charges[Math.Clamp(index, 0, Charges.Count - 1)];
    }
}
