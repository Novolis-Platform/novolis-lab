using System.Numerics;
using Novolis.Math.Geometry;
using Novolis.ThreeD;

namespace TroopCorvetteBuilder;

/// <summary>Cloner-equivalent linear / mirror arrays that stay as one EditableMesh.</summary>
internal static class ShipArray
{
    public static EditableMesh Linear(EditableMesh cell, int count, Vector3 offset)
    {
        ArgumentNullException.ThrowIfNull(cell);
        count = Math.Max(1, count);
        var result = cell.Clone();
        for (var i = 1; i < count; i++)
        {
            var copy = cell.Clone();
            copy.Transform(Matrix4x4.CreateTranslation(offset * i));
            result = MeshBoolean.Concat(result, copy);
        }

        return result;
    }

    public static EditableMesh SymmetricX(EditableMesh portHalf)
    {
        ArgumentNullException.ThrowIfNull(portHalf);
        var stbd = portHalf.Clone();
        stbd.Transform(Matrix4x4.CreateScale(-1f, 1f, 1f));
        stbd.ReverseWinding();
        return MeshBoolean.Concat(portHalf, stbd);
    }
}

internal static class ShipYard
{
    public static EditableMesh Prim(MeshPrimitiveKind kind, float sx, float sy, float sz, Matrix4x4 xf, int segments = 12)
    {
        var node = new MeshNode
        {
            Primitive = kind,
            Size = [sx, sy, sz],
            Segments = segments,
        };
        var mesh = PrimitiveMesher.Tessellate(node);
        mesh.Transform(xf);
        return mesh;
    }

    public static Matrix4x4 Xf(float x, float y, float z, float rx = 0, float ry = 0, float rz = 0) =>
        new SceneTransform
        {
            Position = [x, y, z],
            RotationDeg = [rx, ry, rz],
        }.ToMatrix();

    public static EditableMesh Union(EditableMesh a, EditableMesh b) => MeshBoolean.Apply(a, b, MeshBooleanKind.Union);

    public static EditableMesh Cut(EditableMesh hull, EditableMesh cutter) =>
        MeshBoolean.Apply(hull, cutter, MeshBooleanKind.Difference);

    /// <summary>Dorsal / flank panel grooves via oversized box cutters (AABB difference).</summary>
    public static EditableMesh CutPanels(EditableMesh hull, float z0, float z1, float y, float stepZ, float depth, float width)
    {
        var work = hull;
        for (var z = z0; z <= z1 + 0.01f; z += stepZ)
        {
            var cutter = Prim(MeshPrimitiveKind.Box, width, depth, 0.08f, Xf(0, y, z));
            work = Cut(work, cutter);
        }

        return work;
    }

    public static EditableMesh XBrace(float size = 0.55f, float thickness = 0.06f)
    {
        var a = Prim(MeshPrimitiveKind.Box, thickness, size, thickness, Xf(0, 0, 0, 0, 0, 45));
        var b = Prim(MeshPrimitiveKind.Box, thickness, size, thickness, Xf(0, 0, 0, 0, 0, -45));
        return Union(a, b);
    }
}
