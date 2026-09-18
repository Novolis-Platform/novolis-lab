using System.Numerics;
using Novolis.Math.Geometry;
using Novolis.ThreeD;

namespace ChiselCorvetteBuilder;

/// <summary>
/// Original Martian-style light frigate: long chisel hull + aft drive cup.
/// Silhouette language inspired by The Expanse corvette vernacular (not a licensed replica).
/// Tuned for OpenGL wireframe readability — curves, rings, masts, bells over boxes.
/// </summary>
internal static class CorvetteStages
{
    public static void BuildAll(string stageDir, string finalPath)
    {
        Directory.CreateDirectory(stageDir);
        EditableMesh ship;

        // 01 — Chisel hull spine (tapered capsule stack + wedge nose)
        ship = ChiselHull();
        Write(stageDir, 1, "chisel-hull", ship);

        // 02 — Mid cargo deck rails + struts (reads as lattice, not solid box)
        var deck = CargoDeckLattice();
        deck.Transform(ShipYard.Xf(0, -0.15f, -1.2f));
        ship = ShipYard.Union(ship, deck);
        Write(stageDir, 2, "cargo-deck", ship);

        // 03 — Bridge blister + sensor chin
        var bridge = BridgeBlister();
        bridge.Transform(ShipYard.Xf(0, 1.05f, 5.8f));
        ship = ShipYard.Union(ship, bridge);
        var chin = ShipYard.Prim(MeshPrimitiveKind.Capsule, 0.85f, 1.6f, 0.55f,
            ShipYard.Xf(0, -0.55f, 7.4f, 90, 0, 0), 14);
        ship = ShipYard.Union(ship, chin);
        Write(stageDir, 3, "bridge-chin", ship);

        // 04 — Side weapon pods + PDC turrets
        var pod = WeaponPod();
        pod.Transform(ShipYard.Xf(-2.05f, 0.15f, 0.6f));
        ship = ShipYard.Union(ship, ShipArray.SymmetricX(pod));
        foreach (var (x, y, z) in new[]
                 {
                     (-1.55f, 0.85f, 4.2f), (1.55f, 0.85f, 4.2f),
                     (-1.7f, 0.75f, -2.8f), (1.7f, 0.75f, -2.8f),
                     (0f, 1.15f, 7.8f), (0f, -0.85f, 2.5f),
                 })
        {
            var pdc = PdcTurret();
            pdc.Transform(ShipYard.Xf(x, y, z));
            ship = ShipYard.Union(ship, pdc);
        }
        Write(stageDir, 4, "weapons", ship);

        // 05 — Comms mast farm (wireframe hero)
        var mast = CommsMast();
        mast.Transform(ShipYard.Xf(0, 1.35f, 1.8f));
        ship = ShipYard.Union(ship, mast);
        var dish = ShipYard.SensorDish(0.55f, 1.1f, 16);
        dish.Transform(ShipYard.Xf(0.85f, 1.55f, 3.2f, 0, -20, 15));
        ship = ShipYard.Union(ship, dish);
        var dish2 = ShipYard.SensorDish(0.38f, 0.85f, 14);
        dish2.Transform(ShipYard.Xf(-0.95f, 1.45f, 2.4f, 0, 25, -10));
        ship = ShipYard.Union(ship, dish2);
        Write(stageDir, 5, "comms", ship);

        // 06 — Drive cup + reactor + engine bells
        var drive = DriveCup();
        drive.Transform(ShipYard.Xf(0, 0, -9.6f));
        ship = ShipYard.Union(ship, drive);
        Write(stageDir, 6, "drive", ship);

        // 07 — Radiators, RCS, docking, greeble silhouette finish
        var rad = ShipYard.RadiatorPanel(3.6f, 1.8f, 9);
        rad.Transform(ShipYard.Xf(-2.55f, 0.4f, -5.5f, 0, 8, 18));
        ship = ShipYard.Union(ship, ShipArray.SymmetricX(rad));

        foreach (var (x, y, z) in new[]
                 {
                     (-1.1f, 0.2f, 8.6f), (1.1f, 0.2f, 8.6f),
                     (-2.0f, 0.5f, -1.0f), (2.0f, 0.5f, -1.0f),
                     (-1.4f, 0.9f, -8.2f), (1.4f, 0.9f, -8.2f),
                     (0f, 1.6f, -7.0f), (0f, -0.9f, -6.5f),
                 })
        {
            var rcs = ShipYard.ThrusterCluster(0.22f, 8);
            rcs.Transform(ShipYard.Xf(x, y, z));
            ship = ShipYard.Union(ship, rcs);
        }

        var collar = ShipYard.DockingCollar(0.55f, 3, 14);
        collar.Transform(ShipYard.Xf(0, -0.95f, 6.2f, 90, 0, 0));
        ship = ShipYard.Union(ship, collar);

        var vent = ShipYard.SoftStringer(8.5f, 0.07f, 10);
        vent.Transform(ShipYard.Xf(-0.55f, -0.75f, -1.5f));
        ship = ShipYard.Union(ship, ShipArray.SymmetricX(vent));

        // Running lights along chisel edges
        var marker = ShipYard.MarkerPod(0.12f);
        var lights = ShipArray.Linear(marker, 11, new Vector3(0, 0, 1.35f));
        lights.Transform(ShipYard.Xf(0, 0.95f, -4.5f));
        ship = ShipYard.Union(ship, lights);

        // Aft stabilizer vanes (thin fins, not boxes)
        var fin = ShipYard.StabilizerFin(1.35f, 1.8f, 0.08f);
        fin.Transform(ShipYard.Xf(-1.55f, 0.35f, -11.4f, 0, 0, -12));
        ship = ShipYard.Union(ship, ShipArray.SymmetricX(fin));

        Write(stageDir, 7, "finish", ship);
        File.Copy(Path.Combine(stageDir, "chisel-stage-07.nov3djson"), finalPath, overwrite: true);
        Console.WriteLine($"FINAL|{finalPath}|verts={ship.VertexCount}|tris={ship.TriangleCount}");
    }

    /// <summary>Long chisel: capsule core + progressive cone tapers + soft nose tip.</summary>
    private static EditableMesh ChiselHull()
    {
        // Core pressure vessel — reads as cylinder/capsule in wireframe
        var core = ShipYard.Prim(MeshPrimitiveKind.Capsule, 1.55f, 14.5f, 1.55f,
            ShipYard.Xf(0, 0.15f, 0.5f, 90, 0, 0), 18);

        // Dorsal armor ridge (capsule, not plate)
        var ridge = ShipYard.SoftStringer(13.5f, 0.22f, 12);
        ridge.Transform(ShipYard.Xf(0, 0.95f, 0.3f));
        core = ShipYard.Union(core, ridge);

        // Lateral cheek fairings
        var cheek = ShipYard.Prim(MeshPrimitiveKind.Capsule, 0.7f, 10.5f, 0.95f,
            ShipYard.Xf(-1.15f, 0.05f, 0.2f, 90, 0, 0), 14);
        core = ShipYard.Union(core, ShipArray.SymmetricX(cheek));

        // Chisel tip — stacked taper toward +Z
        var tip = ShipYard.Prim(MeshPrimitiveKind.Cone, 1.35f, 3.4f, 1.35f,
            ShipYard.Xf(0, 0.1f, 8.55f, -90, 0, 0), 16);
        core = ShipYard.Union(core, tip);
        var tipCap = ShipYard.Prim(MeshPrimitiveKind.Sphere, 0.55f, 0.4f, 0.55f,
            ShipYard.Xf(0, 0.05f, 10.35f), 12);
        core = ShipYard.Union(core, tipCap);

        // Mid-body pressure rings
        var rings = ShipYard.PressureRings(7, 1.55f, 0.82f, 0.055f, 20);
        rings.Transform(ShipYard.Xf(0, 0.15f, 0.2f));
        core = ShipYard.Union(core, rings);

        return core;
    }

    private static EditableMesh CargoDeckLattice()
    {
        var rail = ShipYard.SoftStringer(9.5f, 0.08f, 10);
        var rails = ShipArray.Linear(rail.Clone(), 3, new Vector3(0.55f, 0, 0));
        rails.Transform(ShipYard.Xf(-0.55f, 0, 0));
        var cross = ShipYard.Prim(MeshPrimitiveKind.Capsule, 0.07f, 1.35f, 0.07f,
            Matrix4x4.Identity, 8);
        cross.Transform(ShipYard.Xf(0, 0, 0, 0, 0, 90));
        var crosses = ShipArray.Linear(cross, 8, new Vector3(0, 0, 1.15f));
        crosses.Transform(ShipYard.Xf(0, 0, -3.5f));
        var deck = ShipYard.Union(rails, crosses);
        // Soft hangar mouth under midships
        var mouth = ShipYard.HangarMouth(1.1f, 0.85f, 12);
        mouth.Transform(ShipYard.Xf(0, -0.35f, 1.2f));
        return ShipYard.Union(deck, mouth);
    }

    private static EditableMesh BridgeBlister()
    {
        var blister = ShipYard.Prim(MeshPrimitiveKind.Sphere, 1.35f, 0.85f, 1.55f,
            Matrix4x4.Identity, 14);
        var canopy = ShipYard.Prim(MeshPrimitiveKind.Torus, 0.95f, 0.06f, 0.7f,
            ShipYard.Xf(0, 0.25f, 0.35f, 90, 0, 0), 14);
        var cupola = ShipYard.ViewportRing(0.28f, 12);
        cupola.Transform(ShipYard.Xf(0, 0.45f, 0.55f));
        return ShipYard.Union(ShipYard.Union(blister, canopy), cupola);
    }

    private static EditableMesh WeaponPod()
    {
        var body = ShipYard.Prim(MeshPrimitiveKind.Capsule, 0.65f, 3.8f, 0.75f,
            ShipYard.Xf(0, 0, 0, 90, 0, 0), 12);
        var tip = ShipYard.Prim(MeshPrimitiveKind.Cone, 0.45f, 0.9f, 0.45f,
            ShipYard.Xf(0, 0, 2.15f, -90, 0, 0), 10);
        var mount = ShipYard.Prim(MeshPrimitiveKind.Torus, 0.55f, 0.07f, 0.55f,
            ShipYard.Xf(0, 0, -0.4f, 0, 90, 0), 12);
        return ShipYard.Union(ShipYard.Union(body, tip), mount);
    }

    private static EditableMesh PdcTurret()
    {
        var baseRing = ShipYard.Prim(MeshPrimitiveKind.Cylinder, 0.32f, 0.12f, 0.32f,
            Matrix4x4.Identity, 10);
        var yoke = ShipYard.Prim(MeshPrimitiveKind.Torus, 0.28f, 0.05f, 0.28f,
            ShipYard.Xf(0, 0.12f, 0, 90, 0, 0), 10);
        var barrel = ShipYard.Prim(MeshPrimitiveKind.Capsule, 0.08f, 0.55f, 0.08f,
            ShipYard.Xf(0, 0.18f, 0.2f, 90, 0, 0), 8);
        return ShipYard.Union(ShipYard.Union(baseRing, yoke), barrel);
    }

    private static EditableMesh CommsMast()
    {
        var pole = ShipYard.Prim(MeshPrimitiveKind.Capsule, 0.12f, 2.4f, 0.12f,
            ShipYard.Xf(0, 1.1f, 0), 8);
        var yard = ShipYard.Prim(MeshPrimitiveKind.Capsule, 0.07f, 2.0f, 0.07f,
            ShipYard.Xf(0, 2.0f, 0, 0, 0, 90), 8);
        var whip = ShipYard.Prim(MeshPrimitiveKind.Capsule, 0.04f, 1.6f, 0.04f,
            ShipYard.Xf(0.55f, 2.35f, 0.15f, 15, 0, 20), 6);
        var whip2 = ShipYard.Prim(MeshPrimitiveKind.Capsule, 0.04f, 1.4f, 0.04f,
            ShipYard.Xf(-0.55f, 2.25f, -0.1f, -10, 0, -25), 6);
        var hub = ShipYard.Prim(MeshPrimitiveKind.Sphere, 0.22f, 0.22f, 0.22f,
            ShipYard.Xf(0, 2.05f, 0), 10);
        return ShipYard.Union(ShipYard.Union(ShipYard.Union(ShipYard.Union(pole, yard), whip), whip2), hub);
    }

    /// <summary>Aft drive: coffee-cup reactor housing + triple bells + collar rings.</summary>
    private static EditableMesh DriveCup()
    {
        var cup = ShipYard.Prim(MeshPrimitiveKind.Capsule, 2.4f, 3.2f, 2.4f,
            ShipYard.Xf(0, 0.1f, 0, 90, 0, 0), 18);
        var dome = ShipYard.Prim(MeshPrimitiveKind.Sphere, 2.35f, 2.0f, 2.35f,
            ShipYard.Xf(0, 0.1f, 1.1f), 16);
        var collar = ShipYard.Prim(MeshPrimitiveKind.Torus, 2.15f, 0.14f, 2.15f,
            ShipYard.Xf(0, 0.1f, -0.85f, 90, 0, 0), 18);
        var collar2 = ShipYard.Prim(MeshPrimitiveKind.Torus, 1.85f, 0.1f, 1.85f,
            ShipYard.Xf(0, 0.1f, -1.35f, 90, 0, 0), 16);
        var neck = ShipYard.Prim(MeshPrimitiveKind.Cylinder, 1.4f, 1.6f, 1.4f,
            ShipYard.Xf(0, 0.1f, 2.35f, 90, 0, 0), 14);

        EditableMesh drive = ShipYard.Union(ShipYard.Union(ShipYard.Union(ShipYard.Union(cup, dome), collar), collar2), neck);

        // Triple engine bells (center + angled outboard)
        foreach (var (x, y, z, s) in new[]
                 {
                     (0f, 0.1f, -2.35f, 1f),
                     (-0.95f, 0.05f, -2.05f, 0.78f),
                     (0.95f, 0.05f, -2.05f, 0.78f),
                 })
        {
            var bell = ShipYard.EngineBell(0.55f * s, 1.15f * s, 16);
            bell.Transform(ShipYard.Xf(x, y, z));
            drive = ShipYard.Union(drive, bell);
        }

        // Heat exchanger pipes around cup
        var pipe = ShipYard.Prim(MeshPrimitiveKind.Torus, 2.55f, 0.08f, 2.55f,
            ShipYard.Xf(0, 0.1f, 0.2f, 0, 0, 0), 16);
        drive = ShipYard.Union(drive, pipe);
        var pipe2 = ShipYard.Prim(MeshPrimitiveKind.Torus, 2.55f, 0.08f, 2.55f,
            ShipYard.Xf(0, 0.1f, 0.2f, 90, 0, 0), 16);
        drive = ShipYard.Union(drive, pipe2);

        return drive;
    }

    private static void Write(string dir, int stage, string label, EditableMesh ship)
    {
        var doc = new SceneDocument
        {
            Name = $"Chisel Corvette — {label}",
            CreatedAt = DateTimeOffset.UtcNow,
            ModifiedAt = DateTimeOffset.UtcNow,
        };
        var root = new GroupNode { Name = "Root" };
        var cam = new CameraNode
        {
            Name = "Camera",
            ParentId = root.Id,
            Transform = new SceneTransform { Position = [18f, 9f, 22f] },
            Target = [0, 0.4f, -1f],
            FovDeg = 40f,
        };
        var mesh = new MeshNode
        {
            Name = "Chisel Corvette",
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
                Intensity = 3.6f,
                Transform = new SceneTransform { Position = [12, 10, 10], RotationDeg = [35, -35, 0] },
            },
            new LightNode
            {
                Name = "Fill Light",
                ParentId = root.Id,
                LightKind = LightKind.Omni,
                Intensity = 2.2f,
                Color = [0.8f, 0.88f, 1f],
                Transform = new SceneTransform { Position = [0, 1f, -12f] },
            },
            new LightNode
            {
                Name = "Rim",
                ParentId = root.Id,
                LightKind = LightKind.Infinite,
                Intensity = 0.55f,
                Transform = new SceneTransform { RotationDeg = [-50, 40, 0] },
            },
        ]);
        doc.ActiveCameraId = null;
        doc.SelectionId = mesh.Id;
        var path = Path.Combine(dir, $"chisel-stage-{stage:00}.nov3djson");
        SceneSerializer.Save(doc, path);
        Console.WriteLine($"{stage}|{label}|{path}|verts={ship.VertexCount}|tris={ship.TriangleCount}");
    }
}
