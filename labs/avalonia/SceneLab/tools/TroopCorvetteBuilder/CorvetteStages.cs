using System.Numerics;
using Novolis.Math.Geometry;
using Novolis.ThreeD;

namespace TroopCorvetteBuilder;

internal static class CorvetteStages
{
    public static void BuildAll(string stageDir, string finalPath)
    {
        Directory.CreateDirectory(stageDir);
        EditableMesh ship;

        // 01 Keel — structural spine along +Z (bow = +Z)
        ship = ShipYard.Prim(MeshPrimitiveKind.Box, 0.55f, 0.45f, 16f, ShipYard.Xf(0, 0.4f, 0));
        Write(stageDir, 1, "keel", ship);

        // 02 Bow — armored wedge + chin
        ship = ShipYard.Union(ship, ShipYard.Prim(MeshPrimitiveKind.Box, 2.4f, 1.3f, 3.2f, ShipYard.Xf(0, 0.55f, 6.2f)));
        ship = ShipYard.Union(ship, ShipYard.Prim(MeshPrimitiveKind.Pyramid, 2.2f, 1.1f, 2.0f, ShipYard.Xf(0, 0.5f, 8.4f, -90, 0, 0)));
        ship = ShipYard.Union(ship, ShipYard.Prim(MeshPrimitiveKind.Box, 1.8f, 0.55f, 2.0f, ShipYard.Xf(0, -0.05f, 6.0f)));
        Write(stageDir, 2, "bow", ship);

        // 03 Mid — port/stbd hull blocks on keel
        var midPort = ShipYard.Prim(MeshPrimitiveKind.Box, 1.5f, 1.1f, 4.5f, ShipYard.Xf(-1.35f, 0.35f, 0.5f));
        midPort = ShipYard.Union(midPort, ShipYard.Prim(MeshPrimitiveKind.Box, 1.2f, 0.7f, 3.2f, ShipYard.Xf(-1.6f, -0.15f, 0.2f)));
        ship = ShipYard.Union(ship, ShipArray.SymmetricX(midPort));
        Write(stageDir, 3, "mid-hull", ship);

        // 04 Truss — open midships X-brace array along spine
        var brace = ShipYard.XBrace(0.7f, 0.07f);
        brace.Transform(ShipYard.Xf(0, 1.15f, -2.5f));
        var truss = ShipArray.Linear(brace, 7, new Vector3(0, 0, 0.85f));
        // longitudinal chords
        truss = ShipYard.Union(truss, ShipYard.Prim(MeshPrimitiveKind.Box, 0.08f, 0.08f, 6.2f, ShipYard.Xf(0, 1.45f, -0.2f)));
        truss = ShipYard.Union(truss, ShipYard.Prim(MeshPrimitiveKind.Box, 0.08f, 0.08f, 6.2f, ShipYard.Xf(0, 0.85f, -0.2f)));
        ship = ShipYard.Union(ship, truss);
        Write(stageDir, 4, "spine-truss", ship);

        // 05 Stern + bridge tower
        ship = ShipYard.Union(ship, ShipYard.Prim(MeshPrimitiveKind.Box, 3.2f, 1.5f, 4.0f, ShipYard.Xf(0, 0.5f, -5.5f)));
        ship = ShipYard.Union(ship, ShipYard.Prim(MeshPrimitiveKind.Box, 2.0f, 0.9f, 1.8f, ShipYard.Xf(0, 1.55f, -4.8f)));
        ship = ShipYard.Union(ship, ShipYard.Prim(MeshPrimitiveKind.Box, 2.6f, 0.55f, 1.2f, ShipYard.Xf(0, 2.15f, -4.6f))); // bridge head
        ship = ShipYard.Union(ship, ShipYard.Prim(MeshPrimitiveKind.Box, 1.4f, 0.7f, 1.0f, ShipYard.Xf(0, 1.2f, -6.6f)));
        Write(stageDir, 5, "stern-bridge", ship);

        // 06 Drydock cuts — hangars + panel grooves + keel well
        var hangarL = ShipYard.Prim(MeshPrimitiveKind.Box, 1.1f, 0.85f, 2.2f, ShipYard.Xf(-1.9f, 0.2f, 0.8f));
        var hangarR = ShipYard.Prim(MeshPrimitiveKind.Box, 1.1f, 0.85f, 2.2f, ShipYard.Xf(1.9f, 0.2f, 0.8f));
        ship = ShipYard.Cut(ship, hangarL);
        ship = ShipYard.Cut(ship, hangarR);
        ship = ShipYard.Cut(ship, ShipYard.Prim(MeshPrimitiveKind.Box, 0.9f, 0.7f, 1.6f, ShipYard.Xf(0, -0.35f, 5.5f)));
        ship = ShipYard.CutPanels(ship, z0: -6.5f, z1: 7.5f, y: 1.05f, stepZ: 0.55f, depth: 0.22f, width: 2.8f);
        ship = ShipYard.CutPanels(ship, z0: -6.0f, z1: 5.5f, y: -0.4f, stepZ: 0.7f, depth: 0.18f, width: 1.6f);
        Write(stageDir, 6, "boole-cut", ship);

        // 07 Engines — outriggers + 4 pods
        var spar = ShipYard.Prim(MeshPrimitiveKind.Box, 3.6f, 0.28f, 0.45f, ShipYard.Xf(0, 0.35f, -7.4f));
        ship = ShipYard.Union(ship, spar);
        ship = ShipYard.Union(ship, ShipYard.Prim(MeshPrimitiveKind.Box, 3.6f, 0.28f, 0.45f, ShipYard.Xf(0, -0.25f, -7.4f)));
        EditableMesh Pod(float x, float y) =>
            ShipYard.Union(
                ShipYard.Prim(MeshPrimitiveKind.Box, 1.1f, 0.85f, 1.6f, ShipYard.Xf(x, y, -8.3f)),
                ShipYard.Prim(MeshPrimitiveKind.Box, 0.85f, 0.65f, 0.55f, ShipYard.Xf(x, y, -9.2f)));
        ship = ShipYard.Union(ship, Pod(-2.4f, 0.55f));
        ship = ShipYard.Union(ship, Pod(2.4f, 0.55f));
        ship = ShipYard.Union(ship, Pod(-2.4f, -0.45f));
        ship = ShipYard.Union(ship, Pod(2.4f, -0.45f));
        // thruster recesses
        foreach (var (x, y) in new[] { (-2.4f, 0.55f), (2.4f, 0.55f), (-2.4f, -0.45f), (2.4f, -0.45f) })
            ship = ShipYard.Cut(ship, ShipYard.Prim(MeshPrimitiveKind.Box, 0.7f, 0.5f, 0.45f, ShipYard.Xf(x, y, -9.35f)));
        Write(stageDir, 7, "engines", ship);

        // 08 Greeble — turret array on bow, vent row, bridge sensors
        var turret = ShipYard.Union(
            ShipYard.Prim(MeshPrimitiveKind.Cylinder, 0.35f, 0.25f, 0.35f, ShipYard.Xf(0, 0, 0), 10),
            ShipYard.Prim(MeshPrimitiveKind.Capsule, 0.08f, 0.55f, 0.08f, ShipYard.Xf(-0.12f, 0.15f, 0.35f, 90, 0, 0), 8));
        turret = ShipYard.Union(turret, ShipYard.Prim(MeshPrimitiveKind.Capsule, 0.08f, 0.55f, 0.08f, ShipYard.Xf(0.12f, 0.15f, 0.35f, 90, 0, 0), 8));
        var turrets = ShipArray.Linear(turret, 2, new Vector3(0, 0, -1.1f));
        turrets.Transform(ShipYard.Xf(0, 1.25f, 6.8f));
        ship = ShipYard.Union(ship, turrets);

        var vent = ShipYard.Prim(MeshPrimitiveKind.Box, 0.22f, 0.35f, 0.12f, Matrix4x4.Identity);
        var ventsPort = ShipArray.Linear(vent, 8, new Vector3(0, 0, 0.55f));
        ventsPort.Transform(ShipYard.Xf(-2.15f, 0.55f, -1.5f));
        ship = ShipYard.Union(ship, ShipArray.SymmetricX(ventsPort));

        var sensor = ShipYard.Prim(MeshPrimitiveKind.Box, 0.35f, 0.2f, 0.45f, Matrix4x4.Identity);
        var sensors = ShipArray.Linear(sensor, 3, new Vector3(0.55f, 0, 0));
        sensors.Transform(ShipYard.Xf(-0.55f, 2.4f, -4.3f));
        ship = ShipYard.Union(ship, sensors);

        Write(stageDir, 8, "complete", ship);

        File.Copy(Path.Combine(stageDir, "corvette-stage-08.nov3djson"), finalPath, overwrite: true);
        Console.WriteLine($"FINAL|{finalPath}|verts={ship.VertexCount}|tris={ship.TriangleCount}");
    }

    private static void Write(string dir, int stage, string label, EditableMesh ship)
    {
        var doc = new SceneDocument
        {
            Name = $"Scene — {label}",
            CreatedAt = DateTimeOffset.UtcNow,
            ModifiedAt = DateTimeOffset.UtcNow,
        };
        var root = new GroupNode { Name = "Root" };
        var cam = new CameraNode
        {
            Name = "Camera",
            ParentId = root.Id,
            Transform = new SceneTransform { Position = [14f, 6f, 12f] },
            Target = [0, 0.6f, 0],
            FovDeg = 38f,
        };
        var mesh = new MeshNode
        {
            Name = "Mesh",
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
                Transform = new SceneTransform { Position = [8, 10, 6], RotationDeg = [40, -30, 0] },
            },
            new LightNode
            {
                Name = "Fill Light",
                ParentId = root.Id,
                LightKind = LightKind.Omni,
                Intensity = 2.6f,
                Color = [0.85f, 0.9f, 1f],
                Transform = new SceneTransform { Position = [0, 0.2f, -9.6f] },
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
        doc.ActiveCameraId = cam.Id;
        doc.SelectionId = mesh.Id;
        var path = Path.Combine(dir, $"corvette-stage-{stage:00}.nov3djson");
        SceneSerializer.Save(doc, path);
        Console.WriteLine($"{stage}|{label}|{path}|verts={ship.VertexCount}|tris={ship.TriangleCount}");
    }
}
