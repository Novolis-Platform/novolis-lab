using Novolis.Physics.Ballistics;

namespace ArtillerySimulator;

/// <summary>
/// Registry of expandable piece specs. Mission hosts pick one; new vehicles = new factory + register.
/// Ballistics are public open-source ballparks for the educational lab — not manufacturer data.
/// </summary>
internal static class ArtilleryPieceCatalog
{
    private static readonly Dictionary<string, IArtilleryPieceSpec> ById =
        new(StringComparer.OrdinalIgnoreCase);

    static ArtilleryPieceCatalog()
    {
        Register(CreateK9DogEducational());
        Register(CreateOscarsborg28Educational());
        Register(CreateM777Educational());
        Register(CreateHimarsGmlrsEducational());
    }

    /// <summary>Default Kristiansand lab piece (K9 DOG — cheeky educational name).</summary>
    public static IArtilleryPieceSpec Default => Require(ProductIdentity.PieceSpecId);

    public static IArtilleryPieceSpec Require(string id) =>
        ById.TryGetValue(id, out var spec)
            ? spec
            : throw new KeyNotFoundException($"Unknown artillery piece spec '{id}'.");

    public static bool TryGet(string id, out IArtilleryPieceSpec spec) => ById.TryGetValue(id, out spec!);

    public static void Register(IArtilleryPieceSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        if (string.IsNullOrWhiteSpace(spec.Id))
            throw new ArgumentException("Piece spec needs an Id.", nameof(spec));
        ById[spec.Id] = spec;
    }

    /// <summary>
    /// K9 DOG — cheeky educational 155 mm / L52 SP howitzer sandbox piece.
    /// </summary>
    private static IArtilleryPieceSpec CreateK9DogEducational()
    {
        var caliber = 0.155f;
        var projectile = new ProjectileProfile(
            massKg: 47f,
            referenceAreaM2: Math.PI * caliber * caliber / 4.0,
            dragCoefficient: 0.42);

        var errors = new BallisticErrorBudget(
            elevationPeMils: 1.6f,
            deflectionPeMils: 2.2f,
            muzzleVelocityPeFraction: 0.0028f,
            metWindResidualPeFraction: 0.10f,
            envelopePeScale: 2.0f);

        var warhead = new WarheadEffectSpec(
            caughtMeters: 95f,
            pinnedMeters: 200f,
            airburstHeightMeters: 12f,
            scareSeconds: 4.0f);

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

    /// <summary>
    /// Oscarsborg main battery stand-in: public lore around the 28 cm coastal guns that fired
    /// on Blücher (9 Apr 1940). Slow, heavy, short-ish coastal arcs — educational only.
    /// Open ballparks: ~280–283 mm, ~240–305 kg shell class, ~11–15 km practical coastal range,
    /// very low RoF.
    /// </summary>
    private static IArtilleryPieceSpec CreateOscarsborg28Educational()
    {
        var caliber = 0.283f;
        var projectile = new ProjectileProfile(
            massKg: 255f,
            referenceAreaM2: Math.PI * caliber * caliber / 4.0,
            dragCoefficient: 0.48);

        var errors = new BallisticErrorBudget(
            elevationPeMils: 2.4f,
            deflectionPeMils: 3.0f,
            muzzleVelocityPeFraction: 0.004f,
            metWindResidualPeFraction: 0.12f,
            envelopePeScale: 2.2f);

        var warhead = new WarheadEffectSpec(
            caughtMeters: 140f,
            pinnedMeters: 320f,
            airburstHeightMeters: 8f,
            scareSeconds: 6.0f);

        // Single full charge class — historical coastal guns were not modular bag menus.
        IReadOnlyList<ChargeZoneSpec> charges =
        [
            new("full", "full", 680f, 18f),
            new("reduced", "reduced", 520f, 12f),
        ];

        return new ArtilleryPieceSpec(
            id: ProductIdentity.Oscarsborg28PieceSpecId,
            displayName: ProductIdentity.Oscarsborg28DisplayName,
            shortName: ProductIdentity.Oscarsborg28ShortName,
            caliberMeters: caliber,
            barrelLengthMeters: caliber * 35f,
            muzzleRadiusMeters: caliber * 0.5f,
            gunHeightOffsetMeters: 3.2f,
            minElevationDegrees: -3f,
            maxElevationDegrees: 45f,
            maxTraverseDegrees: 120f,
            continuousTraverse: false,
            minEffectiveRangeMeters: 1_800f,
            launchClearanceMeters: 250f,
            projectile: projectile,
            errors: errors,
            warhead: warhead,
            charges: charges,
            loaderRamSeconds: 14f,
            loaderChargeSeconds: 6f,
            loaderChargeSwapSeconds: 10f,
            loaderCloseSeconds: 2.5f,
            elevateMilsPerSecond: 18f,
            traverseMilsPerSecond: 28f);
    }

    /// <summary>
    /// M777 155 mm L39 — public ballparks (US Army/USMC open sources): M795 ~827 m/s,
    /// max range ~22.5–24 km (standard HE), elev about −3°…+71.7°, traverse ±22.5° on carriage.
    /// Softened traverse for island play; charge ladder approximates green/white/red bags.
    /// </summary>
    private static IArtilleryPieceSpec CreateM777Educational()
    {
        var caliber = 0.155f;
        var projectile = new ProjectileProfile(
            massKg: 46.7f,
            referenceAreaM2: Math.PI * caliber * caliber / 4.0,
            dragCoefficient: 0.40);

        var errors = new BallisticErrorBudget(
            elevationPeMils: 1.4f,
            deflectionPeMils: 1.9f,
            muzzleVelocityPeFraction: 0.0025f,
            metWindResidualPeFraction: 0.09f,
            envelopePeScale: 2.0f);

        var warhead = new WarheadEffectSpec(
            caughtMeters: 90f,
            pinnedMeters: 190f,
            airburstHeightMeters: 12f,
            scareSeconds: 4.0f);

        IReadOnlyList<ChargeZoneSpec> charges =
        [
            new("green", "green", 550f, 12f),
            new("white", "white", 700f, 18f),
            new("red", "red", 827f, 24f),
            new("zone1", "zone 1", 420f, 8f),
        ];

        return new ArtilleryPieceSpec(
            id: ProductIdentity.M777PieceSpecId,
            displayName: ProductIdentity.M777DisplayName,
            shortName: ProductIdentity.M777ShortName,
            caliberMeters: caliber,
            barrelLengthMeters: caliber * 39f,
            muzzleRadiusMeters: caliber * 0.5f,
            gunHeightOffsetMeters: 1.9f,
            minElevationDegrees: -3f,
            maxElevationDegrees: 71.7f,
            maxTraverseDegrees: 80f,
            continuousTraverse: false,
            minEffectiveRangeMeters: 2_700f,
            launchClearanceMeters: 300f,
            projectile: projectile,
            errors: errors,
            warhead: warhead,
            charges: charges,
            loaderRamSeconds: 3.4f,
            loaderChargeSeconds: 1.7f,
            loaderChargeSwapSeconds: 4.8f,
            loaderCloseSeconds: 0.65f,
            elevateMilsPerSecond: 55f,
            traverseMilsPerSecond: 70f);
    }

    /// <summary>
    /// M142 HIMARS + GMLRS M31 unitary — public ballparks: 227 mm rocket, 6 per pod,
    /// unitary HE ~70–90 kg class, range band often quoted ~15–70+ km (GMLRS / GMLRS+).
    /// Lab model: high loft, long min range, 6-rocket ripple sheaf for a small carpet.
    /// </summary>
    private static IArtilleryPieceSpec CreateHimarsGmlrsEducational()
    {
        var caliber = 0.227f;
        var projectile = new ProjectileProfile(
            massKg: 90f,
            referenceAreaM2: Math.PI * caliber * caliber / 4.0,
            dragCoefficient: 0.22);

        var errors = new BallisticErrorBudget(
            elevationPeMils: 1.0f,
            deflectionPeMils: 1.2f,
            muzzleVelocityPeFraction: 0.002f,
            metWindResidualPeFraction: 0.07f,
            envelopePeScale: 1.8f);

        // Unitary effects enlarged slightly so a 6-pack sheaf reads as a carpet in the Sound.
        var warhead = new WarheadEffectSpec(
            caughtMeters: 160f,
            pinnedMeters: 380f,
            airburstHeightMeters: 18f,
            scareSeconds: 5.0f);

        // Two "pods": tactical (~45 km class MV stand-in) and extended.
        IReadOnlyList<ChargeZoneSpec> charges =
        [
            new("gmlrs", "GMLRS", 980f, 30f),
            new("gmlrs+", "GMLRS+", 1_120f, 36f),
        ];

        return new ArtilleryPieceSpec(
            id: ProductIdentity.HimarsPieceSpecId,
            displayName: ProductIdentity.HimarsDisplayName,
            shortName: ProductIdentity.HimarsShortName,
            caliberMeters: caliber,
            barrelLengthMeters: 4.0f,
            muzzleRadiusMeters: caliber * 0.55f,
            gunHeightOffsetMeters: 2.8f,
            minElevationDegrees: 15f,
            maxElevationDegrees: 65f,
            maxTraverseDegrees: 180f,
            continuousTraverse: true,
            minEffectiveRangeMeters: 8_000f,
            launchClearanceMeters: 600f,
            projectile: projectile,
            errors: errors,
            warhead: warhead,
            charges: charges,
            loaderRamSeconds: 18f,
            loaderChargeSeconds: 4f,
            loaderChargeSwapSeconds: 22f,
            loaderCloseSeconds: 1.2f,
            elevateMilsPerSecond: 40f,
            traverseMilsPerSecond: 90f,
            salvoRoundCount: 6,
            salvoSpreadMeters: 220f);
    }
}
