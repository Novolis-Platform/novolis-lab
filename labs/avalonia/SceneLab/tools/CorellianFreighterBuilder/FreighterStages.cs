using System.Numerics;
using Novolis.Math.Geometry;
using Novolis.ThreeD;

namespace CorellianFreighterBuilder;

/// <summary>
/// Screen-accurate YT-1300f homage (OT Millennium Falcon silhouette).
/// Scale: 1 unit = 1 meter. Specs from Haynes Owner's Workshop Manual:
///   Length 34.37 m · Width 25.61 m · Height 8.27 m (incl. dish + ventral gun).
/// Exterior reads first (flat saucer, forward mandibles, starboard cockpit tube,
/// rim engines, dorsal rectenna). Interior is thin shells inside the disc.
/// Homage mesh — not a licensed asset.
/// </summary>
internal static class FreighterStages
{
    // Haynes / published envelope
    private const float Length = 34.37f;
    private const float Width = 25.61f;
    private const float HeightEnvelope = 8.27f;
    private const float SaucerRadius = Width * 0.5f;          // 12.805
    private const float MandibleProjection = Length - Width;  // ~8.76 past forward rim
    private const float MandibleTipZ = SaucerRadius + MandibleProjection; // ~21.57
    private const float AftZ = -SaucerRadius;                 // engines flush with aft rim

    // Hull pancake (Johnston: identical upper/lower dishes) — flat, not a ball
    private const float HullHeight = 3.55f;
    private const float RimLip = 0.42f;

    // Mandible plan (top-view OT proportions)
    private const float MandibleOuterX = 4.55f;   // centerline of each fork
    private const float MandibleWidth = 2.85f;
    private const float MandibleHeight = 2.15f;
    private const float MandibleGap = 3.35f;      // clear freight tunnel between forks

    // Ring corridor inside saucer
    private const float CorridorRadius = 7.6f;
    private const float CorridorWidth = 1.45f;
    private const float CorridorHeight = 1.95f;

    public static void BuildAll(string stageDir, string finalPath)
    {
        Directory.CreateDirectory(stageDir);
        EditableMesh ship;

        ship = SaucerExterior();
        Write(stageDir, 1, "saucer-exterior", ship);

        ship = ShipYard.Union(ship, Mandibles());
        Write(stageDir, 2, "mandibles", ship);

        ship = ShipYard.Union(ship, CockpitAndTube());
        Write(stageDir, 3, "cockpit-tube", ship);

        ship = ShipYard.Union(ship, MainCorridorRing());
        Write(stageDir, 4, "main-corridor", ship);

        ship = ShipYard.Union(ship, MainHold());
        Write(stageDir, 5, "main-hold", ship);

        ship = ShipYard.Union(ship, EngineeringBay());
        Write(stageDir, 6, "engineering", ship);

        ship = ShipYard.Union(ship, GunwellsAndTurrets());
        Write(stageDir, 7, "gunwells", ship);

        ship = ShipYard.Union(ship, SensorsAndAccess());
        ship = ShipYard.Union(ship, SublightEngines());
        ship = ShipYard.Union(ship, LandingGearAndRamp());
        ship = ShipYard.Union(ship, ArmorGreebles());
        Write(stageDir, 8, "finish", ship);

        File.Copy(Path.Combine(stageDir, "freighter-stage-08.nov3djson"), finalPath, overwrite: true);
        Console.WriteLine(
            $"FINAL|{finalPath}|verts={ship.VertexCount}|tris={ship.TriangleCount}|envelope={Length:0.##}x{Width:0.##}x{HeightEnvelope:0.##}");
    }

    /// <summary>Flat circular saucer — Johnston pancake, stepped rim, no fat torus.</summary>
    private static EditableMesh SaucerExterior()
    {
        EditableMesh? h = null;
        void Add(EditableMesh p) => h = h is null ? p : ShipYard.Union(h, p);

        // Core disc volume
        Add(ShipYard.Prim(MeshPrimitiveKind.Cylinder,
            Width, HullHeight * 0.72f, Width, ShipYard.Xf(0, 0, 0), 48));

        // Upper / lower dish faces (slightly larger → rim shelf)
        Add(ShipYard.Prim(MeshPrimitiveKind.Cylinder,
            Width * 1.01f, HullHeight * 0.22f, Width * 1.01f,
            ShipYard.Xf(0, HullHeight * 0.28f, 0), 48));
        Add(ShipYard.Prim(MeshPrimitiveKind.Cylinder,
            Width * 1.01f, HullHeight * 0.22f, Width * 1.01f,
            ShipYard.Xf(0, -HullHeight * 0.28f, 0), 48));

        // Armor lip rings (thin tube — TorusRing maps major/tube correctly)
        Add(ShipYard.TorusRing(SaucerRadius - 0.12f, RimLip * 0.55f,
            ShipYard.Xf(0, 0.02f, 0, 90, 0, 0), 48));
        Add(ShipYard.TorusRing(SaucerRadius * 0.92f, 0.09f,
            ShipYard.Xf(0, HullHeight * 0.32f, 0, 90, 0, 0), 40));
        Add(ShipYard.TorusRing(SaucerRadius * 0.92f, 0.09f,
            ShipYard.Xf(0, -HullHeight * 0.32f, 0, 90, 0, 0), 40));

        // Flat deck circles (wireframe silhouette of the pancake)
        Add(ShipYard.Prim(MeshPrimitiveKind.Disc,
            Width * 0.96f, 0.02f, Width * 0.96f,
            ShipYard.Xf(0, HullHeight * 0.38f, 0), 40));
        Add(ShipYard.Prim(MeshPrimitiveKind.Disc,
            Width * 0.96f, 0.02f, Width * 0.96f,
            ShipYard.Xf(0, -HullHeight * 0.38f, 0, 180, 0, 0), 40));

        // Forward “bite” / freight throat fairing where mandibles leave the disc
        Add(ShipYard.Prim(MeshPrimitiveKind.Box,
            MandibleGap + MandibleWidth * 0.35f, HullHeight * 0.85f, 2.4f,
            ShipYard.Xf(0, 0, SaucerRadius - 1.1f)));

        // Circumferential stepped armor plates (Johnston “cutaway rim” look)
        for (var i = 0; i < 16; i++)
        {
            var ang = i * (MathF.PI * 2f / 16f) + 0.12f;
            // Leave forward sector open for mandible roots
            if (ang is > -0.55f and < 0.55f || ang > MathF.PI * 2f - 0.55f)
                continue;
            var x = MathF.Sin(ang) * (SaucerRadius - 0.55f);
            var z = MathF.Cos(ang) * (SaucerRadius - 0.55f);
            var yaw = ang * (180f / MathF.PI);
            Add(ShipYard.Prim(MeshPrimitiveKind.Box, 2.1f, HullHeight * 0.55f, 0.55f,
                ShipYard.Xf(x, 0, z, 0, yaw, 0)));
        }

        return h!;
    }

    /// <summary>Forward freight mandibles — rectangular forks + loading arms (Haynes #21/#24).</summary>
    private static EditableMesh Mandibles()
    {
        EditableMesh? m = null;
        void Add(EditableMesh p) => m = m is null ? p : ShipYard.Union(m, p);

        var forkLength = MandibleProjection + 2.8f; // root inside rim → tip
        var forkCenterZ = SaucerRadius + MandibleProjection * 0.5f - 0.4f;

        foreach (var side in new[] { -1f, 1f })
        {
            var x = side * MandibleOuterX;

            // Main fork body (hard rectangular — reads as OT mandible in wireframe)
            Add(ShipYard.Prim(MeshPrimitiveKind.Box,
                MandibleWidth, MandibleHeight, forkLength,
                ShipYard.Xf(x, 0.05f, forkCenterZ)));

            // Upper / lower armor lips
            Add(ShipYard.Prim(MeshPrimitiveKind.Box,
                MandibleWidth * 1.08f, 0.28f, forkLength * 0.98f,
                ShipYard.Xf(x, MandibleHeight * 0.48f, forkCenterZ)));
            Add(ShipYard.Prim(MeshPrimitiveKind.Box,
                MandibleWidth * 1.08f, 0.28f, forkLength * 0.98f,
                ShipYard.Xf(x, -MandibleHeight * 0.48f, forkCenterZ)));

            // Inner rail (narrower twin prong)
            Add(ShipYard.Prim(MeshPrimitiveKind.Box,
                0.55f, MandibleHeight * 0.72f, forkLength * 0.92f,
                ShipYard.Xf(side * (MandibleGap * 0.5f + 0.35f), 0.05f, forkCenterZ)));

            // Freight-loading arm (Haynes #24)
            Add(ShipYard.Prim(MeshPrimitiveKind.Capsule, 0.28f, 2.8f, 0.28f,
                ShipYard.Xf(x, -0.35f, MandibleTipZ - 1.6f, 90, 0, 0), 10));
            Add(ShipYard.Prim(MeshPrimitiveKind.Box, 0.75f, 0.22f, 0.5f,
                ShipYard.Xf(x, -0.35f, MandibleTipZ - 0.35f)));

            // Tip floodlight / passive sensor (Haynes #22/#25)
            Add(ShipYard.Prim(MeshPrimitiveKind.Cylinder, 0.7f, 0.35f, 0.7f,
                ShipYard.Xf(x, 0.15f, MandibleTipZ - 0.15f, 90, 0, 0), 12));

            // Mandible exterior access hatch (Haynes #21)
            Add(ShipYard.Prim(MeshPrimitiveKind.Cylinder, 0.55f, 0.08f, 0.55f,
                ShipYard.Xf(x, MandibleHeight * 0.52f, SaucerRadius + 2.2f), 10));
        }

        // Cross-brace / freight throat roof between forks
        Add(ShipYard.Prim(MeshPrimitiveKind.Box,
            MandibleGap + 0.4f, 0.35f, 3.2f,
            ShipYard.Xf(0, MandibleHeight * 0.35f, SaucerRadius + 1.4f)));
        Add(ShipYard.Prim(MeshPrimitiveKind.Box,
            MandibleGap + 0.4f, 0.25f, 2.4f,
            ShipYard.Xf(0, -MandibleHeight * 0.25f, SaucerRadius + 1.0f)));

        // Concussion missile tubes in mandible roots (Haynes #17)
        foreach (var side in new[] { -1f, 1f })
        {
            Add(ShipYard.Prim(MeshPrimitiveKind.Cylinder, 0.45f, 2.2f, 0.45f,
                ShipYard.Xf(side * 2.6f, -0.55f, SaucerRadius + 0.6f, 90, 0, 0), 10));
        }

        return m!;
    }

    /// <summary>Starboard offset cockpit + passage tube (Haynes #10–11) — OT side blister.</summary>
    private static EditableMesh CockpitAndTube()
    {
        // Cockpit sits on starboard rim ~35° forward of pure +X (classic 1–2 o'clock)
        const float cockAng = 38f * (MathF.PI / 180f);
        var cockX = MathF.Sin(cockAng) * (SaucerRadius + 0.15f);
        var cockZ = MathF.Cos(cockAng) * (SaucerRadius + 0.15f);
        var cockYaw = cockAng * (180f / MathF.PI) - 90f; // face roughly outward/forward

        // Compact hard-shell blister (not a soft blob)
        var cockpit = ShipYard.ModuleShell(2.15f, 1.55f, 2.85f, 0.05f);
        cockpit.Transform(ShipYard.Xf(cockX, 0.25f, cockZ, 0, cockYaw, 0));

        // Domed canopy
        var canopy = ShipYard.Prim(MeshPrimitiveKind.Sphere, 1.65f, 1.05f, 1.85f,
            ShipYard.Xf(cockX, 0.85f, cockZ, 0, cockYaw, 0), 14);
        EditableMesh cock = ShipYard.Union(cockpit, canopy);

        // Viewport rings on canopy
        foreach (var (dx, dy, dz) in new[]
                 {
                     (0.55f, 0.25f, 0.95f), (-0.15f, 0.25f, 1.05f), (0.2f, 0.45f, 0.9f),
                     (0.55f, -0.05f, 0.9f), (-0.1f, -0.05f, 1.0f),
                 })
        {
            var win = ShipYard.ViewportRing(0.28f, 12);
            // Offset in cockpit-local then place — approximate with world nudge along rim tangent
            win.Transform(ShipYard.Xf(cockX + dx * 0.85f, 0.55f + dy, cockZ + dz * 0.55f, 0, cockYaw, 0));
            cock = ShipYard.Union(cock, win);
        }

        // Four seats
        foreach (var (lx, lz) in new[] { (-0.4f, 0.45f), (0.4f, 0.45f), (-0.4f, -0.3f), (0.4f, -0.3f) })
        {
            var seat = ShipYard.Prim(MeshPrimitiveKind.Cylinder, 0.38f, 0.45f, 0.38f,
                ShipYard.Xf(cockX + lx * 0.7f, -0.15f, cockZ + lz * 0.5f, 0, cockYaw, 0), 10);
            cock = ShipYard.Union(cock, seat);
        }

        // Passage tube (Haynes #10) — cylinder from blister into ring corridor
        var tubeLen = 6.8f;
        var midX = cockX * 0.55f;
        var midZ = cockZ * 0.45f;
        var tube = ShipYard.Prim(MeshPrimitiveKind.Cylinder, 1.25f, tubeLen, 1.25f,
            Matrix4x4.Identity, 16);
        // Cylinder is Y-up — lay along XZ toward center
        tube.Transform(ShipYard.Xf(midX, 0.15f, midZ, 0, cockYaw + 8f, 90));
        cock = ShipYard.Union(cock, tube);

        // Junction hatch into ring
        var hatch = ShipYard.TorusRing(0.55f, 0.07f,
            ShipYard.Xf(MathF.Sin(cockAng) * CorridorRadius,
                0.15f,
                MathF.Cos(cockAng) * CorridorRadius,
                0, 90, 0), 14);
        cock = ShipYard.Union(cock, hatch);

        return cock;
    }

    /// <summary>Annular main corridor inside the saucer (YT modular ring).</summary>
    private static EditableMesh MainCorridorRing()
    {
        EditableMesh? ring = null;
        const int segments = 14;
        for (var i = 0; i < segments; i++)
        {
            var ang = i * (MathF.PI * 2f / segments);
            var deg = ang * (180f / MathF.PI);
            // Gap for cockpit tube (~38°)
            if (deg is > 25f and < 55f)
                continue;

            var x = MathF.Sin(ang) * CorridorRadius;
            var z = MathF.Cos(ang) * CorridorRadius;
            var bay = ShipYard.ModuleShell(CorridorWidth, CorridorHeight, 3.1f, 0.04f);
            bay.Transform(ShipYard.Xf(x, 0.05f, z, 0, deg, 0));
            ring = ring is null ? bay : ShipYard.Union(ring, bay);

            var grate = ShipYard.Prim(MeshPrimitiveKind.Box, CorridorWidth * 0.85f, 0.04f, 2.9f,
                ShipYard.Xf(x, -CorridorHeight * 0.42f, z, 0, deg, 0));
            ring = ShipYard.Union(ring, grate);
        }

        foreach (var angDeg in new[] { 180f, 270f, 90f, 0f })
        {
            var ang = angDeg * (MathF.PI / 180f);
            var frame = ShipYard.HangarMouth(1.15f, 1.75f, 12);
            frame.Transform(ShipYard.Xf(
                MathF.Sin(ang) * CorridorRadius, 0.05f, MathF.Cos(ang) * CorridorRadius,
                0, angDeg, 0));
            ring = ShipYard.Union(ring!, frame);
        }

        return ring!;
    }

    /// <summary>Main hold (Haynes #13) — Dejarik lounge, contained in disc.</summary>
    private static EditableMesh MainHold()
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
        return ShipYard.Union(hold, refresher);
    }

    /// <summary>Aft engineering inside disc (Haynes #1–7) — no protruding engine box.</summary>
    private static EditableMesh EngineeringBay()
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

        // Escape-pod wells (stock YT often 2–5; Falcon keeps wells under aft)
        for (var i = 0; i < 5; i++)
        {
            var px = (i - 2) * 1.25f;
            bay = ShipYard.Union(bay, ShipYard.Prim(MeshPrimitiveKind.Cylinder, 0.75f, 0.1f, 0.75f,
                ShipYard.Xf(px, -HullHeight * 0.42f, -9.6f), 10));
        }

        return bay;
    }

    /// <summary>CEC AG-2G quads — dorsal + ventral (Haynes #8).</summary>
    private static EditableMesh GunwellsAndTurrets()
    {
        EditableMesh? g = null;
        void Add(EditableMesh p) => g = g is null ? p : ShipYard.Union(g, p);

        // Dorsal turret slightly port-forward of center; ventral starboard-aft (OT asymmetry)
        foreach (var (x, y, z) in new[]
                 {
                     (-1.8f, HullHeight * 0.55f, 1.2f),
                     (2.2f, -HullHeight * 0.55f, -2.8f),
                 })
        {
            Add(ShipYard.Prim(MeshPrimitiveKind.Cylinder, 1.15f, 0.4f, 1.15f,
                ShipYard.Xf(x, y, z), 14));
            Add(ShipYard.Prim(MeshPrimitiveKind.Sphere, 1.05f, 0.75f, 1.05f,
                ShipYard.Xf(x, y + MathF.Sign(y) * 0.3f, z), 14));
            foreach (var (ox, oz) in new[] { (-0.26f, 0.26f), (0.26f, 0.26f), (-0.26f, -0.26f), (0.26f, -0.26f) })
            {
                Add(ShipYard.Prim(MeshPrimitiveKind.Capsule, 0.1f, 1.0f, 0.1f,
                    ShipYard.Xf(x + ox, y + MathF.Sign(y) * 0.5f, z + oz, 90, 0, 0), 8));
            }
        }

        // Short gunwell shafts (do not pierce as tall soft towers)
        foreach (var (x, z) in new[] { (-1.8f, 1.2f), (2.2f, -2.8f) })
        {
            var shaft = ShipYard.ModuleShell(1.2f, HullHeight * 0.95f, 1.2f, 0.04f);
            shaft.Transform(ShipYard.Xf(x, 0, z));
            Add(shaft);
        }

        return g!;
    }

    private static EditableMesh SensorsAndAccess()
    {
        // Main rectenna (Haynes #15) — dorsal, slightly port-forward; boom height → ~8.27 envelope
        var dishY = HeightEnvelope * 0.5f - 0.35f; // top of dish near envelope
        var dish = ShipYard.SensorDish(1.85f, dishY - HullHeight * 0.35f, 20);
        dish.Transform(ShipYard.Xf(-2.4f, HullHeight * 0.35f, 4.8f, 0, 18, -12));

        EditableMesh sensors = dish;

        // Aux mast
        sensors = ShipYard.Union(sensors, ShipYard.Prim(MeshPrimitiveKind.Capsule, 0.1f, 2.2f, 0.1f,
            ShipYard.Xf(4.8f, HullHeight * 0.55f + 1.0f, -0.5f), 8));

        // Shield projectors / generators as rim blisters (Haynes #18–19)
        foreach (var (x, z) in new[] { (-SaucerRadius + 1.2f, -3f), (SaucerRadius - 1.2f, -4f), (-9f, 5f), (8.5f, 4.5f) })
        {
            sensors = ShipYard.Union(sensors, ShipYard.Prim(MeshPrimitiveKind.Sphere, 0.7f, 0.5f, 0.7f,
                ShipYard.Xf(x, HullHeight * 0.42f, z), 12));
        }

        // Port / starboard airlocks on rim
        var airL = ShipYard.DockingCollar(0.75f, 3, 12);
        airL.Transform(ShipYard.Xf(-SaucerRadius + 0.15f, 0.05f, -1.2f, 0, 0, 90));
        sensors = ShipYard.Union(sensors, airL);
        var airR = ShipYard.DockingCollar(0.7f, 3, 12);
        airR.Transform(ShipYard.Xf(SaucerRadius - 0.15f, 0.05f, -3.5f, 0, 0, -90));
        return ShipYard.Union(sensors, airR);
    }

    /// <summary>Girodyne SRB42 cluster — rectangular vents on aft rim (OT glow bank).</summary>
    private static EditableMesh SublightEngines()
    {
        EditableMesh? eng = null;
        void Add(EditableMesh p) => eng = eng is null ? p : ShipYard.Union(eng, p);

        // Housing stays inside / at rim — do not extend past AftZ by much
        Add(ShipYard.Prim(MeshPrimitiveKind.Box, 8.4f, HullHeight * 0.85f, 2.6f,
            ShipYard.Xf(0, 0.05f, AftZ + 1.0f)));

        // Classic multi-slot rectangular thruster bank
        float[] xs = [-3.2f, -1.6f, 0f, 1.6f, 3.2f];
        float[] ys = [0.55f, 0.55f, 0.55f, -0.45f, -0.45f];
        for (var i = 0; i < xs.Length; i++)
        {
            Add(ShipYard.Prim(MeshPrimitiveKind.Box, 1.15f, 0.85f, 0.55f,
                ShipYard.Xf(xs[i], ys[i], AftZ - 0.15f)));
            Add(ShipYard.Prim(MeshPrimitiveKind.Cylinder, 0.95f, 0.35f, 0.95f,
                ShipYard.Xf(xs[i], ys[i], AftZ - 0.55f, 90, 0, 0), 12));
        }

        // Heat vents on dorsal aft (Haynes #5)
        for (var i = 0; i < 6; i++)
            Add(ShipYard.Prim(MeshPrimitiveKind.Box, 0.32f, 0.12f, 0.75f,
                ShipYard.Xf(-2.5f + i * 1.0f, HullHeight * 0.42f, AftZ + 2.2f)));

        return eng!;
    }

    private static EditableMesh LandingGearAndRamp()
    {
        EditableMesh? g = null;
        void Add(EditableMesh p) => g = g is null ? p : ShipYard.Union(g, p);

        // YT-1300f-style pads under saucer (gear down for silhouette height cue)
        foreach (var (x, z) in new[]
                 {
                     (-6.5f, 3.5f), (6.5f, 3.5f), (-7.2f, -5.5f), (7.2f, -5.5f), (0f, AftZ + 1.5f),
                 })
        {
            Add(ShipYard.Prim(MeshPrimitiveKind.Capsule, 0.25f, 1.5f, 0.25f,
                ShipYard.Xf(x, -HullHeight * 0.55f - 0.6f, z), 8));
            Add(ShipYard.Prim(MeshPrimitiveKind.Cylinder, 1.0f, 0.16f, 1.0f,
                ShipYard.Xf(x, -HullHeight * 0.55f - 1.35f, z), 12));
        }

        // Port boarding ramp
        Add(ShipYard.Prim(MeshPrimitiveKind.Box, 1.5f, 0.1f, 4.0f,
            ShipYard.Xf(-SaucerRadius + 1.6f, -HullHeight * 0.35f - 0.4f, 1.2f, 22, 0, 0)));

        var hatch = ShipYard.DockingCollar(0.95f, 2, 12);
        hatch.Transform(ShipYard.Xf(0, -HullHeight * 0.42f, 2.2f, 90, 0, 0));
        Add(hatch);

        return g!;
    }

    private static EditableMesh ArmorGreebles()
    {
        EditableMesh? a = null;
        void Add(EditableMesh p) => a = a is null ? p : ShipYard.Union(a, p);

        var tile = ShipYard.SoftArmorTile(1.25f, 0.3f, 0.08f, 8);
        for (var i = 0; i < 20; i++)
        {
            var ang = i * (MathF.PI * 2f / 20f);
            if (ang is > -0.45f and < 0.45f || ang > MathF.PI * 2f - 0.45f)
                continue;
            var x = MathF.Sin(ang) * (SaucerRadius - 0.85f);
            var z = MathF.Cos(ang) * (SaucerRadius - 0.85f);
            var yaw = ang * (180f / MathF.PI);
            var t = tile.Clone();
            t.Transform(ShipYard.Xf(x, HullHeight * 0.4f, z, 0, yaw, 0));
            Add(t);
            var t2 = tile.Clone();
            t2.Transform(ShipYard.Xf(x, -HullHeight * 0.4f, z, 0, yaw, 0));
            Add(t2);
        }

        for (var i = 0; i < 8; i++)
        {
            var ang = i * (MathF.PI * 2f / 8f) + 0.25f;
            var rcs = ShipYard.ThrusterCluster(0.28f, 8);
            rcs.Transform(ShipYard.Xf(
                MathF.Sin(ang) * (SaucerRadius - 0.35f), 0.05f,
                MathF.Cos(ang) * (SaucerRadius - 0.35f)));
            Add(rcs);
        }

        return a!;
    }

    private static void Write(string dir, int stage, string label, EditableMesh ship)
    {
        var doc = new SceneDocument
        {
            Name = $"YT-1300f Homage — {label}",
            CreatedAt = DateTimeOffset.UtcNow,
            ModifiedAt = DateTimeOffset.UtcNow,
        };
        var root = new GroupNode { Name = "Root" };
        var cam = new CameraNode
        {
            Name = "Camera",
            ParentId = root.Id,
            Transform = new SceneTransform { Position = [28f, 14f, 32f] },
            Target = [0, 0.2f, 2f],
            FovDeg = 36f,
        };
        var mesh = new MeshNode
        {
            Name = "YT-1300f Homage Freighter",
            ParentId = root.Id,
            Primitive = MeshPrimitiveKind.Box,
        };
        MeshEditBake.WriteBaked(mesh, ship);
        doc.Nodes.AddRange([
            root, cam, mesh,
            new LightNode
            {
                Name = "Key",
                ParentId = root.Id,
                LightKind = LightKind.Spot,
                Intensity = 3.8f,
                Transform = new SceneTransform { Position = [22, 16, 18], RotationDeg = [40, -35, 0] },
            },
            new LightNode
            {
                Name = "Fill",
                ParentId = root.Id,
                LightKind = LightKind.Omni,
                Intensity = 2.0f,
                Color = [0.85f, 0.9f, 1f],
                Transform = new SceneTransform { Position = [0, 2f, -18f] },
            },
            new LightNode
            {
                Name = "Rim",
                ParentId = root.Id,
                LightKind = LightKind.Infinite,
                Intensity = 0.5f,
                Transform = new SceneTransform { RotationDeg = [-55, 30, 0] },
            },
        ]);
        doc.ActiveCameraId = null;
        doc.SelectionId = mesh.Id;
        var path = Path.Combine(dir, $"freighter-stage-{stage:00}.nov3djson");
        SceneSerializer.Save(doc, path);
        Console.WriteLine($"{stage}|{label}|{path}|verts={ship.VertexCount}|tris={ship.TriangleCount}");
    }
}
