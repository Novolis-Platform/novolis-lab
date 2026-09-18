using System.Numerics;
using Novolis.Math.Geometry;
using Novolis.ThreeD;

namespace CorellianFreighterBuilder;

/// <summary>
/// Procedural YT-1300 interior for the CGTrader exterior (Haynes deck landmarks).
/// Built with ShipYard shells/arrays — not SceneLab UI edit — and kept as its own
/// mesh node so we never boolean against the ~1.5M-vert hull.
/// Scale: 1 unit = 1 m inside the same 34.37 m length envelope as the import.
/// </summary>
internal static class FalconInterior
{
    private const float Width = 25.61f;
    private const float SaucerRadius = Width * 0.5f;
    private const float HullHeight = 3.55f;

    private const float CorridorRadius = 7.6f;
    private const float CorridorWidth = 1.45f;
    private const float CorridorHeight = 1.95f;

    /// <summary>Full interior kit as one EditableMesh (corridor → hold → eng → tube → wells).</summary>
    public static EditableMesh Build()
    {
        EditableMesh? i = null;
        void Add(EditableMesh p) => i = i is null ? p : ShipYard.Union(i, p);

        Add(RingCorridor());
        Add(CockpitPassageInterior());
        Add(MainHold());
        Add(EngineeringBay());
        Add(GunwellShafts());
        Add(AirlockVestibules());
        Add(UtilityRuns());

        return i!;
    }

    /// <summary>Annular main corridor (thin shells + floor grate + hatch frames).</summary>
    public static EditableMesh RingCorridor()
    {
        EditableMesh? ring = null;
        const int segments = 16;
        for (var i = 0; i < segments; i++)
        {
            var ang = i * (MathF.PI * 2f / segments);
            var deg = ang * (180f / MathF.PI);
            // Gap for starboard cockpit tube (~38°)
            if (deg is > 25f and < 55f)
                continue;

            var x = MathF.Sin(ang) * CorridorRadius;
            var z = MathF.Cos(ang) * CorridorRadius;
            var bay = ShipYard.ModuleShell(CorridorWidth, CorridorHeight, 2.85f, 0.04f);
            bay.Transform(ShipYard.Xf(x, 0.05f, z, 0, deg, 0));
            ring = ring is null ? bay : ShipYard.Union(ring, bay);

            var grate = ShipYard.Prim(MeshPrimitiveKind.Box, CorridorWidth * 0.85f, 0.04f, 2.65f,
                ShipYard.Xf(x, -CorridorHeight * 0.42f, z, 0, deg, 0));
            ring = ShipYard.Union(ring, grate);

            // Ceiling conduit rib
            var rib = ShipYard.Prim(MeshPrimitiveKind.Box, CorridorWidth * 0.7f, 0.06f, 0.12f,
                ShipYard.Xf(x, CorridorHeight * 0.38f, z, 0, deg, 0));
            ring = ShipYard.Union(ring, rib);
        }

        foreach (var angDeg in new[] { 0f, 90f, 180f, 270f })
        {
            var ang = angDeg * (MathF.PI / 180f);
            var frame = ShipYard.HangarMouth(1.15f, 1.75f, 12);
            frame.Transform(ShipYard.Xf(
                MathF.Sin(ang) * CorridorRadius, 0.05f, MathF.Cos(ang) * CorridorRadius,
                0, angDeg, 0));
            ring = ShipYard.Union(ring!, frame);
        }

        // Pressure rings along a short radial spur toward hold
        var rings = ShipYard.PressureRings(5, 0.55f, 0.62f, 0.05f, 14);
        rings.Transform(ShipYard.Xf(0, 0.05f, CorridorRadius - 2.2f, 90, 0, 0));
        return ShipYard.Union(ring!, rings);
    }

    /// <summary>Interior of cockpit passage + seats (no exterior blister — hull already has it).</summary>
    public static EditableMesh CockpitPassageInterior()
    {
        const float cockAng = 38f * (MathF.PI / 180f);
        var cockX = MathF.Sin(cockAng) * (SaucerRadius - 0.35f);
        var cockZ = MathF.Cos(cockAng) * (SaucerRadius - 0.35f);
        var cockYaw = cockAng * (180f / MathF.PI) - 90f;

        // Passage tube shell toward ring
        var tube = ShipYard.Corridor(length: 5.8f, width: 1.15f, height: 1.65f, wall: 0.04f);
        tube.Transform(ShipYard.Xf(cockX * 0.55f, 0.1f, cockZ * 0.5f, 0, cockYaw + 8f, 0));

        EditableMesh cock = tube;

        // Compact crew cabin shell just inside the rim (reads in wireframe through hull)
        var cabin = ShipYard.ModuleShell(2.0f, 1.45f, 2.4f, 0.04f);
        cabin.Transform(ShipYard.Xf(cockX, 0.2f, cockZ, 0, cockYaw, 0));
        cock = ShipYard.Union(cock, cabin);

        foreach (var (lx, lz) in new[] { (-0.4f, 0.45f), (0.4f, 0.45f), (-0.4f, -0.3f), (0.4f, -0.3f) })
        {
            var seat = ShipYard.Prim(MeshPrimitiveKind.Cylinder, 0.36f, 0.4f, 0.36f,
                ShipYard.Xf(cockX + lx * 0.65f, -0.2f, cockZ + lz * 0.45f, 0, cockYaw, 0), 10);
            cock = ShipYard.Union(cock, seat);
        }

        // Console blocks
        foreach (var (lx, lz) in new[] { (-0.55f, 0.85f), (0.55f, 0.85f), (0f, 0.95f) })
        {
            var panel = ShipYard.Prim(MeshPrimitiveKind.Box, 0.45f, 0.55f, 0.2f,
                ShipYard.Xf(cockX + lx * 0.5f, 0.15f, cockZ + lz * 0.4f, 0, cockYaw, 0));
            cock = ShipYard.Union(cock, panel);
        }

        var hatch = ShipYard.TorusRing(0.52f, 0.06f,
            ShipYard.Xf(MathF.Sin(cockAng) * CorridorRadius, 0.15f, MathF.Cos(cockAng) * CorridorRadius, 0, 90, 0), 14);
        return ShipYard.Union(cock, hatch);
    }

    /// <summary>Main hold — Dejarik lounge, bunks, refresher (Haynes #13).</summary>
    public static EditableMesh MainHold()
    {
        var hold = ShipYard.ModuleShell(6.4f, 2.1f, 5.6f, 0.05f);
        hold.Transform(ShipYard.Xf(0, 0.05f, -0.8f));

        var table = ShipYard.Prim(MeshPrimitiveKind.Cylinder, 1.25f, 0.5f, 1.25f,
            ShipYard.Xf(0, -0.55f, -0.4f), 16);
        var hologram = ShipYard.Prim(MeshPrimitiveKind.Sphere, 0.5f, 0.4f, 0.5f,
            ShipYard.Xf(0, 0.0f, -0.4f), 12);
        hold = ShipYard.Union(ShipYard.Union(hold, table), hologram);

        var bench = ShipYard.Prim(MeshPrimitiveKind.Box, 2.2f, 0.4f, 0.5f, Matrix4x4.Identity);
        foreach (var (x, z) in new[] { (-2.0f, -2.2f), (2.0f, -2.2f), (-2.0f, 0.9f), (2.0f, 0.9f) })
        {
            var b = bench.Clone();
            b.Transform(ShipYard.Xf(x, -0.65f, z));
            hold = ShipYard.Union(hold, b);
        }

        var bunk = ShipYard.Prim(MeshPrimitiveKind.Capsule, 0.5f, 1.7f, 0.5f, Matrix4x4.Identity, 10);
        foreach (var (x, z) in new[] { (-2.5f, -2.8f), (2.5f, -2.8f), (-2.5f, 1.4f), (2.5f, 1.4f) })
        {
            var bed = bunk.Clone();
            bed.Transform(ShipYard.Xf(x, 0.1f, z, 0, 0, 90));
            hold = ShipYard.Union(hold, bed);
        }

        var refresher = ShipYard.ModuleShell(1.3f, 1.9f, 1.4f, 0.04f);
        refresher.Transform(ShipYard.Xf(-3.2f, 0.05f, -0.2f));
        hold = ShipYard.Union(hold, refresher);

        // Overhead gantry across hold
        var gantry = ShipYard.CargoGantry(5.2f, 0.85f);
        gantry.Transform(ShipYard.Xf(0, 0.75f, -0.8f));
        return ShipYard.Union(hold, gantry);
    }

    /// <summary>Aft engineering (Haynes #1–7) — hyperdrive cores + reactor inside disc.</summary>
    public static EditableMesh EngineeringBay()
    {
        var bay = ShipYard.ModuleShell(7.2f, 2.2f, 6.0f, 0.05f);
        bay.Transform(ShipYard.Xf(0, 0.05f, -7.4f));

        foreach (var x in new[] { -1.9f, 1.9f })
        {
            var hd = ShipYard.Prim(MeshPrimitiveKind.Capsule, 1.15f, 3.0f, 1.15f,
                ShipYard.Xf(x, 0.1f, -8.2f, 90, 0, 0), 14);
            bay = ShipYard.Union(bay, hd);
            bay = ShipYard.Union(bay, ShipYard.TorusRing(0.55f, 0.1f,
                ShipYard.Xf(x, 0.1f, -7.2f, 90, 0, 0), 12));
        }

        var core = ShipYard.Prim(MeshPrimitiveKind.Cylinder, 1.55f, 2.0f, 1.55f,
            ShipYard.Xf(0, 0.15f, -6.2f), 14);
        bay = ShipYard.Union(bay, core);
        bay = ShipYard.Union(bay, ShipYard.TorusRing(0.72f, 0.09f,
            ShipYard.Xf(0, 0.85f, -6.2f, 90, 0, 0), 14));

        foreach (var x in new[] { -3.0f, 3.0f })
            bay = ShipYard.Union(bay, ShipYard.Prim(MeshPrimitiveKind.Box, 1.0f, 1.25f, 0.5f,
                ShipYard.Xf(x, -0.1f, -5.6f)));

        var catwalk = ShipYard.Catwalk(5.5f, 0.7f, 0.4f);
        catwalk.Transform(ShipYard.Xf(0, -0.55f, -7.6f));
        bay = ShipYard.Union(bay, catwalk);

        for (var i = 0; i < 5; i++)
        {
            var px = (i - 2) * 1.25f;
            bay = ShipYard.Union(bay, ShipYard.Prim(MeshPrimitiveKind.Cylinder, 0.75f, 0.1f, 0.75f,
                ShipYard.Xf(px, -HullHeight * 0.42f, -9.6f), 10));
        }

        return bay;
    }

    /// <summary>Hollow gunwell shafts (exterior turrets already on FBX).</summary>
    public static EditableMesh GunwellShafts()
    {
        EditableMesh? g = null;
        foreach (var (x, z) in new[] { (-1.8f, 1.2f), (2.2f, -2.8f) })
        {
            var shaft = ShipYard.ModuleShell(1.15f, HullHeight * 0.9f, 1.15f, 0.04f);
            shaft.Transform(ShipYard.Xf(x, 0, z));
            g = g is null ? shaft : ShipYard.Union(g, shaft);

            var ladder = ShipYard.LadderWell(HullHeight * 0.75f, 0.5f);
            ladder.Transform(ShipYard.Xf(x, 0, z));
            g = ShipYard.Union(g, ladder);
        }

        return g!;
    }

    /// <summary>Port/starboard airlock vestibules on the ring.</summary>
    public static EditableMesh AirlockVestibules()
    {
        EditableMesh? a = null;
        foreach (var (x, z, yaw) in new[]
                 {
                     (-SaucerRadius + 2.4f, -1.2f, 0f),
                     (SaucerRadius - 2.4f, -3.5f, 180f),
                 })
        {
            var vest = ShipYard.ModuleShell(1.6f, 1.85f, 1.8f, 0.04f);
            vest.Transform(ShipYard.Xf(x, 0.05f, z, 0, yaw, 0));
            a = a is null ? vest : ShipYard.Union(a, vest);

            var collar = ShipYard.DockingCollar(0.55f, 2, 12);
            collar.Transform(ShipYard.Xf(x, 0.05f, z, 0, yaw, 90));
            a = ShipYard.Union(a, collar);
        }

        return a!;
    }

    /// <summary>Utility conduits linking hold ↔ engineering under the deck.</summary>
    public static EditableMesh UtilityRuns()
    {
        EditableMesh? u = null;
        void Add(EditableMesh p) => u = u is null ? p : ShipYard.Union(u, p);

        foreach (var x in new[] { -1.1f, 0f, 1.1f })
        {
            var run = ShipYard.ConduitRun(6.5f, 0.07f, 8);
            run.Transform(ShipYard.Xf(x, -0.85f, -4.2f));
            Add(run);
        }

        // Cross manifolds at hold/eng bulkhead
        Add(ShipYard.TorusRing(0.9f, 0.12f, ShipYard.Xf(0, -0.85f, -4.0f, 90, 0, 0), 16));

        return u!;
    }
}
