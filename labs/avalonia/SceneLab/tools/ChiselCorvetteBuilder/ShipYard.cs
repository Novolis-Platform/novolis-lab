using System.Numerics;
using Novolis.Math.Geometry;
using Novolis.ThreeD;

namespace ChiselCorvetteBuilder;

internal static class ShipYard
{
    public static EditableMesh Prim(MeshPrimitiveKind kind, float sx, float sy, float sz, Matrix4x4 xf, int segments = 12)
    {
        var node = new MeshNode { Primitive = kind, Size = [sx, sy, sz], Segments = segments };
        var mesh = PrimitiveMesher.Tessellate(node);
        mesh.Transform(xf);
        return mesh;
    }

    public static Matrix4x4 Xf(float x, float y, float z, float rx = 0, float ry = 0, float rz = 0) =>
        new SceneTransform { Position = [x, y, z], RotationDeg = [rx, ry, rz] }.ToMatrix();

    public static EditableMesh Union(EditableMesh a, EditableMesh b) => MeshBoolean.Concat(a, b);

    public static EditableMesh CutOpening(EditableMesh hull, EditableMesh cutter) =>
        MeshBoolean.Apply(hull, cutter, MeshBooleanKind.Difference);

    /// <summary>Thin outer shell from six wall plates (interior-first — do not carve solids).</summary>
    public static EditableMesh ModuleShell(float sx, float sy, float sz, float wall = 0.05f)
    {
        wall = System.Math.Clamp(wall, 0.02f, System.Math.Min(sx, System.Math.Min(sy, sz)) * 0.2f);
        var hx = sx * 0.5f;
        var hy = sy * 0.5f;
        var hz = sz * 0.5f;
        var ix = sx - 2f * wall;
        var iy = sy - 2f * wall;
        var iz = sz - 2f * wall;

        var shell = Prim(MeshPrimitiveKind.Box, sx, wall, sz, Xf(0, -hy + wall * 0.5f, 0));
        shell = Union(shell, Prim(MeshPrimitiveKind.Box, sx, wall, sz, Xf(0, hy - wall * 0.5f, 0)));
        shell = Union(shell, Prim(MeshPrimitiveKind.Box, wall, iy, iz, Xf(-hx + wall * 0.5f, 0, 0)));
        shell = Union(shell, Prim(MeshPrimitiveKind.Box, wall, iy, iz, Xf(hx - wall * 0.5f, 0, 0)));
        shell = Union(shell, Prim(MeshPrimitiveKind.Box, ix, iy, wall, Xf(0, 0, -hz + wall * 0.5f)));
        shell = Union(shell, Prim(MeshPrimitiveKind.Box, ix, iy, wall, Xf(0, 0, hz - wall * 0.5f)));
        return shell;
    }

    /// <summary>
    /// Soft-edged module: inset plate shell + capsule edge beads + corner spheres.
    /// Reads as rounded hard-surface in wireframe without true CSG fillets.
    /// </summary>
    public static EditableMesh SoftModuleShell(float sx, float sy, float sz, float wall = 0.05f, float fillet = 0.14f, int segments = 10)
    {
        fillet = System.Math.Clamp(fillet, 0.06f, System.Math.Min(sx, System.Math.Min(sy, sz)) * 0.22f);
        var inset = fillet * 0.55f;
        var shell = ModuleShell(sx - inset, sy - inset, sz - inset, wall);
        shell = Union(shell, SoftEdgeCage(sx, sy, sz, fillet, segments));
        return shell;
    }

    /// <summary>12 capsule edge beads + 8 corner spheres framing a box.</summary>
    public static EditableMesh SoftEdgeCage(float sx, float sy, float sz, float fillet = 0.14f, int segments = 10)
    {
        fillet = System.Math.Max(0.05f, fillet);
        var hx = sx * 0.5f;
        var hy = sy * 0.5f;
        var hz = sz * 0.5f;
        var d = fillet * 2f;
        var cornerSeg = System.Math.Max(6, segments - 2);
        EditableMesh? cage = null;

        void Add(EditableMesh part) => cage = cage is null ? part : Union(cage, part);

        // Edges parallel to Z
        foreach (var (x, y) in new[] { (-hx, -hy), (hx, -hy), (-hx, hy), (hx, hy) })
            Add(Prim(MeshPrimitiveKind.Capsule, d, sz, d, Xf(x, y, 0, 90, 0, 0), segments));

        // Edges parallel to Y
        foreach (var (x, z) in new[] { (-hx, -hz), (hx, -hz), (-hx, hz), (hx, hz) })
            Add(Prim(MeshPrimitiveKind.Capsule, d, sy, d, Xf(x, 0, z, 0, 0, 0), segments));

        // Edges parallel to X
        foreach (var (y, z) in new[] { (-hy, -hz), (hy, -hz), (-hy, hz), (hy, hz) })
            Add(Prim(MeshPrimitiveKind.Capsule, d, sx, d, Xf(0, y, z, 0, 0, 90), segments));

        // Corners
        foreach (var x in new[] { -hx, hx })
        foreach (var y in new[] { -hy, hy })
        foreach (var z in new[] { -hz, hz })
            Add(Prim(MeshPrimitiveKind.Sphere, d, d, d, Xf(x, y, z), cornerSeg));

        return cage ?? Prim(MeshPrimitiveKind.Sphere, d, d, d, Matrix4x4.Identity, cornerSeg);
    }

    /// <summary>Soft armor tile (flattened capsule) for exterior skin arrays.</summary>
    public static EditableMesh SoftArmorTile(float length, float width, float bulge = 0.1f, int segments = 12) =>
        Prim(MeshPrimitiveKind.Capsule, width, length, width, Xf(0, 0, 0, 0, 0, 90), segments);

    /// <summary>Longitudinal fairing rail (capsule stringer).</summary>
    public static EditableMesh SoftStringer(float length, float radius = 0.1f, int segments = 12) =>
        Prim(MeshPrimitiveKind.Capsule, radius * 2f, length, radius * 2f, Xf(0, 0, 0, 90, 0, 0), segments);

    /// <summary>Blunted bow fairing: capsule body + sphere tip.</summary>
    public static EditableMesh SoftNose(float radius, float length, int segments = 16)
    {
        var body = Prim(MeshPrimitiveKind.Capsule, radius * 2f, length, radius * 2f, Xf(0, 0, 0, 90, 0, 0), segments);
        var tip = Prim(MeshPrimitiveKind.Sphere, radius * 1.85f, radius * 1.85f, radius * 1.85f,
            Xf(0, 0, length * 0.42f), segments);
        return Union(body, tip);
    }

    public static EditableMesh DeckStack(int count, float spacing, float sx, float sz, float thickness = 0.04f)
    {
        count = System.Math.Max(1, count);
        var deck = Prim(MeshPrimitiveKind.Box, sx, thickness, sz, Matrix4x4.Identity);
        var startY = -((count - 1) * spacing) * 0.5f;
        deck.Transform(Matrix4x4.CreateTranslation(0, startY, 0));
        return ShipArray.Linear(deck, count, new Vector3(0, spacing, 0));
    }

    public static EditableMesh Corridor(float length, float width, float height, float wall = 0.04f)
    {
        var floor = Prim(MeshPrimitiveKind.Box, width, wall, length, Xf(0, -height * 0.5f + wall * 0.5f, 0));
        var left = Prim(MeshPrimitiveKind.Box, wall, height, length, Xf(-width * 0.5f + wall * 0.5f, 0, 0));
        var right = Prim(MeshPrimitiveKind.Box, wall, height, length, Xf(width * 0.5f - wall * 0.5f, 0, 0));
        var ceiling = Prim(MeshPrimitiveKind.Box, width, wall, length, Xf(0, height * 0.5f - wall * 0.5f, 0));
        return Union(Union(Union(floor, left), right), ceiling);
    }

    public static EditableMesh PodBay(int rows, int cols, float pitchY, float pitchZ,
        float cellSx = 0.38f, float cellSy = 0.48f, float cellSz = 0.58f)
    {
        var cell = Prim(MeshPrimitiveKind.Box, cellSx, cellSy, cellSz, Matrix4x4.Identity);
        return ShipArray.Grid(cell, rows, cols, new Vector3(0, pitchY, 0), new Vector3(0, 0, pitchZ));
    }

    public static EditableMesh PressureRings(int count, float spacing, float radius, float thickness = 0.08f, int segments = 20)
    {
        var ring = Prim(MeshPrimitiveKind.Torus, radius * 2f, thickness, radius * 2f, Matrix4x4.Identity, segments);
        var startZ = -((count - 1) * spacing) * 0.5f;
        ring.Transform(Matrix4x4.CreateTranslation(0, 0, startZ));
        return ShipArray.Linear(ring, count, new Vector3(0, 0, spacing));
    }

    public static EditableMesh Catwalk(float length, float width, float railHeight = 0.45f)
    {
        var floor = Prim(MeshPrimitiveKind.Box, width, 0.04f, length, Matrix4x4.Identity);
        var railL = Prim(MeshPrimitiveKind.Box, 0.03f, railHeight, length, Xf(-width * 0.5f, railHeight * 0.5f, 0));
        var railR = Prim(MeshPrimitiveKind.Box, 0.03f, railHeight, length, Xf(width * 0.5f, railHeight * 0.5f, 0));
        return Union(Union(floor, railL), railR);
    }

    public static EditableMesh BulkheadStack(int count, float spacing, float sx, float sy, float thickness = 0.05f)
    {
        var wall = Prim(MeshPrimitiveKind.Box, sx, sy, thickness, Matrix4x4.Identity);
        var startZ = -((count - 1) * spacing) * 0.5f;
        wall.Transform(Matrix4x4.CreateTranslation(0, 0, startZ));
        return ShipArray.Linear(wall, count, new Vector3(0, 0, spacing));
    }

    public static EditableMesh LadderWell(float height, float width = 0.55f)
    {
        var rails = Prim(MeshPrimitiveKind.Box, 0.04f, height, 0.04f, Xf(-width * 0.5f, 0, 0));
        rails = Union(rails, Prim(MeshPrimitiveKind.Box, 0.04f, height, 0.04f, Xf(width * 0.5f, 0, 0)));
        var rungCount = System.Math.Max(4, (int)(height / 0.35f));
        var rung = Prim(MeshPrimitiveKind.Box, width, 0.03f, 0.03f, Matrix4x4.Identity);
        var rungs = ShipArray.Linear(rung, rungCount, new Vector3(0, 0.35f, 0));
        rungs.Transform(Matrix4x4.CreateTranslation(0, -height * 0.5f + 0.2f, 0));
        return Union(rails, rungs);
    }

    /// <summary>Sensor dish on a boom — strong OpenGL wireframe silhouette.</summary>
    public static EditableMesh SensorDish(float dishRadius = 0.95f, float boomLength = 1.6f, int segments = 16)
    {
        var boom = Prim(MeshPrimitiveKind.Capsule, 0.1f, boomLength, 0.1f, Xf(0, boomLength * 0.5f, 0), 8);
        var dish = Prim(MeshPrimitiveKind.Torus, dishRadius * 2f, 0.08f, dishRadius * 2f,
            Xf(0, boomLength, 0, 90, 0, 0), segments);
        var hub = Prim(MeshPrimitiveKind.Sphere, 0.28f, 0.28f, 0.28f, Xf(0, boomLength, 0), 12);
        var rim = Prim(MeshPrimitiveKind.Torus, dishRadius * 1.55f, 0.05f, dishRadius * 1.55f,
            Xf(0, boomLength, 0.05f, 90, 0, 0), segments);
        return Union(Union(Union(boom, dish), hub), rim);
    }

    /// <summary>Flat radiator / heat-dump panel array (reads as lattice in wireframe).</summary>
    public static EditableMesh RadiatorPanel(float length, float height, int fins = 7)
    {
        fins = System.Math.Clamp(fins, 3, 16);
        var frame = ModuleShell(System.Math.Max(0.18f, length * 0.04f), height, length, 0.03f);
        var fin = Prim(MeshPrimitiveKind.Box, 0.03f, height * 0.85f, length * 0.9f, Matrix4x4.Identity);
        var pitch = height * 0.85f / (fins - 1);
        var bank = ShipArray.Linear(fin, fins, new Vector3(0, pitch, 0));
        bank.Transform(Matrix4x4.CreateTranslation(0, -height * 0.4f, 0));
        return Union(frame, bank);
    }

    /// <summary>Docking collar: stacked torus rings + flange.</summary>
    public static EditableMesh DockingCollar(float radius = 0.85f, int rings = 3, int segments = 16)
    {
        rings = System.Math.Clamp(rings, 2, 6);
        var ring = Prim(MeshPrimitiveKind.Torus, radius * 2f, 0.07f, radius * 2f, Matrix4x4.Identity, segments);
        var stack = ShipArray.Linear(ring, rings, new Vector3(0, 0, 0.14f));
        stack.Transform(Matrix4x4.CreateTranslation(0, 0, -((rings - 1) * 0.14f) * 0.5f));
        var flange = Prim(MeshPrimitiveKind.Cylinder, radius * 2.1f, 0.08f, radius * 2.1f,
            Xf(0, 0, rings * 0.07f), segments);
        return Union(stack, flange);
    }

    /// <summary>External cargo clamp (claw + hinge) for mid-module outsides.</summary>
    public static EditableMesh CargoClamp(float reach = 0.9f)
    {
        var hinge = Prim(MeshPrimitiveKind.Sphere, 0.22f, 0.22f, 0.22f, Matrix4x4.Identity, 10);
        var arm = Prim(MeshPrimitiveKind.Capsule, 0.12f, reach, 0.12f, Xf(0, 0, reach * 0.5f, 90, 0, 0), 8);
        var jaw = Prim(MeshPrimitiveKind.Box, 0.35f, 0.08f, 0.22f, Xf(0, 0, reach));
        var tip = Prim(MeshPrimitiveKind.Sphere, 0.14f, 0.14f, 0.14f, Xf(0, 0, reach + 0.12f), 8);
        return Union(Union(Union(hinge, arm), jaw), tip);
    }

    /// <summary>Engine bell: truncated cone stack + rim torus.</summary>
    public static EditableMesh EngineBell(float radius = 0.55f, float length = 1.1f, int segments = 14)
    {
        var bell = Prim(MeshPrimitiveKind.Cone, radius * 2f, length, radius * 2f, Xf(0, 0, 0, 90, 0, 0), segments);
        var rim = Prim(MeshPrimitiveKind.Torus, radius * 1.85f, 0.07f, radius * 1.85f,
            Xf(0, 0, -length * 0.45f, 90, 0, 0), segments);
        var throat = Prim(MeshPrimitiveKind.Cylinder, radius * 0.7f, length * 0.35f, radius * 0.7f,
            Xf(0, 0, length * 0.15f, 90, 0, 0), segments);
        return Union(Union(bell, rim), throat);
    }

    /// <summary>Bridge window frame (thin torus oval stand-in).</summary>
    public static EditableMesh ViewportRing(float radius = 0.35f, int segments = 14) =>
        Prim(MeshPrimitiveKind.Torus, radius * 2f, 0.045f, radius * 2f, Matrix4x4.Identity, segments);

    /// <summary>Running-light / marker pod.</summary>
    public static EditableMesh MarkerPod(float size = 0.18f) =>
        Prim(MeshPrimitiveKind.Sphere, size, size, size, Matrix4x4.Identity, 10);

    /// <summary>RCS thruster cluster (cross of short cones + hub).</summary>
    public static EditableMesh ThrusterCluster(float scale = 0.35f, int segments = 10)
    {
        var hub = Prim(MeshPrimitiveKind.Sphere, scale * 0.55f, scale * 0.55f, scale * 0.55f, Matrix4x4.Identity, segments);
        EditableMesh cluster = hub;
        foreach (var (rx, ry, rz) in new[]
                 {
                     (90f, 0f, 0f), (-90f, 0f, 0f), (0f, 0f, 90f), (0f, 0f, -90f), (0f, 90f, 0f), (0f, -90f, 0f),
                 })
        {
            var nozzle = Prim(MeshPrimitiveKind.Cone, scale * 0.45f, scale * 0.7f, scale * 0.45f,
                Xf(0, 0, scale * 0.55f, rx, ry, rz), segments);
            cluster = Union(cluster, nozzle);
        }

        return cluster;
    }

    /// <summary>Vertical stabilizer / fin plate with soft leading edge.</summary>
    public static EditableMesh StabilizerFin(float height = 1.8f, float chord = 1.4f, float thickness = 0.12f)
    {
        var plate = Prim(MeshPrimitiveKind.Box, thickness, height, chord, Matrix4x4.Identity);
        var leading = Prim(MeshPrimitiveKind.Capsule, thickness * 1.4f, height * 0.95f, thickness * 1.4f,
            Xf(0, 0, chord * 0.48f), 10);
        return Union(plate, leading);
    }

    /// <summary>Pipe / conduit run (capsule segments) for keel utility lines.</summary>
    public static EditableMesh ConduitRun(float length, float radius = 0.06f, int segments = 8) =>
        SoftStringer(length, radius, segments);

    /// <summary>Overhead cargo gantry: rail + trolley + hook.</summary>
    public static EditableMesh CargoGantry(float span = 3.2f, float height = 0.9f)
    {
        var rail = Prim(MeshPrimitiveKind.Box, span, 0.08f, 0.12f, Matrix4x4.Identity);
        var uprightL = Prim(MeshPrimitiveKind.Capsule, 0.1f, height, 0.1f, Xf(-span * 0.45f, -height * 0.5f, 0), 8);
        var uprightR = Prim(MeshPrimitiveKind.Capsule, 0.1f, height, 0.1f, Xf(span * 0.45f, -height * 0.5f, 0), 8);
        var trolley = Prim(MeshPrimitiveKind.Box, 0.35f, 0.22f, 0.28f, Xf(0, -0.05f, 0));
        var hook = Prim(MeshPrimitiveKind.Capsule, 0.08f, 0.55f, 0.08f, Xf(0, -0.45f, 0), 8);
        return Union(Union(Union(Union(rail, uprightL), uprightR), trolley), hook);
    }

    /// <summary>Hangar mouth frame: torus lip + side posts.</summary>
    public static EditableMesh HangarMouth(float width = 1.4f, float height = 1.6f, int segments = 14)
    {
        var lip = Prim(MeshPrimitiveKind.Torus, width, 0.08f, height * 0.55f, Xf(0, 0, 0, 0, 90, 0), segments);
        var postL = Prim(MeshPrimitiveKind.Capsule, 0.1f, height, 0.1f, Xf(-width * 0.55f, 0, 0), 8);
        var postR = Prim(MeshPrimitiveKind.Capsule, 0.1f, height, 0.1f, Xf(width * 0.55f, 0, 0), 8);
        var lintel = Prim(MeshPrimitiveKind.Box, width * 1.15f, 0.1f, 0.12f, Xf(0, height * 0.48f, 0));
        return Union(Union(Union(lip, postL), postR), lintel);
    }
}
