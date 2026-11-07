using Novolis.Physics.Ballistics;

namespace ArtillerySimulator;

/// <summary>
/// Registry of expandable piece specs. Mission hosts pick one; new vehicles = new factory + register.
/// </summary>
internal static class ArtilleryPieceCatalog
{
    private static readonly Dictionary<string, IArtilleryPieceSpec> ById =
        new(StringComparer.OrdinalIgnoreCase);

    static ArtilleryPieceCatalog()
    {
        Register(CreateK9DogEducational());
    }

    /// <summary>Default Kristiansand lab piece (K9 DOG — cheeky educational name).</summary>
    public static IArtilleryPieceSpec Default => Require(ProductIdentity.PieceSpecId);

    public static IArtilleryPieceSpec K9DogEducational => Default;

    public static IArtilleryPieceSpec Require(string id) =>
        ById.TryGetValue(id, out var spec)
            ? spec
            : throw new KeyNotFoundException($"Unknown artillery piece spec '{id}'.");

    public static bool TryGet(string id, out IArtilleryPieceSpec spec) => ById.TryGetValue(id, out spec!);

    public static IEnumerable<IArtilleryPieceSpec> All => ById.Values;

    public static void Register(IArtilleryPieceSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        if (string.IsNullOrWhiteSpace(spec.Id))
            throw new ArgumentException("Piece spec needs an Id.", nameof(spec));
        ById[spec.Id] = spec;
    }

    /// <summary>
    /// K9 DOG — cheeky educational 155 mm / L52 SP howitzer sandbox piece.
    /// Not a manufacturer product. Charge MV and PE are open approximations for the lab —
    /// expand by registering another id rather than editing callers.
    /// </summary>
    public static IArtilleryPieceSpec CreateK9DogEducational()
    {
        var caliber = 0.155f;
        var projectile = new ProjectileProfile(
            massKg: 47f,
            referenceAreaM2: Math.PI * caliber * caliber / 4.0,
            dragCoefficient: 0.42);

        // PE: modern digital SP order-of-magnitude (educational). Envelope draws ≈2× PE.
        var errors = new BallisticErrorBudget(
            elevationPeMils: 1.6f,
            deflectionPeMils: 2.2f,
            muzzleVelocityPeFraction: 0.0028f,
            metWindResidualPeFraction: 0.10f,
            envelopePeScale: 2.0f);

        var warhead = new WarheadEffectSpec(caughtMeters: 200f, pinnedMeters: 340f);

        // Ordered reduced / standard / max / low — same ordinals the UI and agent already use.
        IReadOnlyList<ChargeZoneSpec> charges =
        [
            new("reduced", "reduced", 420f, 10f),
            new("standard", "standard", 560f, 16f),
            new("max", "max", 720f, 24f),
            new("low", "low", 360f, 8f),
        ];

        return new ArtilleryPieceSpec(
            id: ProductIdentity.PieceSpecId,
            displayName: ProductIdentity.PieceDisplayName,
            shortName: ProductIdentity.ShortPieceName,
            caliberMeters: caliber,
            barrelLengthMeters: caliber * 52f,
            muzzleRadiusMeters: caliber * 0.5f,
            gunHeightOffsetMeters: 2.4f,
            minElevationDegrees: -2.5f,
            maxElevationDegrees: 70f,
            maxTraverseDegrees: 180f,
            continuousTraverse: true,
            minEffectiveRangeMeters: 4_000f,
            launchClearanceMeters: 400f,
            projectile: projectile,
            errors: errors,
            warhead: warhead,
            charges: charges);
    }
}
