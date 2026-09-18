using System.Numerics;
using Novolis.Math.Geometry;
using Novolis.ThreeD;

namespace KeelTransportBuilder;

/// <summary>Interior-first keel transport build stages (one EditableMesh).</summary>
internal static class KeelStages
{
    public static void BuildAll(string stageDir, string finalPath)
    {
        Directory.CreateDirectory(stageDir);
        EditableMesh ship;

        // 01 — Keel + pressure tunnel rings (soft-edged spine)
        ship = ShipYard.SoftModuleShell(1.6f, 1.6f, 22f, 0.055f, 0.12f, 10);
        ship.Transform(ShipYard.Xf(0, 1.1f, 0));
        var rings = ShipYard.PressureRings(18, 1.1f, 0.62f, 0.07f, 22);
        rings.Transform(ShipYard.Xf(0, 1.1f, 0));
        ship = ShipYard.Union(ship, rings);
        var walk = ShipYard.Catwalk(21f, 0.8f, 0.42f);
        walk.Transform(ShipYard.Xf(0, 0.45f, 0));
        ship = ShipYard.Union(ship, walk);
        var keelRail = ShipYard.SoftStringer(21.5f, 0.09f, 12);
        keelRail.Transform(ShipYard.Xf(0, 1.95f, 0));
        ship = ShipYard.Union(ship, keelRail);
        Write(stageDir, 1, "keel-tunnel", ship);

        // 02 — Mid module shells (symmetric, soft-edged)
        var midShell = ShipYard.SoftModuleShell(3.6f, 3.0f, 6.5f, 0.05f, 0.18f, 10);
        midShell.Transform(ShipYard.Xf(-2.9f, 1.2f, 0.4f));
        ship = ShipYard.Union(ship, ShipArray.SymmetricX(midShell));
        // Join fairings at mid↔keel
        var fairing = ShipYard.Prim(MeshPrimitiveKind.Torus, 1.1f, 0.16f, 1.1f, Matrix4x4.Identity, 16);
        fairing.Transform(ShipYard.Xf(-1.55f, 1.2f, 0.4f, 0, 0, 90));
        ship = ShipYard.Union(ship, ShipArray.SymmetricX(fairing));
        Write(stageDir, 2, "module-shells", ship);

        // 03 — Decks + bulkheads
        var decks = ShipYard.DeckStack(3, 0.95f, 3.25f, 6.0f, 0.04f);
        decks.Transform(ShipYard.Xf(-2.9f, 1.2f, 0.4f));
        ship = ShipYard.Union(ship, ShipArray.SymmetricX(decks));
        var bulkheads = ShipYard.BulkheadStack(6, 0.95f, 3.25f, 2.7f, 0.045f);
        bulkheads.Transform(ShipYard.Xf(-2.9f, 1.2f, 0.4f));
        ship = ShipYard.Union(ship, ShipArray.SymmetricX(bulkheads));
        var ladder = ShipYard.LadderWell(2.6f, 0.5f);
        ladder.Transform(ShipYard.Xf(-2.1f, 1.2f, -2.2f));
        ship = ShipYard.Union(ship, ShipArray.SymmetricX(ladder));
        Write(stageDir, 3, "decks-bulkheads", ship);

        // 04 — Crew/cargo bay arrays + corridor
        var corridor = ShipYard.Corridor(5.8f, 1.0f, 2.3f, 0.04f);
        corridor.Transform(ShipYard.Xf(-2.9f, 1.2f, 0.4f));
        ship = ShipYard.Union(ship, ShipArray.SymmetricX(corridor));
        var bayLower = ShipYard.PodBay(3, 10, 0.65f, 0.62f);
        bayLower.Transform(ShipYard.Xf(-3.7f, 0.35f, -2.4f));
        ship = ShipYard.Union(ship, ShipArray.SymmetricX(bayLower));
        var bayUpper = ShipYard.PodBay(2, 10, 0.7f, 0.62f);
        bayUpper.Transform(ShipYard.Xf(-3.7f, 1.85f, -2.4f));
        ship = ShipYard.Union(ship, ShipArray.SymmetricX(bayUpper));
        var locker = ShipYard.Prim(MeshPrimitiveKind.Box, 0.24f, 0.8f, 0.3f, Matrix4x4.Identity);
        var lockers = ShipArray.Linear(locker, 16, new Vector3(0, 0, 0.38f));
        lockers.Transform(ShipYard.Xf(-2.35f, 0.55f, -2.5f));
        ship = ShipYard.Union(ship, ShipArray.SymmetricX(lockers));
        // Soft bunk pods (capsule beds) along corridor
        var bunk = ShipYard.Prim(MeshPrimitiveKind.Capsule, 0.35f, 0.95f, 0.35f, Matrix4x4.Identity, 10);
        var bunks = ShipArray.Linear(bunk, 8, new Vector3(0, 0, 0.7f));
        bunks.Transform(ShipYard.Xf(-2.15f, 1.55f, -2.0f, 0, 0, 90));
        ship = ShipYard.Union(ship, ShipArray.SymmetricX(bunks));
        Write(stageDir, 4, "crew-bays", ship);

        // 05 — Bow shell + airlock interior (blunted soft nose)
        var bowShell = ShipYard.SoftModuleShell(2.8f, 2.0f, 3.6f, 0.055f, 0.16f, 10);
        bowShell.Transform(ShipYard.Xf(0, 0.85f, 7.4f));
        ship = ShipYard.Union(ship, bowShell);
        var airlock = ShipYard.Corridor(1.5f, 0.9f, 2.0f, 0.05f);
        airlock.Transform(ShipYard.Xf(0, 0.7f, 8.7f));
        ship = ShipYard.Union(ship, airlock);
        var magL = ShipYard.PodBay(2, 5, 0.55f, 0.5f, 0.32f, 0.38f, 0.42f);
        magL.Transform(ShipYard.Xf(-0.85f, 0.7f, 6.6f));
        ship = ShipYard.Union(ship, magL);
        var magR = ShipYard.PodBay(2, 5, 0.55f, 0.5f, 0.32f, 0.38f, 0.42f);
        magR.Transform(ShipYard.Xf(0.85f, 0.7f, 6.6f));
        ship = ShipYard.Union(ship, magR);
        var nose = ShipYard.SoftNose(0.95f, 2.4f, 12);
        nose.Transform(ShipYard.Xf(0, 0.7f, 9.35f));
        ship = ShipYard.Union(ship, nose);
        Write(stageDir, 5, "bow-airlock", ship);

        // 06 — Bridge tower + CIC furniture (soft tower)
        var tower = ShipYard.SoftModuleShell(2.4f, 3.4f, 3.0f, 0.05f, 0.16f, 10);
        tower.Transform(ShipYard.Xf(0, 3.0f, -7.2f));
        ship = ShipYard.Union(ship, tower);
        var cicDecks = ShipYard.DeckStack(3, 1.0f, 2.15f, 2.7f, 0.04f);
        cicDecks.Transform(ShipYard.Xf(0, 3.0f, -7.2f));
        ship = ShipYard.Union(ship, cicDecks);
        var console = ShipYard.Prim(MeshPrimitiveKind.Box, 0.55f, 0.7f, 0.35f, Matrix4x4.Identity);
        var banks = ShipArray.Linear(console, 5, new Vector3(0.42f, 0, 0));
        banks.Transform(ShipYard.Xf(-0.84f, 2.35f, -6.35f));
        ship = ShipYard.Union(ship, banks);
        var banks2 = ShipArray.Linear(console, 5, new Vector3(0.42f, 0, 0));
        banks2.Transform(ShipYard.Xf(-0.84f, 3.35f, -6.35f));
        ship = ShipYard.Union(ship, banks2);
        var seat = ShipYard.Prim(MeshPrimitiveKind.Cylinder, 0.28f, 0.45f, 0.28f, Matrix4x4.Identity, 10);
        var seats = ShipArray.Linear(seat, 4, new Vector3(0.5f, 0, 0));
        seats.Transform(ShipYard.Xf(-0.75f, 2.15f, -7.55f));
        ship = ShipYard.Union(ship, seats);
        var ladderBridge = ShipYard.LadderWell(3.2f, 0.45f);
        ladderBridge.Transform(ShipYard.Xf(0.7f, 2.0f, -8.2f));
        ship = ShipYard.Union(ship, ladderBridge);
        Write(stageDir, 6, "bridge-cic", ship);

        // 07 — Engineering + catwalks + soft engines
        var eng = ShipYard.SoftModuleShell(3.2f, 2.6f, 4.2f, 0.05f, 0.17f, 10);
        eng.Transform(ShipYard.Xf(0, 1.0f, -10.4f));
        ship = ShipYard.Union(ship, eng);
        var engDecks = ShipYard.DeckStack(2, 1.1f, 2.9f, 3.8f, 0.045f);
        engDecks.Transform(ShipYard.Xf(0, 1.0f, -10.4f));
        ship = ShipYard.Union(ship, engDecks);
        var cat = ShipYard.Catwalk(3.6f, 0.55f, 0.35f);
        cat.Transform(ShipYard.Xf(0, 1.55f, -10.4f));
        ship = ShipYard.Union(ship, cat);
        var coolant = ShipYard.Prim(MeshPrimitiveKind.Capsule, 0.55f, 1.4f, 0.55f, Matrix4x4.Identity, 14);
        var tanks = ShipArray.Linear(coolant, 4, new Vector3(0.85f, 0, 0));
        tanks.Transform(ShipYard.Xf(-1.275f, 0.7f, -9.4f));
        ship = ShipYard.Union(ship, tanks);
        var mount = ShipYard.SoftModuleShell(0.95f, 0.6f, 1.15f, 0.04f, 0.1f, 10);
        var mounts = ShipArray.Linear(mount, 2, new Vector3(1.6f, 0, 0));
        mounts.Transform(ShipYard.Xf(-0.8f, 0.55f, -11.5f));
        ship = ShipYard.Union(ship, mounts);
        // Soft nacelles + engine bells (wireframe hero)
        foreach (var x in new[] { -1.35f, 1.35f })
        {
            ship = ShipYard.Union(ship,
                ShipYard.Prim(MeshPrimitiveKind.Capsule, 1.0f, 2.6f, 1.0f, ShipYard.Xf(x, 0.7f, -12.9f, 90, 0, 0), 16));
            ship = ShipYard.Union(ship,
                ShipYard.Prim(MeshPrimitiveKind.Sphere, 1.05f, 1.05f, 1.05f, ShipYard.Xf(x, 0.7f, -14.35f), 14));
            ship = ShipYard.Union(ship,
                ShipYard.Prim(MeshPrimitiveKind.Torus, 0.85f, 0.12f, 0.85f, ShipYard.Xf(x, 0.7f, -13.7f, 90, 0, 0), 14));
            var bell = ShipYard.EngineBell(0.58f, 1.15f, 16);
            bell.Transform(ShipYard.Xf(x, 0.7f, -14.9f));
            ship = ShipYard.Union(ship, bell);
        }
        Write(stageDir, 7, "engineering", ship);

        // 08 — Bay mouths / door cuts + soft exterior skin + silhouette greeble → final
        ship = ShipYard.CutOpening(ship, ShipYard.Prim(MeshPrimitiveKind.Box, 1.2f, 1.5f, 0.6f,
            ShipYard.Xf(-4.55f, 0.9f, -0.8f)));
        ship = ShipYard.CutOpening(ship, ShipYard.Prim(MeshPrimitiveKind.Box, 1.2f, 1.5f, 0.6f,
            ShipYard.Xf(4.55f, 0.9f, -0.8f)));
        ship = ShipYard.CutOpening(ship, ShipYard.Prim(MeshPrimitiveKind.Box, 0.9f, 1.8f, 0.5f,
            ShipYard.Xf(0, 0.85f, 9.15f)));
        ship = ShipYard.CutOpening(ship, ShipYard.Prim(MeshPrimitiveKind.Box, 1.4f, 0.35f, 0.25f,
            ShipYard.Xf(0, 3.9f, -5.85f)));
        ship = ShipYard.CutOpening(ship, ShipYard.Prim(MeshPrimitiveKind.Box, 1.0f, 0.3f, 0.22f,
            ShipYard.Xf(0, 4.5f, -5.85f)));

        // Soft armor tiles (capsules) instead of box plates
        var tile = ShipYard.SoftArmorTile(0.85f, 0.28f, 0.1f, 10);
        var skinL = ShipArray.Grid(tile, 3, 7, new Vector3(0, 0.52f, 0), new Vector3(0, 0, 0.9f));
        skinL.Transform(ShipYard.Xf(-4.85f, 0.55f, -2.2f));
        ship = ShipYard.Union(ship, skinL);
        var skinR = ShipArray.Grid(tile, 3, 7, new Vector3(0, 0.52f, 0), new Vector3(0, 0, 0.9f));
        skinR.Transform(ShipYard.Xf(4.85f, 0.55f, -2.2f));
        ship = ShipYard.Union(ship, skinR);

        // Dorsal stringers + soft bay-lip rings
        var dorsal = ShipYard.SoftStringer(6.2f, 0.08f, 10);
        dorsal.Transform(ShipYard.Xf(-2.9f, 2.75f, 0.4f));
        ship = ShipYard.Union(ship, ShipArray.SymmetricX(dorsal));
        var bayLip = ShipYard.Prim(MeshPrimitiveKind.Torus, 0.95f, 0.1f, 0.95f, Matrix4x4.Identity, 14);
        bayLip.Transform(ShipYard.Xf(-4.55f, 0.9f, -0.8f, 0, 90, 0));
        ship = ShipYard.Union(ship, ShipArray.SymmetricX(bayLip));

        // Bridge antennas + dishes (OpenGL wireframe heroes)
        var antenna = ShipYard.Prim(MeshPrimitiveKind.Capsule, 0.07f, 1.9f, 0.07f, Matrix4x4.Identity, 8);
        var antennas = ShipArray.Linear(antenna, 4, new Vector3(0.35f, 0, 0));
        antennas.Transform(ShipYard.Xf(-0.525f, 4.9f, -7.0f));
        ship = ShipYard.Union(ship, antennas);
        var dish = ShipYard.SensorDish(0.72f, 1.35f, 16);
        dish.Transform(ShipYard.Xf(1.1f, 4.55f, -6.4f));
        ship = ShipYard.Union(ship, dish);
        var dish2 = ShipYard.SensorDish(0.48f, 0.95f, 14);
        dish2.Transform(ShipYard.Xf(-1.25f, 4.7f, -7.6f, 0, 25, 0));
        ship = ShipYard.Union(ship, dish2);

        // Bridge viewport rings
        foreach (var (x, y) in new[] { (-0.55f, 3.95f), (0.55f, 3.95f), (-0.55f, 4.45f), (0.55f, 4.45f), (0f, 4.15f) })
        {
            var win = ShipYard.ViewportRing(0.22f, 12);
            win.Transform(ShipYard.Xf(x, y, -5.7f, 0, 0, 0));
            ship = ShipYard.Union(ship, win);
        }

        // Radiator wings on mid modules
        var rad = ShipYard.RadiatorPanel(4.2f, 1.6f, 8);
        rad.Transform(ShipYard.Xf(-5.35f, 1.4f, 0.4f, 0, 0, 12));
        ship = ShipYard.Union(ship, ShipArray.SymmetricX(rad));

        // Docking collar under bow airlock
        var collar = ShipYard.DockingCollar(0.7f, 4, 16);
        collar.Transform(ShipYard.Xf(0, 0.15f, 8.9f, 90, 0, 0));
        ship = ShipYard.Union(ship, collar);

        // External cargo clamps along mid modules
        var clamp = ShipYard.CargoClamp(0.85f);
        var clamps = ShipArray.Linear(clamp, 5, new Vector3(0, 0, 1.05f));
        clamps.Transform(ShipYard.Xf(-4.95f, 2.35f, -1.8f, 0, 0, -20));
        ship = ShipYard.Union(ship, ShipArray.SymmetricX(clamps));

        // Running lights along keel
        var marker = ShipYard.MarkerPod(0.14f);
        var lights = ShipArray.Linear(marker, 9, new Vector3(0, 0, 2.4f));
        lights.Transform(ShipYard.Xf(0, 2.05f, -9.6f));
        ship = ShipYard.Union(ship, lights);
        var cheekL = ShipYard.MarkerPod(0.2f);
        cheekL.Transform(ShipYard.Xf(-1.6f, 0.55f, 9.6f));
        ship = ShipYard.Union(ship, ShipArray.SymmetricX(cheekL));

        // Ventral cargo keel strip (extra silhouette under mid)
        var ventral = ShipYard.SoftStringer(12f, 0.11f, 12);
        ventral.Transform(ShipYard.Xf(0, 0.12f, -1.5f));
        ship = ShipYard.Union(ship, ventral);
        var ventralPods = ShipYard.PodBay(1, 10, 0.5f, 0.95f, 0.4f, 0.35f, 0.55f);
        ventralPods.Transform(ShipYard.Xf(0, 0.35f, -5.5f));
        ship = ShipYard.Union(ship, ventralPods);

        // Utility conduits along keel tunnel
        var conduit = ShipYard.ConduitRun(20f, 0.055f, 8);
        conduit.Transform(ShipYard.Xf(-0.55f, 1.55f, 0));
        ship = ShipYard.Union(ship, ShipArray.SymmetricX(conduit));
        var conduitLow = ShipYard.ConduitRun(18f, 0.045f, 8);
        conduitLow.Transform(ShipYard.Xf(-0.35f, 0.75f, 0.2f));
        ship = ShipYard.Union(ship, ShipArray.SymmetricX(conduitLow));

        // RCS thruster clusters (bow / mid / aft)
        foreach (var (x, y, z) in new[]
                 {
                     (-1.4f, 0.4f, 9.2f), (1.4f, 0.4f, 9.2f),
                     (-5.1f, 2.0f, 1.2f), (5.1f, 2.0f, 1.2f),
                     (-2.0f, 1.6f, -12.2f), (2.0f, 1.6f, -12.2f),
                     (0f, 3.6f, -8.5f),
                 })
        {
            var rcs = ShipYard.ThrusterCluster(0.28f, 8);
            rcs.Transform(ShipYard.Xf(x, y, z));
            ship = ShipYard.Union(ship, rcs);
        }

        // Aft stabilizer fins
        var fin = ShipYard.StabilizerFin(1.9f, 1.5f, 0.1f);
        fin.Transform(ShipYard.Xf(-1.9f, 2.2f, -13.2f, 0, 0, -8));
        ship = ShipYard.Union(ship, ShipArray.SymmetricX(fin));
        var finV = ShipYard.StabilizerFin(1.4f, 1.1f, 0.09f);
        finV.Transform(ShipYard.Xf(0, 3.1f, -13.0f, 0, 0, 0));
        ship = ShipYard.Union(ship, finV);

        // Mid cargo gantries (reads as overhead crane in wireframe)
        var gantry = ShipYard.CargoGantry(3.0f, 0.85f);
        gantry.Transform(ShipYard.Xf(-2.9f, 2.55f, -0.6f));
        ship = ShipYard.Union(ship, ShipArray.SymmetricX(gantry));
        var gantry2 = ShipYard.CargoGantry(2.6f, 0.75f);
        gantry2.Transform(ShipYard.Xf(-2.9f, 2.55f, 1.4f));
        ship = ShipYard.Union(ship, ShipArray.SymmetricX(gantry2));

        // Hangar mouths over bay cuts
        var mouth = ShipYard.HangarMouth(1.35f, 1.55f, 14);
        mouth.Transform(ShipYard.Xf(-4.55f, 0.9f, -0.8f));
        ship = ShipYard.Union(ship, ShipArray.SymmetricX(mouth));

        // Solar / radiator sail panels aft of mid (extra lattice silhouette)
        var sail = ShipYard.RadiatorPanel(3.4f, 2.2f, 10);
        sail.Transform(ShipYard.Xf(-5.6f, 2.4f, -3.2f, 0, 15, 25));
        ship = ShipYard.Union(ship, ShipArray.SymmetricX(sail));

        // Extra engine centerline thruster (third bell)
        var centerBell = ShipYard.EngineBell(0.42f, 0.95f, 14);
        centerBell.Transform(ShipYard.Xf(0, 0.55f, -14.6f));
        ship = ShipYard.Union(ship, centerBell);
        ship = ShipYard.Union(ship,
            ShipYard.Prim(MeshPrimitiveKind.Capsule, 0.7f, 1.8f, 0.7f, ShipYard.Xf(0, 0.55f, -13.5f, 90, 0, 0), 14));

        Write(stageDir, 8, "cuts-skin", ship);

        File.Copy(Path.Combine(stageDir, "keel-stage-08.nov3djson"), finalPath, overwrite: true);
        Console.WriteLine($"FINAL|{finalPath}|verts={ship.VertexCount}|tris={ship.TriangleCount}");
    }

    private static void Write(string dir, int stage, string label, EditableMesh ship)
    {
        var doc = new SceneDocument
        {
            Name = $"Keel Transport — {label}",
            CreatedAt = DateTimeOffset.UtcNow,
            ModifiedAt = DateTimeOffset.UtcNow,
        };
        var root = new GroupNode { Name = "Root" };
        var cam = new CameraNode
        {
            Name = "Camera",
            ParentId = root.Id,
            Transform = new SceneTransform { Position = [22f, 12f, 26f] },
            Target = [0, 1.8f, -1f],
            FovDeg = 42f,
        };
        var mesh = new MeshNode
        {
            Name = "Keel Transport",
            ParentId = root.Id,
            Primitive = MeshPrimitiveKind.Box,
        };
        MeshEditBake.WriteBaked(mesh, ship);
        doc.Nodes.AddRange([
            root, cam, mesh,
            new LightNode
            {
                Name = "Key Light",
                ParentId = root.Id,
                LightKind = LightKind.Spot,
                Intensity = 3.4f,
                Transform = new SceneTransform { Position = [10, 12, 8], RotationDeg = [40, -30, 0] },
            },
            new LightNode
            {
                Name = "Fill Light",
                ParentId = root.Id,
                LightKind = LightKind.Omni,
                Intensity = 2.4f,
                Color = [0.85f, 0.9f, 1f],
                Transform = new SceneTransform { Position = [0, 0.8f, -13f] },
            },
            new LightNode
            {
                Name = "Sun",
                ParentId = root.Id,
                LightKind = LightKind.Infinite,
                Intensity = 0.45f,
                Transform = new SceneTransform { RotationDeg = [-40, 25, 0] },
            },
        ]);
        // Free orbit in OpenGL SceneLab — don't lock to baked camera.
        doc.ActiveCameraId = null;
        doc.SelectionId = mesh.Id;
        var path = Path.Combine(dir, $"keel-stage-{stage:00}.nov3djson");
        SceneSerializer.Save(doc, path);
        Console.WriteLine($"{stage}|{label}|{path}|verts={ship.VertexCount}|tris={ship.TriangleCount}");
    }
}
